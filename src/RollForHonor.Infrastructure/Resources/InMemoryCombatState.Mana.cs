using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Resources.Models;
using RollForHonor.Domain.Stats;

namespace RollForHonor.Infrastructure.Combat;

public sealed partial class InMemoryCombatState
{
    /// <inheritdoc />
    public decimal RestoreMana(CombatantId owner, decimal amount)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        lock (_gate)
        {
            var resources = _data.Resources[owner];
            var key = new ResourceId("mana");
            var current = resources.GetValueOrDefault(key);
            var maximum = _data.Stats[owner].Get(new(StatType.MaxMana));
            var restored = _data.Actors[owner].IsDefeated ? 0 : Math.Min(amount, Math.Max(0, maximum - current));

            if (restored == 0)
            {
                return 0;
            }

            _data = _data with
            {
                Version = checked(_data.Version + 1),
                Resources = _data.Resources.SetItem(owner, resources.SetItem(key, current + restored))
            };

            return restored;
        }
    }
}
