using System.Linq;
using System;
using Godot;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Stats;
using RollForHonor.Godot.Arena;
using RollForHonor.Godot.Content.Loot;
using RollForHonor.Godot.Gameplay.Actors;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Hud;
using RollForHonor.Godot.UI.Inventory;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Admin;

/// <summary>Compact live sandbox tools for current catalogs and canonical actor stat inputs.</summary>
public partial class AdminPanel : Control
{
    /// <summary>Shared width of the expanded item editor panel.</summary>
    public const int PanelWidth = 740;

    /// <summary>Raised when the panel is closed.</summary>
    public event Action Closed;

    private GameBootstrap _runtime;
    private GreyboxArena _arena;
    private Control _inventoryWindow;
    private OptionButton _actors;
    private OptionButton _stats;
    private AdminItemEditor _creator;
    private AdminItemEditor _editor;
    private SpinBox _value;
    private Label _resolved;
    private Label _status;
    private DynamicEntity[] _actorChoices = [];
    private StatKey[] _statChoices = [];

    /// <inheritdoc />
    public override void _Ready()
    {
        var panel = UiLayout.Panel(this, "Panel", new Rect2(0, 0, PanelWidth, 740));
        UiLayout.Place(panel, UiLayout.Text(L.Text("Admin tools"), 25, HonorTheme.Brass, true), new Rect2(20, 10, 470, 42));
        UiLayout.Place(panel, UiLayout.CloseButton(() => Closed?.Invoke()), new Rect2(680, 12, 40, 40));
        var tabs = UiLayout.Place(panel, new TabContainer { Name = "Tabs" }, new Rect2(18, 64, 704, 592));
        BuildWorld(Tab(tabs, "World"));
        BuildItems(Tab(tabs, "Items"));
        BuildEditor(Tab(tabs, "Edit item"));
        BuildStats(Tab(tabs, "Stats"));
        _status = UiLayout.Place(panel, UiLayout.Text("", 15, HonorTheme.Brass), new Rect2(20, 668, 700, 56));
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        Hide();
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        if (Visible && _runtime is not null)
        {
            ArrangePanel();
        }
    }

    /// <summary>Connects the current sandbox session without introducing a second source of gameplay state.</summary>
    /// <param name="runtime">The live session.</param>
    /// <param name="arena">The active map and actors.</param>
    public void Configure(GameBootstrap runtime, GreyboxArena arena)
    {
        _runtime = runtime;
        _arena = arena;
        _inventoryWindow = arena.GetNode<ArenaHud>("Presentation").FindChild("Inventory", true, false) as Control;
        _creator.Configure(runtime, arena);
        _editor.Configure(runtime, arena);
    }

    /// <summary>Refreshes current actor/catalog choices and opens the live tools.</summary>
    public void Open()
    {
        _actorChoices = new DynamicEntity[] { _arena.Player }
            .Concat(_arena.Enemies)
            .Concat(_arena.AdminEnemies)
            .ToArray();
        _actors.Clear();

        foreach (var actor in _actorChoices)
        {
            _actors.AddItem(_arena.ActorName(actor));
        }

        _creator.Open();
        _editor.Open();

        ArrangePanel();
        Show();
        ReadStat();
    }

    private void ArrangePanel()
    {
        var parent = GetParent<Control>();
        var bagWidth = _inventoryWindow?.IsVisibleInTree() == true ? 560f : 0;
        var available = Mathf.Max(320, parent.Size.X - bagWidth - 36);
        var scale = Mathf.Min(1, Mathf.Min(available / PanelWidth, Mathf.Max(320, parent.Size.Y - 48) / 740));
        Scale = Vector2.One * scale;
        Position = new Vector2(bagWidth > 0 ? Mathf.Max(12, parent.Size.X - bagWidth - PanelWidth * scale - 24)
            : Mathf.Max(12, (parent.Size.X - PanelWidth * scale) / 2),
            Mathf.Min(160, Mathf.Max(12, parent.Size.Y - 740 * scale - 24)));
    }

    private static Control Tab(TabContainer tabs, string title)
    {
        var page = new Control { Name = title };
        tabs.AddChild(page);
        L.OnChanged(page, () => tabs.SetTabTitle(page.GetIndex(), L.Text(title)));

        return page;
    }

    private void BuildWorld(Control page)
    {
        UiLayout.Place(page, UiLayout.Text(L.Text("Monster"), 17), new Rect2(12, 14, 160, 32));
        var monsters = UiLayout.Place(page, new OptionButton { Name = "Monster" }, new Rect2(180, 14, 330, 36));
        monsters.AddItem(L.Text("Sentinel"));
        UiLayout.Place(page, UiLayout.Button(L.Text("Spawn monster"), () =>
        {
            SetStatus(_arena.QueueAdminEnemy() ? L.Text("Spawn queued. Sandbox enemies do not count toward the round.") : L.Text("Cannot spawn after player defeat."));
        }), new Rect2(180, 64, 330, 42)).Name = "Spawn";
        var note = UiLayout.Place(page, UiLayout.Text(L.Text("Sandbox changes apply immediately. The world keeps running."), 15, HonorTheme.Muted), new Rect2(12, 128, 500, 74));
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
    }

    private void BuildItems(Control page)
    {
        _creator = UiLayout.Place(page, new AdminItemEditor { Name = "ItemBuilder" }, new Rect2(12, 10, 680, 560));
        _creator.StatusChanged += SetStatus;
    }

    private void BuildEditor(Control page)
    {
        _editor = UiLayout.Place(page, new AdminItemEditor { Name = "ItemEditor", EditExisting = true }, new Rect2(12, 10, 680, 560));
        _editor.StatusChanged += SetStatus;
    }

    private void BuildStats(Control page)
    {
        _actors = UiLayout.Place(page, new OptionButton { Name = "Actor" }, new Rect2(12, 10, 500, 36));
        _actors.ItemSelected += _ => ReadStat();
        _stats = UiLayout.Place(page, new OptionButton { Name = "Stat" }, new Rect2(12, 54, 500, 36));
        _statChoices = Enum.GetValues<StatType>()
            .Where(type => type is not (StatType.Resistance or StatType.AddedDamage or StatType.Constitution) && DamageDiceStats.Sides(type) == 0)
            .Select(type => new StatKey(type))
            .Concat(DamageTypes.HitTypes
                .SelectMany(type => new[] { new StatKey(StatType.Resistance, type), new StatKey(StatType.AddedDamage, type), new StatKey(StatType.DamageMultiplier, type) }))
            .ToArray();

        foreach (var key in _statChoices)
        {
            _stats.AddItem(CombatGlossary.Stat(key).Title);
        }

        _stats.ItemSelected += _ => ReadStat();
        UiLayout.Place(page, UiLayout.Text(L.Text("Base value"), 17), new Rect2(12, 104, 160, 36));
        _value = UiLayout.Place(page, new SpinBox { Name = "Value", MinValue = -1000000, MaxValue = 1000000, Step = 0.01, AllowGreater = false }, new Rect2(180, 104, 332, 36));
        _resolved = UiLayout.Place(page, UiLayout.Text("", 15, HonorTheme.Muted), new Rect2(12, 148, 500, 28));
        UiLayout.Place(page, UiLayout.Button(L.Text("Apply stat"), ApplyStat), new Rect2(12, 186, 500, 40)).Name = "Apply";
    }

    private void ReadStat()
    {
        if (_runtime is null || _actorChoices.Length == 0 || _stats.Selected < 0)
        {
            return;
        }

        var actor = _actorChoices[Math.Max(0, _actors.Selected)];
        var key = _statChoices[_stats.Selected];
        _value.Value = (double)_runtime.CombatState.ReadEquipment(actor.CombatantId).NonEquipmentProfile.BaseValues.Get(key);
        L.SetText(_resolved, L.Format($"Resolved value: {_runtime.CombatState.GetStats(actor.CombatantId).Get(key):0.##}"));
    }

    private void ApplyStat()
    {
        try
        {
            var actor = _actorChoices[Math.Max(0, _actors.Selected)];
            var key = _statChoices[_stats.Selected];
            var profile = _runtime.CombatState.ReadEquipment(actor.CombatantId).NonEquipmentProfile;
            _runtime.CombatState.UpdateNonEquipmentStats(actor.CombatantId, new(profile.BaseValues.With(key, (decimal)_value.Value), profile.Modifiers));
            _arena.GetNode<ArenaHud>("Presentation").RecordAction(L.Format($"Admin: changed {CombatGlossary.Stat(key).Title} to {_value.Value:0.##}."));
            ReadStat();
            SetStatus(L.Text("Stat updated. Equipped bonuses are preserved."));
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            SetStatus(L.Text("Invalid stat value. No change was published."));
        }
    }

    private void SetStatus(string message) => L.SetText(_status, message);
}
