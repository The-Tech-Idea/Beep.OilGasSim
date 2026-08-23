#nullable enable

using System.Collections.Generic;
using System.Globalization;
using System;
using Beep.ECS.UI;
using Godot;
using OilfieldDays.App;
using OilfieldDays.Host;

namespace OilfieldDays.Screens;

/// <summary>
/// Load Game - the menu entry that gap G-10 kept greyed out.
///
/// <para>Each row is a slot: the company, the month it was saved on, its cash and
/// well count, and the world it was drawn from. All of it comes from the sidecar
/// beside the save rather than from opening the payload, so listing a hundred
/// slots costs a hundred small JSON reads and no engine builds.</para>
///
/// <para><b>A failed load names every reason.</b> The container is validated
/// before it is opened, and <c>SaveGame.Read</c>'s refusals are shown in full -
/// §9.1 again, and the same rule that governs a refused New Game.</para>
/// </summary>
[Tool]
public sealed partial class LoadGame : Control
{
    private VBoxContainer _list = null!;
    private Label _empty = null!;
    private Label _problem = null!;
    private PanelContainer _slotTemplate = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        StyleGround();
        BindBoard();

        if (!Godot.Engine.IsEditorHint())
            Refresh();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Godot.Engine.IsEditorHint())
            return;

        if (@event.IsActionPressed(World.GameInput.Cancel))
        {
            SceneRouter.Instance.Go(SceneRouter.MainMenu);
            GetViewport().SetInputAsHandled();
        }
    }

    private void StyleGround()
    {
        var ground = RequireNamed<ColorRect>("Ground");
        ground.Color = KitTheme.Void;
        ground.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    private void BindBoard()
    {
        var panel = RequireNamed<PanelContainer>("LoadPanel");
        panel.CustomMinimumSize = new Vector2(900, 720);
        CenterPanel(panel, 900, 720);
        panel.AddThemeStyleboxOverride("panel", SlateChrome.PanelPlate());

        var page = RequireNamed<VBoxContainer>(panel, "Page");
        page.AddThemeConstantOverride("separation", 10);

        Label title = RequireNamed<Label>(page, "Title");
        title.Text = "LOAD GAME";
        SlateChrome.PromoteHeader(title, UiSurface.Role.Warning, centered: true);

        var scroll = RequireNamed<ScrollContainer>(page, "Scroll");
        scroll.CustomMinimumSize = new Vector2(860, 540);
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;

        _list = RequireNamed<VBoxContainer>(scroll, "List");
        _list.CustomMinimumSize = new Vector2(840, 0);
        _list.AddThemeConstantOverride("separation", 6);

        _empty = RequireNamed<Label>(_list, "EmptyState");
        _empty.Text = "No saved games. A run saves from the pause menu while it is playing.";
        _empty.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _empty.AddThemeFontSizeOverride("font_size", 14);
        _empty.AddThemeColorOverride("font_color", KitTheme.Muted);

        _slotTemplate = RequireNamed<PanelContainer>(_list, "SlotTemplate");
        StyleSlotTemplate(_slotTemplate, editorVisible: Godot.Engine.IsEditorHint());

        _problem = RequireNamed<Label>(page, "Problem");
        _problem.Text = string.Empty;
        _problem.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _problem.CustomMinimumSize = new Vector2(840, 0);
        _problem.AddThemeFontSizeOverride("font_size", 14);
        _problem.AddThemeColorOverride("font_color", KitTheme.Muted);

        Button back = RequireNamed<Button>(page, "BackButton");
        SlateChrome.ApplyChunk(back, "BACK", UiSurface.Role.Danger, new Vector2(220, 50));
        back.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

        if (!Godot.Engine.IsEditorHint())
            back.Pressed += () => SceneRouter.Instance.Go(SceneRouter.MainMenu);
    }

    private void Refresh()
    {
        foreach (Node child in _list.GetChildren())
        {
            if (child == _empty || child == _slotTemplate)
                continue;

            _list.RemoveChild(child);
            child.QueueFree();
        }

        IReadOnlyList<SaveSlots.Slot> slots = SaveSlots.All();
        _empty.Visible = slots.Count == 0;
        _slotTemplate.Visible = false;

        if (slots.Count == 0)
            return;

        for (int i = 0; i < slots.Count; i++)
            _list.AddChild(Row(slots[i]));
    }

    private Control Row(SaveSlots.Slot slot)
    {
        var panel = (PanelContainer)_slotTemplate.Duplicate();
        panel.Name = "Slot";
        panel.Visible = true;
        StyleSlotTemplate(panel, editorVisible: false);

        Label title = RequireNamed<Label>(panel, "SlotTitle");
        title.Text = $"{slot.Company}   -   month {slot.Tick.ToString(CultureInfo.InvariantCulture)}";
        title.AddThemeFontSizeOverride("font_size", 18);
        title.AddThemeColorOverride("font_color", KitTheme.Amber);

        Label meta = RequireNamed<Label>(panel, "SlotMeta");
        meta.Text =
            $"${slot.Cash / 1e6:N1}M   -   {slot.Wells} well{(slot.Wells == 1 ? "" : "s")}   -   " +
            $"{slot.Draft.Cells} km basin   -   seed {slot.Draft.Seed.ToString(CultureInfo.InvariantCulture)}";
        meta.AddThemeFontSizeOverride("font_size", 14);
        meta.AddThemeColorOverride("font_color", KitTheme.Muted);

        Button open = RequireNamed<Button>(panel, "LoadButton");
        SlateChrome.ApplyChunk(open, "LOAD", UiSurface.Role.Success, new Vector2(140, 46), fontSize: 15);
        open.Pressed += () => Open(slot);

        Button drop = RequireNamed<Button>(panel, "DeleteButton");
        SlateChrome.ApplyChunk(drop, "DELETE", UiSurface.Role.Danger, new Vector2(140, 46), fontSize: 15);
        drop.Pressed += () =>
        {
            SaveSlots.Delete(slot);
            Refresh();
        };

        return panel;
    }

    private static void StyleSlotTemplate(PanelContainer panel, bool editorVisible)
    {
        panel.Visible = editorVisible;
        panel.CustomMinimumSize = new Vector2(830, 0);
        panel.AddThemeStyleboxOverride("panel", SlateChrome.PanelPlate(0));

        HBoxContainer row = RequireNamed<HBoxContainer>(panel, "SlotRow");
        row.AddThemeConstantOverride("separation", 12);

        TextureRect icon = RequireNamed<TextureRect>(row, "SlotIcon");
        icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        icon.CustomMinimumSize = new Vector2(40, 40);

        VBoxContainer lines = RequireNamed<VBoxContainer>(row, "SlotLines");
        lines.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        lines.AddThemeConstantOverride("separation", 2);

        Button open = RequireNamed<Button>(row, "LoadButton");
        SlateChrome.ApplyChunk(open, "LOAD", UiSurface.Role.Success, new Vector2(140, 46), fontSize: 15);

        Button drop = RequireNamed<Button>(row, "DeleteButton");
        SlateChrome.ApplyChunk(drop, "DELETE", UiSurface.Role.Danger, new Vector2(140, 46), fontSize: 15);
    }

    private void Open(SaveSlots.Slot slot)
    {
        if (EngineHost.Instance.Load(slot))
        {
            SceneRouter.Instance.Go(SceneRouter.Gameplay);

            return;
        }

        _problem.Text = "The save would not open:\n- " +
                        string.Join("\n- ", EngineHost.Instance.StartupProblems);

        _problem.AddThemeColorOverride("font_color", KitTheme.Red);
    }

    private static void CenterPanel(Control panel, float width, float height)
    {
        panel.AnchorLeft = 0.5f;
        panel.AnchorTop = 0.5f;
        panel.AnchorRight = 0.5f;
        panel.AnchorBottom = 0.5f;
        panel.OffsetLeft = -width / 2.0f;
        panel.OffsetTop = -height / 2.0f;
        panel.OffsetRight = width / 2.0f;
        panel.OffsetBottom = height / 2.0f;
        panel.GrowHorizontal = GrowDirection.Both;
        panel.GrowVertical = GrowDirection.Both;
    }

    private T? FindNamed<T>(string name) where T : Node => FindNamed<T>(this, name);

    private T RequireNamed<T>(string name) where T : Node =>
        FindNamed<T>(name) ?? throw new InvalidOperationException(
			$"{nameof(LoadGame)} requires a design-time {typeof(T).Name} named '{name}'.");

    private static T RequireNamed<T>(Node root, string name) where T : Node =>
        FindNamed<T>(root, name) ?? throw new InvalidOperationException(
			$"{nameof(LoadGame)} requires a design-time {typeof(T).Name} named '{name}' under {root.GetPath()}.");

    private static T? FindNamed<T>(Node root, string name) where T : Node
    {
        if (root is T self && root.Name == name)
            return self;

        foreach (Node child in root.GetChildren())
        {
            T? found = FindNamed<T>(child, name);

            if (found is not null)
                return found;
        }

        return null;
    }
}
