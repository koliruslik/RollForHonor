using RollForHonor.Domain.Combat.State;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Domain.Tests.Combat.State;

/// <summary>Verifies manual selection, combat refresh and timeout boundaries.</summary>
public sealed class EngagementStateTests
{
    /// <summary>Automatic targets expire exactly at the timeout and repeated interactions refresh it.</summary>
    [Fact]
    public void AutomaticSelectionExpiresAtTheRefreshedBoundary()
    {
        var state = new EngagementState();
        var target = new CombatantId(Guid.NewGuid());
        state.Record(target, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        state.Record(target, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5));
        state.Advance(TimeSpan.FromSeconds(7), _ => true);
        Assert.Equal(target, state.TargetId);
        Assert.True(state.IsInCombat(TimeSpan.FromSeconds(7)));
        state.Advance(TimeSpan.FromSeconds(8), _ => true);
        Assert.Null(state.TargetId);
        Assert.False(state.IsInCombat(TimeSpan.FromSeconds(8)));
    }

    /// <summary>A manual target survives unrelated contacts and timeout, but not death or explicit clearing.</summary>
    [Fact]
    public void ManualSelectionIsStableUntilClearedOrDefeated()
    {
        var state = new EngagementState();
        var selected = new CombatantId(Guid.NewGuid());
        state.Select(selected);
        state.Record(new(Guid.NewGuid()), TimeSpan.Zero, TimeSpan.FromSeconds(5));
        state.Advance(TimeSpan.FromSeconds(6), _ => true);
        Assert.Equal(selected, state.TargetId);
        state.Advance(TimeSpan.FromSeconds(6), _ => false);
        Assert.Null(state.TargetId);
        state.Select(selected);
        state.Select(null);
        Assert.Null(state.TargetId);
        Assert.False(state.IsManual);
    }
}
