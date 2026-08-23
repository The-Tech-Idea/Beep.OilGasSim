#nullable enable

using System;
using Beep.ECS.UI;
using Beep.ECS.UI.Kit;
using Godot;
using OilfieldDays.App;
using OilfieldDays.Host;

namespace OilfieldDays.Screens;

/// <summary>
/// The title screen, built to the supplied main-menu mockup: the mark top-left
/// over the key art, the vertical stack of entries beneath it, field notes along
/// the bottom-left, and the build stamp bottom-right.
///
/// <para>It touches no engine. Plan 08 is explicit that the menu's only
/// engine-facing decision is <em>whether</em> to build one, and that decision is
/// taken on the next screen.</para>
///
/// <para><b>The mockup's company card is not drawn, and that is the honest
/// reading of it.</b> It shows a name, a reputation of 50 and $25,400 - a
/// carried-over company. There is no save yet (gap G-10), so there is no company
/// to carry, and reputation has no engine owner at all (gap G-04). A card filled
/// with plausible numbers would be the one thing the whole information model
/// exists to prevent. It appears the day a save does.</para>
/// </summary>
[Tool]
public sealed partial class MainMenu : Control
{
    private sealed record Entry(string Text, string Glyph, bool Ready, string Note, Action? Go);

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildBackdrop();
        BuildMark();
        BuildEntries();
        BuildNotes();
        BuildStamp();
    }

    private void BuildBackdrop()
    {
        var ground = RequireNamed<ColorRect>("Ground");
        ground.Color = KitTheme.Void;
        ground.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var art = RequireNamed<TextureRect>("Art");
        art.Texture ??= GD.Load<Texture2D>(SlateChrome.SplashPath);
        art.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        art.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
        art.MouseFilter = MouseFilterEnum.Ignore;
        art.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var ramp = new Gradient();
        ramp.SetColor(0, new Color(0.02f, 0.05f, 0.08f, 0.82f));
        ramp.SetColor(1, new Color(0.02f, 0.05f, 0.08f, 0.0f));

        var shade = RequireNamed<TextureRect>("Shade");
        shade.Texture = new GradientTexture2D
        {
            Gradient = ramp,
            Width = 256,
            Height = 4,
            FillFrom = Vector2.Zero,
            FillTo = Vector2.Right,
        };
        shade.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        shade.StretchMode = TextureRect.StretchModeEnum.Scale;
        shade.MouseFilter = MouseFilterEnum.Ignore;
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.LeftWide);
        shade.OffsetRight = 620;

    }

    private void BuildMark()
    {
        var mark = RequireNamed<TextureRect>("Mark");
        mark.Texture ??= GD.Load<Texture2D>(SlateChrome.LogoPath);
        mark.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        mark.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        mark.CustomMinimumSize = new Vector2(250, 250);
        mark.Position = new Vector2(44, 12);
        mark.Size = new Vector2(250, 250);
        mark.MouseFilter = MouseFilterEnum.Ignore;

    }

    private void BuildEntries()
    {
        var column = RequireNamed<VBoxContainer>("Entries");
        column.Position = new Vector2(64, 268);
        column.CustomMinimumSize = new Vector2(400, 0);
        column.AddThemeConstantOverride("separation", 8);

        SaveSlots.Slot? newest = Godot.Engine.IsEditorHint() ? null : SaveSlots.Newest();

        Entry[] entries =
        {
            new("NEW GAME", "▶", true, string.Empty,
                () => SceneRouter.Instance.Go(SceneRouter.NewGame)),

            new("CONTINUE", "⏵", newest is not null,
                newest is not null
                    ? $"{newest.Company}, month {newest.Tick}"
                    : "nothing saved yet",
                newest is not null ? () => Resume(newest) : null),

            new("LOAD GAME", "⭯", newest is not null,
                newest is not null ? "every saved run" : "nothing saved yet",
                newest is not null ? () => SceneRouter.Instance.Go(SceneRouter.LoadGame) : null),

            new("CHALLENGES", "★", false,
                "one scenario is composed, and New Game starts it", null),

            new("OPTIONS", "⚙", true, string.Empty, () => SceneRouter.Instance.Go(SceneRouter.Options)),

            new("EXIT GAME", "⏻", true, string.Empty, () => GetTree().Quit()),
        };

        string[] names =
        {
            "NewGameButton",
            "ContinueButton",
            "LoadButton",
            "ChallengesButton",
            "OptionsButton",
            "ExitButton",
        };

        for (int i = 0; i < entries.Length; i++)
        {
            Button button = RequireNamed<Button>(column, names[i]);
            ApplyItem(button, entries[i]);
        }
    }

    private static void ApplyItem(Button button, Entry entry)
    {
        SlateChrome.ApplyChunk(
            button,
            $"   {entry.Glyph}    {entry.Text}",
            entry.Text == "NEW GAME" ? UiSurface.Role.Success : UiSurface.Role.Neutral,
            new Vector2(400, 50),
            fontSize: 20);

        button.Alignment = HorizontalAlignment.Left;
        button.Disabled = !entry.Ready;
        button.TooltipText = entry.Note;

        if (!Godot.Engine.IsEditorHint() && entry.Go is not null)
            button.Pressed += entry.Go;
    }

    private void Resume(SaveSlots.Slot slot)
    {
        if (EngineHost.Instance.Load(slot))
        {
            SceneRouter.Instance.Go(SceneRouter.Gameplay);

            return;
        }

        SceneRouter.Instance.Go(SceneRouter.LoadGame);
    }

    private void BuildNotes()
    {
        var panel = RequireNamed<PanelContainer>("NotesPanel");
        panel.Position = new Vector2(64, 632);
        panel.CustomMinimumSize = new Vector2(400, 250);
        panel.AddThemeStyleboxOverride("panel", SlateChrome.PanelPlate());

        var content = RequireNamed<VBoxContainer>(panel, "Content");
        content.AddThemeConstantOverride("separation", 8);

        var header = RequireNamed<Label>(content, "Header");
        header.Text = "FIELD NOTES";
        SlateChrome.PromoteHeader(header, UiSurface.Role.Warning, centered: true);

        var notes = RequireNamed<VBoxContainer>(content, "Notes");
        notes.AddThemeConstantOverride("separation", 6);

        Note(notes, "NoteRun", "The run", KitTheme.Green,
            "One basin. Ten years. $600M, or broke.");

        Note(notes, "NoteEngine", "The engine", KitTheme.Sky,
            "OGSim decides it all. A tick is a month; a month is thirty days.");

        Note(notes, "NoteRock", "The rock", KitTheme.Amber,
            "You know nothing until you measure. Survey, log, core, or drill.");
    }

    private static void Note(Container parent, string rowName, string tag, Color colour, string text)
    {
        var row = RequireNamed<HBoxContainer>(parent, rowName);
        row.AddThemeConstantOverride("separation", 8);

        Label label = RequireNamed<Label>(row, "Tag");
        label.Text = tag + ":";
        label.AddThemeFontSizeOverride("font_size", 15);
        label.AddThemeColorOverride("font_color", colour);
        label.CustomMinimumSize = new Vector2(96, 0);

        Label body = RequireNamed<Label>(row, "Body");
        body.Text = text;
        body.AddThemeFontSizeOverride("font_size", 15);
        body.AddThemeColorOverride("font_color", KitTheme.Muted);
        body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.CustomMinimumSize = new Vector2(250, 0);
        body.SizeFlagsHorizontal = SizeFlags.ExpandFill;

    }

    private void BuildStamp()
    {
        var stamp = RequireNamed<VBoxContainer>("Stamp");
        stamp.AnchorLeft = 1.0f;
        stamp.AnchorTop = 1.0f;
        stamp.AnchorRight = 1.0f;
        stamp.AnchorBottom = 1.0f;
        stamp.OffsetLeft = -300.0f;
        stamp.OffsetTop = -70.0f;
        stamp.OffsetRight = -20.0f;
        stamp.OffsetBottom = -20.0f;
        stamp.GrowHorizontal = GrowDirection.Begin;
        stamp.GrowVertical = GrowDirection.Begin;
        stamp.Alignment = BoxContainer.AlignmentMode.End;

        Label title = RequireNamed<Label>(stamp, "StampTitle");
        Right(title, "OILFIELD DAYS", 15, KitTheme.Amber);

        Label sub = RequireNamed<Label>(stamp, "StampSub");
        Right(sub, "powered by OGSim", 13, KitTheme.Muted);
    }

    private static void Right(Label label, string text, int size, Color colour)
    {
        label.Text = text;
        label.HorizontalAlignment = HorizontalAlignment.Right;
        label.CustomMinimumSize = new Vector2(280, 0);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", colour);
    }

    private T? FindNamed<T>(string name) where T : Node => FindNamed<T>(this, name);

    private T RequireNamed<T>(string name) where T : Node =>
        FindNamed<T>(name) ?? throw new InvalidOperationException(
            $"{nameof(MainMenu)} requires a design-time {typeof(T).Name} named '{name}'.");

    private static T RequireNamed<T>(Node root, string name) where T : Node =>
        FindNamed<T>(root, name) ?? throw new InvalidOperationException(
            $"{nameof(MainMenu)} requires a design-time {typeof(T).Name} named '{name}' under {root.GetPath()}.");

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
