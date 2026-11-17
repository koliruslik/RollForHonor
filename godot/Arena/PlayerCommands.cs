using Godot;

namespace RollForHonor.Godot.Arena;

/// <summary>Represents one discrete input intention executed during a player physics step.</summary>
internal interface IPlayerCommand
{
    /// <summary>Executes the intention through the player facade.</summary>
    /// <param name="player">The recipient of this input command.</param>
    void Execute(ArenaPlayer player);
}

internal sealed record ShootAtCursor(Vector2 ScreenPosition) : IPlayerCommand
{
    /// <inheritdoc />
    public void Execute(ArenaPlayer player)
    {
        if (player.Facing.TryGetAttackPoint(ScreenPosition, out var point)
            && new Vector2(point.X - player.GlobalPosition.X, point.Z - player.GlobalPosition.Z).Length() > 0.2f)
            player.RequestAttack(point);
    }
}
