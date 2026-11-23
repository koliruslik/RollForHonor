using System;

namespace RollForHonor.Godot.UI.Settings;

/// <summary>Captures supported display and master-audio preferences independently of game progression.</summary>
public sealed record PlayerPreferences
{
    /// <summary>Gets the initial windowed, synchronized, 80-percent-volume settings.</summary>
    public static PlayerPreferences Default { get; } = new(false, true, 0.8f);

    /// <summary>Gets whether the game uses a borderless fullscreen window.</summary>
    public bool Fullscreen { get; }

    /// <summary>Gets whether display refresh synchronization is enabled.</summary>
    public bool VSync { get; }

    /// <summary>Gets linear master volume between zero and one.</summary>
    public float MasterVolume { get; }

    /// <summary>Gets whether living enemies display overhead health bars.</summary>
    public bool EnemyHealthBars { get; }

    /// <summary>Gets whether the player displays an overhead health bar.</summary>
    public bool PlayerHealthBar { get; }

    /// <summary>Gets whether the player displays an overhead mana bar.</summary>
    public bool PlayerManaBar { get; }

    /// <summary>Creates validated preferences without changing the display or audio.</summary>
    /// <param name="fullscreen">Whether fullscreen is requested.</param>
    /// <param name="vSync">Whether display synchronization is requested.</param>
    /// <param name="masterVolume">A finite linear volume in [0,1].</param>
    /// <param name="enemyHealthBars">Show enemy health bars.</param>
    /// <param name="playerHealthBar">Show player health.</param>
    /// <param name="playerManaBar">Show player mana.</param>
    /// <exception cref="ArgumentOutOfRangeException">The volume is nonfinite or outside [0,1].</exception>
    public PlayerPreferences(bool fullscreen, bool vSync, float masterVolume, bool enemyHealthBars = true, bool playerHealthBar = false, bool playerManaBar = false)
    {
        if (!float.IsFinite(masterVolume) || masterVolume is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(masterVolume));
        }

        Fullscreen = fullscreen;
        VSync = vSync;
        MasterVolume = masterVolume;
        EnemyHealthBars = enemyHealthBars;
        PlayerHealthBar = playerHealthBar;
        PlayerManaBar = playerManaBar;
    }
}
