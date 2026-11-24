using System;
using System.Collections.Generic;
using Godot;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Reference;
using RollForHonor.Godot.UI.Settings;

namespace RollForHonor.Godot.UI.Hud;

internal sealed class HudNavigationController
{
    private readonly SceneTree _tree;
    private readonly Panel _pause;
    private readonly ColorRect _shade;
    private readonly SettingsPanel _settings;
    private readonly ReferencePanel _reference;
    private readonly InspectionStack _inspector;
    private readonly Action _prepareReference;
    private readonly Func<IReadOnlyList<ReferenceEntry>> _captureReference;
    private readonly Action _changed;
    private readonly Action _reloadPreferences;
    private bool _ownsPause;
    private bool _previousPause;
    private bool _referenceReturnToPause;

    internal HudNavigationController(SceneTree tree, Panel pause, ColorRect shade, SettingsPanel settings,
        ReferencePanel reference, InspectionStack inspector, Action prepareReference,
        Func<IReadOnlyList<ReferenceEntry>> captureReference, Action changed, Action reloadPreferences)
    {
        _tree = tree;
        _pause = pause;
        _shade = shade;
        _settings = settings;
        _reference = reference;
        _inspector = inspector;
        _prepareReference = prepareReference;
        _captureReference = captureReference;
        _changed = changed;
        _reloadPreferences = reloadPreferences;
        settings.Closed += ReturnToPause;
        reference.Closed += CloseReference;
    }

    internal void TogglePause()
    {
        _pause.Visible = !_pause.Visible;
        Refresh();
    }

    internal void Resume()
    {
        _pause.Hide();
        Refresh();
    }

    internal void OpenSettings()
    {
        _pause.Hide();
        _settings.Open();
        Refresh();
    }

    internal void ToggleReference()
    {
        if (_settings.Visible)
        {
            return;
        }

        if (_reference.Visible)
        {
            _reference.Close();
            return;
        }

        _referenceReturnToPause = _pause.Visible;
        _pause.Hide();
        _prepareReference();
        _reference.Open(_captureReference());
        Refresh();
    }

    internal void Refresh()
    {
        _shade.Visible = _pause.Visible || _reference.Visible;
        _inspector.Visible = _inspector.Count > 0 && !_pause.Visible && !_settings.Visible;
        var active = _pause.Visible || _settings.Visible || _reference.Visible;

        if (active && !_ownsPause)
        {
            _previousPause = _tree.Paused;
            _ownsPause = true;
            _tree.Paused = true;
        }
        else if (!active)
        {
            ReleasePause();
        }

        _changed();
    }

    internal void Navigate(string path)
    {
        _tree.Paused = false;
        _ownsPause = false;
        _tree.ChangeSceneToFile(path);
    }

    internal void Dispose()
    {
        _settings.Closed -= ReturnToPause;
        _reference.Closed -= CloseReference;
        ReleasePause();
    }

    private void ReturnToPause()
    {
        _pause.Show();
        _reloadPreferences();
        Refresh();
    }

    private void CloseReference()
    {
        _inspector.Clear();
        _pause.Visible = _referenceReturnToPause;
        Refresh();
    }

    private void ReleasePause()
    {
        if (!_ownsPause)
        {
            return;
        }

        _tree.Paused = _previousPause;
        _ownsPause = false;
    }
}
