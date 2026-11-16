using RollForHonor.Domain.Resources.Models;
using RollForHonor.Domain.Resources.Services;

namespace RollForHonor.Domain.Tests.Resources;

public sealed class ResourceCostResolverTests
{
    private static readonly ResourceId Mana = new("mana");
    private static readonly ResourceId Stamina = new("stamina");

    [Theory]
    [InlineData(0, 0, 100)]
    [InlineData(10, 20, 70)]
    [InlineData(60, 60, 0)]
    [InlineData(100, 0, 0)]
    public void ReductionsAdd_AndCannotProduceNegativeCost(int first, int second, int expected)
    {
        var result = Resolver().Resolve([new ResourceAmount(Mana, 100)],
            [new ResourceCostReduction(Mana, first / 100m), new ResourceCostReduction(Mana, second / 100m)]);
        Assert.Equal(expected, Assert.Single(result.Costs).Amount);
        Assert.Equal(100, Assert.Single(result.BaseCosts).Amount);
    }

    [Fact]
    public void EachResourceUsesOnlyItsOwnReductions_AndUnrelatedModifiersDoNotCreateCosts()
    {
        var result = Resolver().Resolve([new ResourceAmount(Mana, 100), new ResourceAmount(Stamina, 50)],
        [
            new ResourceCostReduction(Mana, 0.1m), new ResourceCostReduction(Mana, 0.2m),
            new ResourceCostReduction(Stamina, 0.2m), new ResourceCostReduction(new ResourceId("other"), 1m)
        ]);
        Assert.Equal(new[] { 70m, 40m }, result.Costs.Select(value => value.Amount));
        Assert.Equal(new[] { Mana, Stamina }, result.Costs.Select(value => value.Resource));
    }

    [Fact]
    public void FractionalCostsAreExact_WithoutImplicitIntegerRounding()
    {
        var result = Resolver().Resolve([new ResourceAmount(Mana, 11)], [new ResourceCostReduction(Mana, 0.3m)]);
        Assert.Equal(7.7m, Assert.Single(result.Costs).Amount);
    }

    [Fact]
    public void PolicyCanBeReplacedWithoutChangingResolverOrResourceInputs()
    {
        var result = new ResourceCostResolver(new FixedPolicy(42m)).Resolve(
            [new ResourceAmount(Mana, 100)], [new ResourceCostReduction(Mana, 0.3m)]);
        Assert.Equal(42m, Assert.Single(result.Costs).Amount);
    }

    [Fact]
    public void InvalidPolicyCannotProduceNegativeResourceSpending()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResourceCostResolver(new FixedPolicy(-1m))
            .Resolve([new ResourceAmount(Mana, 100)], []));
    }

    [Fact]
    public void ResultKeepsCopiedInputs_AndCannotBeChangedThroughMutableListInterfaces()
    {
        var costs = new[] { new ResourceAmount(Mana, 100) };
        var modifiers = new[] { new ResourceCostReduction(Mana, 0.3m) };
        var result = Resolver().Resolve(costs, modifiers);
        costs[0] = new ResourceAmount(Mana, 999);
        modifiers[0] = new ResourceCostReduction(Mana, 1m);
        Assert.Equal(100m, Assert.Single(result.BaseCosts).Amount);
        Assert.Equal(0.3m, Assert.Single(result.Reductions).Fraction);
        Assert.Equal(70m, Assert.Single(result.Costs).Amount);
        Assert.Throws<NotSupportedException>(() => ((IList<ResourceAmount>)result.Costs)[0] = costs[0]);
    }

    [Fact]
    public void DuplicateBaseResourcesAreRejectedInsteadOfAmbiguousDoubleCharging()
    {
        Assert.Throws<ArgumentException>(() => Resolver().Resolve(
            [new ResourceAmount(Mana, 10), new ResourceAmount(Mana, 20)], []));
    }

    [Fact]
    public void FreeActionsAndZeroBaseCostsRemainFree()
    {
        Assert.Empty(Resolver().Resolve([], [new ResourceCostReduction(Mana, 1m)]).Costs);
        Assert.Equal(0, Assert.Single(Resolver().Resolve([new ResourceAmount(Mana, 0)], []).Costs).Amount);
    }

    private static ResourceCostResolver Resolver() => new(new AdditiveResourceCostPolicy());
    private sealed class FixedPolicy(decimal value) : IResourceCostPolicy
    {
        public decimal Calculate(decimal baseCost, IReadOnlyList<decimal> reductions) => value;
    }
}
