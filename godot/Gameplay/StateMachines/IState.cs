namespace RollForHonor.Godot.Gameplay.StateMachines;

/// <summary>Defines lifecycle callbacks for one mutually exclusive behavior mode.</summary>
public interface IState
{
    /// <summary>Activates this mode after the previous mode has exited.</summary>
    void Enter();

    /// <summary>Releases this mode before another mode is activated.</summary>
    void Exit();
}
