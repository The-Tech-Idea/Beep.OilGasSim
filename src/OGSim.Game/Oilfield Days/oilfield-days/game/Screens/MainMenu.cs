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
/// reading of it.</b> It shows a name, a reputation of 50 and $25,400 — a
/// carried-over company. There is no save yet (gap G-10), so there is no company
/// to carry, and reputation has no engine owner at all (gap G-04). A card filled
/// with plausible numbers would be the one thing the whole information model
/// exists to prevent. It appears the day a save does.</para>
/// </summary>
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
        var ground = new ColorRect { Color = KitTheme.Void };
        ground.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(ground);

        var art = new TextureRect
        {
            Texture = GD.Load<Texture2D>(SlateChrome.SplashPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
        };

        art.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(art);

        // The art carries the logo in its middle; the panels sit over the left
        // third, so that side is darkened to keep the type readable. A gradient
        // rather than a rectangle, because a hard vertical edge across a painted
        // landscape reads as a seam in the art.
        var ramp = new Gradient();
        ramp.SetColor(0, new Color(0.02f, 0.05f, 0.08f, 0.82f));
        ramp.SetColor(1, new Color(0.02f, 0.05f, 0.08f, 0.0f));

        var shade = new TextureRect
        {
            Texture = new GradientTexture2D
            {
                Gradient = ramp,
                Width = 256,
                Height = 4,
                FillFrom = Vector2.Zero,
                FillTo = Vector2.Right,
            },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
        };

        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.LeftWide);
        shade.OffsetRight = 620;
        AddChild(shade);
    }

    private void BuildMark()
    {
        var mark = new TextureRect
        {
            Texture = GD.Load<Texture2D>(SlateChrome.LogoPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(250, 250),
            Position = new Vector2(44, 12),
            Size = new Vector2(250, 250),
            MouseFilter = MouseFilterEnum.Ignore,
        };

        AddChild(mark);
    }

    private void BuildEntries()
    {
        var column = new VBoxContainer
        {
            Position = new Vector2(64, 268),
            CustomMinimumSize = new Vector2(400, 0),
        };

        column.AddThemeConstantOverride("separation", 8);
        AddChild(column);

        // Continue is the newest slot, which is what a player nearly always
        // means by it; Load Game is the list. Both are dead until something has
        // been saved, and say so rather than opening an empty screen.
        SaveSlots.Slot? newest = SaveSlots.Newest();

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

        foreach (Entry entry in entries)
            column.AddChild(Item(entry));
    }

    private static Control Item(Entry entry)
    {
        // The kit's button, accented by role: the one that starts a game reads
        // as success, the rest as neutral. Nothing here names a colour.
        Button button = SlateChrome.Chunk(
            $"   {entry.Glyph}    {entry.Text}",
            entry.Text == "NEW GAME" ? UiSurface.Role.Success : UiSurface.Role.Neutral,
            new Vector2(400, 50),
            fontSize: 20);

        button.Alignment = HorizontalAlignment.Left;
        button.Disabled = !entry.Ready;
        button.TooltipText = entry.Note;

        if (entry.Go is not null)
            button.Pressed += entry.Go;

        return button;
    }

    /// <summary>
    /// Open the newest save, or say why it would not open.
    /// </summary>
    /// <remarks>
    /// A refusal goes to the field-notes panel rather than nowhere. The menu has
    /// no other place to speak, and a Continue that did nothing when pressed
    /// would be indistinguishable from a broken button.
    /// </remarks>
    private void Resume(SaveSlots.Slot slot)
    {
        if (EngineHost.Instance.Load(slot))
        {
            SceneRouter.Instance.Go(SceneRouter.Gameplay);

            return;
        }

        SceneRouter.Instance.Go(SceneRouter.LoadGame);
    }

    /// <summary>
    /// The mockup's NEWS &amp; UPDATES panel, carrying things that are true.
    /// </summary>
    /// <remarks>
    /// The mockup's lines are a market report, a technology unlock and a season
    /// event — a live feed from a running company. There is no company here and
    /// no feed to read, so rather than write three plausible sentences the panel
    /// says what this build actually is and what the run ahead actually asks. A
    /// menu that invented a market report would be teaching the player to trust
    /// numbers nothing computed.
    /// </remarks>
    private void BuildNotes()
    {
        Container inset = SlateChrome.Frame(new Vector2(400, 250), "FIELD NOTES", UiSurface.Role.Warning);
        Control panel = SlateChrome.PanelOf(inset);
        panel.Position = new Vector2(64, 632);
        AddChild(panel);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 6);
        inset.AddChild(column);

        Note(column, "The run", KitTheme.Green,
            "One basin. Ten years. $600M, or broke.");

        Note(column, "The engine", KitTheme.Sky,
            "OGSim decides it all. A tick is a month; a month is thirty days.");

        Note(column, "The rock", KitTheme.Amber,
            "You know nothing until you measure. Survey, log, core, or drill.");
    }

    private static void Note(Container parent, string tag, Color colour, string text)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        Label label = SlateChrome.Line(tag + ":", 15, colour);
        label.CustomMinimumSize = new Vector2(96, 0);
        row.AddChild(label);

        Label body = SlateChrome.Line(text, 15, KitTheme.Muted);
        body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.CustomMinimumSize = new Vector2(250, 0);
        body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(body);

        parent.AddChild(row);
    }

    private void BuildStamp()
    {
        var stamp = new VBoxContainer();
        stamp.SetAnchorsPreset(LayoutPreset.BottomRight);
        stamp.Position = new Vector2(-300, -70);
        stamp.Alignment = BoxContainer.AlignmentMode.End;
        AddChild(stamp);

        stamp.AddChild(Right(SlateChrome.Line("OILFIELD DAYS", 15, KitTheme.Amber)));
        stamp.AddChild(Right(SlateChrome.Line("powered by OGSim", 13, KitTheme.Muted)));
    }

    private static Control Right(Label label)
    {
        label.HorizontalAlignment = HorizontalAlignment.Right;
        label.CustomMinimumSize = new Vector2(280, 0);

        return label;
    }
}
