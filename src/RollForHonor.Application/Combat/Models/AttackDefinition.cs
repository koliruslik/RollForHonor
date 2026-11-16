using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Resources.Models;

namespace RollForHonor.Application.Combat.Models;

/// <summary>Supplies attack content, base launch costs and a source-local cooldown key.</summary>
public sealed record AttackDefinition
{
    /// <summary>Gets the stable key identifying this attack's cooldown on a source.</summary>
    public string Key { get; }
    /// <summary>Gets the base impact description.</summary>
    public AttackPayload Attack { get; }
    /// <summary>Gets the exact cooldown duration.</summary>
    public TimeSpan Cooldown { get; }
    /// <summary>Gets immutable base costs, unique by resource.</summary>
    public IReadOnlyList<ResourceAmount> BaseCosts { get; }
    /// <summary>Gets the configured contact and lifetime rules.</summary>
    public AttackExecutionRules ExecutionRules { get; }
    /// <summary>Creates an attack definition without performing any launch.</summary>
    /// <param name="key">The nonblank cooldown/content key.</param>
    /// <param name="attack">The base impact description.</param>
    /// <param name="cooldown">A nonnegative duration.</param>
    /// <param name="baseCosts">Unique, non-null resource costs.</param>
    /// <param name="executionRules">Optional contact/lifetime rules; absent preserves unrestricted impacts until the host ends the attack.</param>
    public AttackDefinition(string key, AttackPayload attack, TimeSpan cooldown, IReadOnlyList<ResourceAmount> baseCosts,
        AttackExecutionRules? executionRules = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(attack);
        ArgumentNullException.ThrowIfNull(baseCosts);
        if (cooldown < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(cooldown));
        var costs = baseCosts.ToArray();
        if (costs.Any(cost => cost is null) || costs.Select(cost => cost.Resource).Distinct().Count() != costs.Length)
            throw new ArgumentException("Base costs must be non-null and unique by resource.", nameof(baseCosts));
        Key = key;
        Attack = attack;
        Cooldown = cooldown;
        BaseCosts = Array.AsReadOnly(costs);
        ExecutionRules = executionRules ?? new AttackExecutionRules();
    }
}
