using System;
using System.Linq;
using Godot;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Stats;
using RollForHonor.Godot.Arena;
using RollForHonor.Godot.Content.Items;
using RollForHonor.Godot.Gameplay.Actors;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Hud;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Admin;

/// <summary>Compact live sandbox tools for current catalogs and canonical actor stat inputs.</summary>
public partial class AdminPanel : Control
{
    /// <summary>Raised when the panel is closed.</summary>
    public event Action Closed;

    private GameBootstrap _runtime;
    private GreyboxArena _arena;
    private OptionButton _actors;
    private OptionButton _stats;
    private OptionButton _items;
    private SpinBox _value;
    private Label _resolved;
    private Label _status;
    private DynamicEntity[] _actorChoices = [];
    private StatKey[] _statChoices = [];
    private string[] _itemChoices = [];

    /// <inheritdoc />
    public override void _Ready()
    {
        var panel = UiLayout.Panel(this, "Panel", new Rect2(0, 0, 580, 440));
        UiLayout.Place(panel, UiLayout.Text(L.Text("Admin tools"), 25, HonorTheme.Brass, true), new Rect2(20, 10, 470, 42));
        UiLayout.Place(panel, UiLayout.CloseButton(() => Closed?.Invoke()), new Rect2(520, 12, 40, 40));
        var tabs = UiLayout.Place(panel, new TabContainer { Name = "Tabs" }, new Rect2(18, 64, 544, 278));
        BuildWorld(Tab(tabs, L.Text("World")));
        BuildItems(Tab(tabs, L.Text("Items")));
        BuildStats(Tab(tabs, L.Text("Stats")));
        _status = UiLayout.Place(panel, UiLayout.Text("", 15, HonorTheme.Brass), new Rect2(20, 350, 540, 70));
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        Hide();
    }

    /// <summary>Connects the current sandbox session without introducing a second source of gameplay state.</summary>
    /// <param name="runtime">The live session.</param>
    /// <param name="arena">The active map and actors.</param>
    public void Configure(GameBootstrap runtime, GreyboxArena arena)
    {
        _runtime = runtime;
        _arena = arena;
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

        var catalog = StarterItemCatalog.Create(new CombatantId(Guid.NewGuid()));
        _itemChoices = catalog
            .Select(item => item.Base.Id)
            .ToArray();
        _items.Clear();

        foreach (var item in catalog)
        {
            _items.AddItem(L.Text(item.Base.Name));
        }

        Show();
        ReadStat();
    }

    private static Control Tab(TabContainer tabs, string title)
    {
        var page = new Control { Name = title };
        tabs.AddChild(page);
        L.OnChanged(page, () => tabs.SetTabTitle(page.GetIndex(), L.Refresh(title)));

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
        var note = UiLayout.Place(page, UiLayout.Text(L.Text("Gold tools will be available when the wallet is implemented. The world keeps running."), 15, HonorTheme.Muted), new Rect2(12, 128, 500, 74));
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
    }

    private void BuildItems(Control page)
    {
        _items = UiLayout.Place(page, new OptionButton { Name = "Item" }, new Rect2(12, 14, 500, 40));
        UiLayout.Place(page, UiLayout.Button(L.Text("Create item"), CreateItem), new Rect2(12, 72, 500, 42)).Name = "Create";
        var note = UiLayout.Place(page, UiLayout.Text(L.Text("Choose any item from the current catalog. A full bag leaves the created item at your feet."), 15, HonorTheme.Muted), new Rect2(12, 132, 500, 70));
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
    }

    private void BuildStats(Control page)
    {
        _actors = UiLayout.Place(page, new OptionButton { Name = "Actor" }, new Rect2(12, 10, 500, 36));
        _actors.ItemSelected += _ => ReadStat();
        _stats = UiLayout.Place(page, new OptionButton { Name = "Stat" }, new Rect2(12, 54, 500, 36));
        _statChoices = Enum.GetValues<StatType>()
            .Where(type => type is not (StatType.Resistance or StatType.AddedDamage))
            .Select(type => new StatKey(type))
            .Concat(Enum.GetValues<DamageType>()
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

    private void CreateItem()
    {
        var item = StarterItemCatalog.Create(new CombatantId(Guid.NewGuid()))
            .First(value => value.Base.Id == _itemChoices[_items.Selected]);
        var hud = _arena.GetNode<ArenaHud>("Presentation");
        var acquired = hud.AcquireItem(item, recordAction: false) == RollForHonor.Application.Items.ItemAcquisitionStatus.Acquired;

        if (!acquired)
        {
            _arena.PlaceAdminItem(item);
        }

        hud.RecordAction(L.Format($"Admin: created {L.Text(item.Base.Name)}."));
        SetStatus(acquired ? L.Text("Item created in inventory.") : L.Text("Item created on the ground."));
    }

    private void SetStatus(string message) => L.SetText(_status, message);
}
