using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Infrastructure.Combat;

public sealed partial class InMemoryCombatState
{
    /// <inheritdoc />
    public int RestoreHealth(CombatantId owner, int amount)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        lock (_gate)
        {
            var actor = _data.Actors[owner];
            var restored = actor.IsDefeated ? 0 : Math.Min(amount, actor.MaxHealth - actor.Health);

            if (restored == 0)
            {
                return 0;
            }

            _data = _data with
            {
                Version = checked(_data.Version + 1),
                Actors = _data.Actors.SetItem(owner, Copy(actor, actor.Health + restored, actor.Effects))
            };

            return restored;
        }
    }
}
