namespace RollForHonor.Godot.Gameplay.Actors;

/// <summary>Defines the actor-owned physics sequence, independent of scene child ordering.</summary>
public enum ActorPhysicsStage
{
    /// <summary>Observe authoritative mode changes before accepting intent.</summary>
    State,

    /// <summary>Convert controller input into world-space intent.</summary>
    Input,

    /// <summary>Consume discrete commands once.</summary>
    Commands,

    /// <summary>Apply movement and native collision.</summary>
    Movement,

    /// <summary>Run optional presentation-capability steps after movement; player cursor sampling follows the shared sequence.</summary>
    Presentation
}
