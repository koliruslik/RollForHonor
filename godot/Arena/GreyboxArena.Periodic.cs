using System.Linq;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Godot.CombatDemo;
using RollForHonor.Godot.UI.Hud;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.Arena;

public partial class GreyboxArena
{
    private void StepPeriodicEffects()
    {
        foreach (var tick in _runtime.PeriodicEffects.Advance(_runtime.GameTime))
        {
            if (!tick.IsCommitted || tick.Outcome is not ResolvedCombat resolved)
            {
                continue;
            }

            CombatResolved?.Invoke(resolved.Resolution);
            var enemy = _enemies
                .Concat(_adminEnemies)
                .FirstOrDefault(actor => actor.CombatantId == tick.TargetId);
            var name = enemy is not null ? ActorName(enemy) : tick.TargetId == Player.CombatantId ? L.Text("Player") : L.Text("Target");
            var status = DemoEffectCatalog.Name(tick.Request.TickEffect.Definition.Id);
            GetNode<ArenaHud>("Presentation").RecordAction(L.Format($"{status}: {name} takes {resolved.Resolution.Result.Target.DamageResolution?.AfterDefenses.Sum() ?? 0} damage"));
            Append(DemoCombatJournal.FormatTick(tick, status, name));
        }

        foreach (var target in GetTargets())
        {
            var state = _runtime.CombatState.Read(Player.CombatantId, target.CombatantId).Target;
            target.GetNode<global::Godot.Label3D>("Name").Text = L.Format($"{target.Name}\n{state.Health}/{state.MaxHealth} HP");
        }
    }
}
