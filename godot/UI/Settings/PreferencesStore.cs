using Godot;

namespace RollForHonor.Godot.UI.Settings;

/// <summary>Persists supported presentation preferences in user storage, separate from future run saves.</summary>
public sealed class PreferencesStore
{
    private readonly string _path;

    /// <summary>Uses an isolated preference file; tests can supply a separate user path.</summary>
    /// <param name="path">The Godot configuration path.</param>
    public PreferencesStore(string path = "user://presentation.cfg")
    {
        _path = path;
    }

    /// <summary>Reads validated settings, falling back safely when the file or individual values are invalid.</summary>
    /// <returns>Supported preferences without applying them.</returns>
    public PlayerPreferences Load()
    {
        using var config = new ConfigFile();

        if (config.Load(_path) != Error.Ok)
        {
            return PlayerPreferences.Default;
        }

        var volume = (float)Read(config, "volume", 0.8).AsDouble();

        return new PlayerPreferences(Read(config, "fullscreen", false).AsBool(), Read(config, "vsync", true).AsBool(),
            float.IsFinite(volume) ? Mathf.Clamp(volume, 0, 1) : PlayerPreferences.Default.MasterVolume,
            Read(config, "enemyHealthBars", true).AsBool(), Read(config, "playerHealthBar", false).AsBool(), Read(config, "playerManaBar", false).AsBool());
    }

    /// <summary>Saves preferences without changing the display or audio.</summary>
    /// <param name="preferences">The validated requested values.</param>
    /// <returns>Godot's configuration save result.</returns>
    public Error Save(PlayerPreferences preferences)
    {
        using var config = new ConfigFile();
        config.SetValue("presentation", "fullscreen", preferences.Fullscreen);
        config.SetValue("presentation", "vsync", preferences.VSync);
        config.SetValue("presentation", "volume", preferences.MasterVolume);

        config.SetValue("presentation", "enemyHealthBars", preferences.EnemyHealthBars);
        config.SetValue("presentation", "playerHealthBar", preferences.PlayerHealthBar);
        config.SetValue("presentation", "playerManaBar", preferences.PlayerManaBar);

        return config.Save(_path);
    }

    /// <summary>Saves preferences before applying them; an unsuccessful save leaves the runtime unchanged.</summary>
    /// <param name="preferences">The validated requested values.</param>
    /// <returns>Godot's save result; Ok means both persistence and runtime application completed.</returns>
    public Error SaveAndApply(PlayerPreferences preferences)
    {
        var result = Save(preferences);

        if (result == Error.Ok)
        {
            Apply(preferences);
        }

        return result;
    }

    /// <summary>Applies supported preferences; a headless process only applies audio.</summary>
    /// <param name="preferences">The validated settings.</param>
    public static void Apply(PlayerPreferences preferences)
    {
        if (DisplayServer.GetName() != "headless")
        {
            DisplayServer.WindowSetMode(preferences.Fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
            DisplayServer.WindowSetVsyncMode(preferences.VSync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
        }

        AudioServer.SetBusVolumeDb(0, preferences.MasterVolume == 0 ? -80 : Mathf.LinearToDb(preferences.MasterVolume));
        AudioServer.SetBusMute(0, preferences.MasterVolume == 0);
    }

    private static Variant Read(ConfigFile config, string key, Variant fallback)
    {
        var value = config.GetValue("presentation", key, fallback);

        return value.VariantType == fallback.VariantType ? value : fallback;
    }
}
