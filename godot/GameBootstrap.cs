using System;
using System.Reflection;
using Chickensoft.GoDotTest;
using Godot;
using RollForHonor.Godot.Gameplay.Input;
using RollForHonor.Application.Combat.Services;
using RollForHonor.Domain.Combat.Damage.Configuration;
using RollForHonor.Domain.Combat.Damage.Services;
using RollForHonor.Domain.Combat.Resolution.Services;
using RollForHonor.Domain.Combat.Resolution.Services.Steps;
using RollForHonor.Domain.Resources.Services;
using RollForHonor.Godot.Diagnostics;
using RollForHonor.Infrastructure.Combat;
using RollForHonor.Infrastructure.Diagnostics;
using RollForHonor.Infrastructure.Dice;

namespace RollForHonor.Godot;

/// <summary>Owns one combat session and manually composes its runtime dependencies before child nodes enter the tree.</summary>
/// <remarks>Configure exported diagnostic settings before tree entry. Runtime adapters are per bootstrap, not global singletons.</remarks>
public partial class GameBootstrap : Node3D
{
    private AttackInitiationService _attacks;
    private PeriodicEffectService _ticks;
    private TimeSpan _gameTime;
#if DEBUG
    private TestEnvironment _testEnvironment;
#endif

    /// <summary>Gets the session-local in-memory adapter; participants must be registered explicitly.</summary>
    public InMemoryCombatState CombatState { get; } = new();
    /// <summary>Gets the shared launch service after this bootstrap enters the tree.</summary>
    /// <exception cref="InvalidOperationException">The bootstrap has not entered the tree.</exception>
    public AttackInitiationService Attacks => _attacks ?? throw new InvalidOperationException("Combat runtime is not initialized.");
    /// <summary>Gets the periodic use case after bootstrap enters the tree; scheduling remains with the caller.</summary>
    /// <exception cref="InvalidOperationException">The bootstrap has not entered the tree.</exception>
    public PeriodicEffectService Ticks => _ticks ?? throw new InvalidOperationException("Combat runtime is not initialized.");
    /// <summary>Gets accumulated game time; engine delta already accounts for time scale and paused trees do not advance it.</summary>
    public TimeSpan GameTime => _gameTime;
    /// <summary>Gets or sets whether runtime combat reports are enabled when the session is composed.</summary>
    [Export]
    public bool CombatDiagnosticsEnabled { get; set; }
    /// <summary>Gets or sets the runtime diagnostic representation selected before tree entry.</summary>
    [Export]
    public CombatDiagnosticView DiagnosticView { get; set; } = CombatDiagnosticView.Summary;
    /// <summary>Gets or sets which scene this fresh session hosts; the workbench has a separate bootstrap preset.</summary>
    [Export(PropertyHint.File, "*.tscn")]
    public string StartupScenePath { get; set; } = "res://Arena.tscn";

    /// <inheritdoc />
    public override void _EnterTree()
    {
        UI.Settings.PreferencesStore.InitializeLanguage();
        GameInputBindings.EnsureDefaults();

        if (_attacks is not null)
        {
            return;
        }
        var diagnostics = CombatDiagnosticsEnabled
            ? new LoggerCombatDiagnostics(new GodotCombatLogger(), DiagnosticView) : null;
        var impacts = new CombatImpactService(CombatState, new CombatSnapshotBuilder(),
            new BasicAttackPipelineFactory(new DamageMultipliers(0, 0.5m, 1, 2)),
            () => new RandomDiceRoller(), diagnostics);
        _attacks = new AttackInitiationService(CombatState,
            new ResourceCostResolver(new AdditiveResourceCostPolicy()), new NoResourceCostModifiers(),
            impacts, () => _gameTime, diagnostics);
        _ticks = new PeriodicEffectService(CombatState,
            new CombatPipeline([new RawDamageStep(new RawDamageResolver()), new SourceDamageStep(),
                new DefenseStep(new DefenseResolver(new HitMagnitudeArmorFormula()))]), () => new RandomDiceRoller());
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        if (GetTree().Paused) return;
        if (!double.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        _gameTime += TimeSpan.FromSeconds(delta);
    }

    /// <inheritdoc />
    public override void _Ready()
    {
#if DEBUG
        _testEnvironment = TestEnvironment.From(OS.GetCmdlineArgs());
        if (_testEnvironment.ShouldRunTests)
        {
            CallDeferred(MethodName.RunTests);
            return;
        }
#endif
        if (ResourceLoader.Exists(StartupScenePath))
            AddChild(GD.Load<PackedScene>(StartupScenePath).Instantiate());
    }

#if DEBUG
    private void RunTests()
    {
        _ = GoTest.RunTests(Assembly.GetExecutingAssembly(), this, _testEnvironment);
    }
#endif
}
