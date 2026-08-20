#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Beep.ECS.UI;
using Godot;
using OilfieldDays.App;
using OilfieldDays.Host;

namespace OilfieldDays.Screens;

/// <summary>
/// Load Game — the menu entry that gap G-10 kept greyed out.
///
/// <para>Each row is a slot: the company, the month it was saved on, its cash and
/// well count, and the world it was drawn from. All of it comes from the sidecar
/// beside the save rather than from opening the payload, so listing a hundred
/// slots costs a hundred small JSON reads and no engine builds.</para>
///
/// <para><b>A failed load names every reason.</b> The container is validated
/// before it is opened, and <c>SaveGame.Read</c>'s refusals are shown in full —
/// §9.1 again, and the same rule that governs a refused New Game.</para>
/// </summary>
public sealed partial class LoadGame : Control
{
    private VBoxContainer _list = null!;
    private Label _problem = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var ground = new ColorRect { Color = KitTheme.Void };
        ground.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(ground);

        Container inset = SlateChrome.Frame(new Vector2(900, 720), "LOAD GAME", UiSurface.Role.Info);
        Control panel = SlateChrome.PanelOf(inset);
        panel.SetAnchorsPreset(LayoutPreset.Center);
        panel.Position = new Vector2(-450, -360);
        AddChild(panel);

        var page = new VBoxContainer();
        page.AddThemeConstantOverride("separation", 10);
        inset.AddChild(page);

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(860, 540),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };

        page.AddChild(scroll);

        _list = new VBoxContainer { CustomMinimumSize = new Vector2(840, 0) };
        _list.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_list);

        _problem = SlateChrome.Caption(string.Empty);
        _problem.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _problem.CustomMinimumSize = new Vector2(840, 0);
        page.AddChild(_problem);

        Button back = SlateChrome.Chunk("BACK", UiSurface.Role.Danger, new Vector2(220, 50));
        back.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        back.Pressed += () => SceneRouter.Instance.Go(SceneRouter.MainMenu);
        page.AddChild(back);

        Refresh();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(World.GameInput.Cancel))
        {
            SceneRouter.Instance.Go(SceneRouter.MainMenu);
            GetViewport().SetInputAsHandled();
        }
    }

    private void Refresh()
    {
        foreach (Node child in _list.GetChildren())
        {
            _list.RemoveChild(child);
            child.QueueFree();
        }

        IReadOnlyList<SaveSlots.Slot> slots = SaveSlots.All();

        if (slots.Count == 0)
        {
            _list.AddChild(SlateChrome.Caption(
                "No saved games. A run saves from the pause menu while it is playing."));

            return;
        }

        for (int i = 0; i < slots.Count; i++)
            _list.AddChild(Row(slots[i]));
    }

    private Control Row(SaveSlots.Slot slot)
    {
        Container inset = SlateChrome.Frame(new Vector2(830, 0));
        Control panel = SlateChrome.PanelOf(inset);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        inset.AddChild(row);

        row.AddChild(SlateChrome.Icon("crude-oil-storage-tank", 40.0f));

        var lines = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        lines.AddThemeConstantOverride("separation", 2);

        lines.AddChild(SlateChrome.Line(
            $"{slot.Company}   -   month {slot.Tick.ToString(CultureInfo.InvariantCulture)}",
            18,
            KitTheme.Amber));

        lines.AddChild(SlateChrome.Line(
            $"${slot.Cash / 1e6:N1}M   -   {slot.Wells} well{(slot.Wells == 1 ? "" : "s")}   -   " +
            $"{slot.Draft.Cells} km basin   -   seed {slot.Draft.Seed.ToString(CultureInfo.InvariantCulture)}",
            14,
            KitTheme.Muted));

        row.AddChild(lines);

        Button open = SlateChrome.Chunk("LOAD", UiSurface.Role.Success, new Vector2(140, 46), fontSize: 15);
        open.Pressed += () => Open(slot);
        row.AddChild(open);

        Button drop = SlateChrome.Chunk("DELETE", UiSurface.Role.Danger, new Vector2(140, 46), fontSize: 15);
        drop.Pressed += () =>
        {
            SaveSlots.Delete(slot);
            Refresh();
        };

        row.AddChild(drop);

        return panel;
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
}
