using System;
using Godot;

namespace RollForHonor.Godot.UI.Localization;

internal partial class LocaleBinding : Node
{
    internal Action RefreshText { get; set; }

    /// <inheritdoc />
    public override void _Notification(int what)
    {
        if (what == NotificationTranslationChanged)
        {
            RefreshText?.Invoke();
        }
    }
}
