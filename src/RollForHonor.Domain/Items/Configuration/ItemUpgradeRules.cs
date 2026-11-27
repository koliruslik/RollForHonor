using RollForHonor.Domain.Items.Models;

namespace RollForHonor.Domain.Items.Configuration;

/// <summary>Captures replaceable enhancement, evolution and resource-price rules.</summary>
public sealed class ItemUpgradeRules
{
    /// <summary>Gets the authored v1 rules for first-generation equipment.</summary>
    public static ItemUpgradeRules Default { get; } = new([14, 20, 27, 39, 54, 77, 108, 153, 216, 305],
        [3, 5, 7, 10], [35, 70, 140, 280]);

    /// <summary>Gets the identifier of the resource spent by operations.</summary>
    public string ResourceId { get; }

    /// <summary>Gets the linear base increase per enhancement step.</summary>
    public decimal EnhancementStep { get; }

    /// <summary>Gets the maximum supported base-generation gate for this price table.</summary>
    public int MaximumBaseLevel { get; }

    private readonly long[] _enhancementPrices;
    private readonly int[] _gates;
    private readonly long[] _evolutionPrices;
    private readonly long _rerollBase;
    private readonly long _rerollIncrement;
    private readonly decimal _lockMultiplier;
    private readonly decimal _additionMultiplier;

    /// <summary>Copies a complete ten-step price curve and four rarity transitions.</summary>
    /// <param name="enhancementPrices">Ten positive per-step prices.</param>
    /// <param name="gates">Four evolution gates within 1–10.</param>
    /// <param name="evolutionPrices">Four positive evolution fees.</param>
    /// <param name="resourceId">Nonblank payment resource identity.</param>
    /// <param name="enhancementStep">Nonnegative linear enhancement fraction.</param>
    /// <param name="rerollBase">Positive base fee for a full reroll.</param>
    /// <param name="rerollIncrement">Nonnegative increment per additional occupied affix.</param>
    /// <param name="lockMultiplier">Multiplier of at least one per locked affix.</param>
    /// <param name="additionMultiplier">Positive multiplier of the unlocked base reroll fee.</param>
    /// <param name="maximumBaseLevel">Largest base requirement level supported by these prices.</param>
    /// <exception cref="ArgumentException">An array or resource identity is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A numeric coefficient is invalid.</exception>
    public ItemUpgradeRules(IEnumerable<long> enhancementPrices, IEnumerable<int> gates, IEnumerable<long> evolutionPrices,
        string resourceId = "gold", decimal enhancementStep = 0.05m, long rerollBase = 48, long rerollIncrement = 12,
        decimal lockMultiplier = 1.5m, decimal additionMultiplier = 8, int maximumBaseLevel = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);
        ArgumentNullException.ThrowIfNull(enhancementPrices);
        ArgumentNullException.ThrowIfNull(gates);
        ArgumentNullException.ThrowIfNull(evolutionPrices);
        _enhancementPrices = enhancementPrices.ToArray();
        _gates = gates.ToArray();
        _evolutionPrices = evolutionPrices.ToArray();

        if (_enhancementPrices.Length != 10 || _gates.Length != 4 || _evolutionPrices.Length != 4
            || _enhancementPrices.Any(price => price <= 0) || _evolutionPrices.Any(price => price <= 0)
            || _gates.Any(gate => gate is < 1 or > 10))
        {
            throw new ArgumentException("Upgrade tables require ten positive step prices and four valid gates/fees.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(enhancementStep);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rerollBase);
        ArgumentOutOfRangeException.ThrowIfNegative(rerollIncrement);
        ArgumentOutOfRangeException.ThrowIfLessThan(lockMultiplier, 1);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(additionMultiplier);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBaseLevel);
        ResourceId = resourceId;
        EnhancementStep = enhancementStep;
        MaximumBaseLevel = maximumBaseLevel;
        _rerollBase = rerollBase;
        _rerollIncrement = rerollIncrement;
        _lockMultiplier = lockMultiplier;
        _additionMultiplier = additionMultiplier;
    }

    /// <summary>Gets the fee for one enhancement step.</summary>
    /// <param name="target">Target level within 1–10.</param>
    /// <returns>The positive resource amount.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The target is outside 1–10.</exception>
    public long EnhancementPrice(int target)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(target, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(target, 10);
        return _enhancementPrices[target - 1];
    }

    /// <summary>Gets the rarity's enhancement cap before evolution; Legendary ends at +10.</summary>
    /// <param name="rarity">A defined item rarity.</param>
    /// <returns>The configured evolution gate, or ten for Legendary.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The rarity is undefined.</exception>
    public int EnhancementLimit(ItemRarity rarity) => rarity == ItemRarity.Legendary ? 10 : EvolutionGate(rarity);

    /// <summary>Gets the minimum level required to leave this rarity.</summary>
    /// <param name="rarity">A nonfinal rarity.</param>
    /// <returns>The required enhancement level.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The rarity is undefined or final.</exception>
    public int EvolutionGate(ItemRarity rarity)
    {
        ValidateEvolutionRarity(rarity);
        return _gates[(int)rarity];
    }

    /// <summary>Gets the separate fee for leaving this rarity.</summary>
    /// <param name="rarity">A nonfinal rarity.</param>
    /// <returns>The positive resource amount.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The rarity is undefined or final.</exception>
    public long EvolutionPrice(ItemRarity rarity)
    {
        ValidateEvolutionRarity(rarity);
        return _evolutionPrices[(int)rarity];
    }

    /// <summary>Calculates the full reroll fee with one final upward rounding.</summary>
    /// <param name="count">Current occupied count.</param>
    /// <param name="locks">Locked count.</param>
    /// <returns>The whole resource amount.</returns>
    public long RerollPrice(int count, int locks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfNegative(locks);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(locks, count);
        var fee = (decimal)checked(_rerollBase + _rerollIncrement * Math.Max(0, count - 1));
        for (var index = 0; index < locks; index++)
        {
            fee *= _lockMultiplier;
        }

        return checked((long)decimal.Ceiling(fee));
    }

    /// <summary>Calculates a random addition's fee independently of locks.</summary>
    /// <param name="count">Occupied count before addition.</param>
    /// <returns>The whole resource amount.</returns>
    public long AdditionPrice(int count) => checked((long)decimal.Ceiling(RerollPrice(count, 0) * _additionMultiplier));

    /// <summary>Calculates the base multiplier for a newly created item at an explicit rarity.</summary>
    /// <param name="rarity">A defined rarity.</param>
    /// <returns>Its cumulative gate-derived base multiplier.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The rarity is undefined.</exception>
    public decimal RarityMultiplier(ItemRarity rarity)
    {
        if (!Enum.IsDefined(rarity))
        {
            throw new ArgumentOutOfRangeException(nameof(rarity));
        }

        return _gates
            .Take((int)rarity)
            .Aggregate(1m, (value, gate) => value * (1 + EnhancementStep * gate));
    }

    private static void ValidateEvolutionRarity(ItemRarity rarity)
    {
        if (!Enum.IsDefined(rarity) || rarity == ItemRarity.Legendary)
        {
            throw new ArgumentOutOfRangeException(nameof(rarity));
        }
    }
}
