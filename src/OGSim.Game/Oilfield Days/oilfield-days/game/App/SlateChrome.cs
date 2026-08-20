#nullable enable

using System;
using System.Collections.Generic;
using Beep.ECS.UI;
using Beep.ECS.UI.Kit;
using Godot;

namespace OilfieldDays.App;

/// <summary>
/// The game's chrome, cut from the supplied UI atlas and stretched as
/// nine-patches.
///
/// <para><b>Nine-patch, not drawn.</b> Every frame, plate and field here is a
/// <see cref="StyleBoxTexture"/> over a piece of the atlas: the four corners are
/// pinned, the edges tile along one axis and the middle fills. That is what makes
/// one 242-pixel plate serve a 1400-pixel panel with its bolts, bevel and rim
/// still the size the artist drew them. A stylebox that paints a rounded
/// rectangle can imitate the colours and can never carry the art.</para>
///
/// <para><b>The header sits inside the panel.</b> The kit's own panel hangs a
/// notched banner over its top edge, which is a fine look for a fantasy sheet and
/// wrong for this atlas — the supplied cards put a flush coloured bar across the
/// top of the frame, icon and title inside it, body beneath. <see cref="Frame"/>
/// builds that, which is why it no longer uses <c>KitPanel</c>.</para>
///
/// <para>Kit widgets that draw a control rather than a container — the star
/// rating, the slider — are still the kit's, over <see cref="KitTheme"/>. There
/// was never a reason to redraw those.</para>
/// </summary>
/// <summary>
/// A panel that grows to fit what is put in it, even when nothing is laying it
/// out.
/// </summary>
/// <remarks>
/// <para><b>Why this has to exist.</b> A screen positions its panels by anchor
/// and offset, which means the panel's parent is a plain <c>Control</c> and not a
/// container — and only a container re-fits its children when their minimum size
/// changes. So a panel anchored at construction froze at whatever size it was
/// given, and every row added afterwards spilled out of the bottom of the frame.
/// The size looked deliberate, which is what made it hard to see.</para>
///
/// <para>Inside a container this stands aside: the container owns the rect, and a
/// second opinion about it would fight the sort every frame.</para>
/// </remarks>
public sealed partial class SelfSizingPanel : PanelContainer
{
    public override void _Ready() => MinimumSizeChanged += Fit;

    private void Fit()
    {
        if (GetParent() is Container)
            return;

        Vector2 wanted = GetCombinedMinimumSize();

        if (!Size.IsEqualApprox(wanted))
            Size = wanted;
    }
}

public static class SlateChrome
{
    private const string Nine = "res://assets/ui/nine";

    /// <summary>Cut pieces are shared: one texture, however many controls use it.</summary>
    private static readonly Dictionary<string, StyleBoxTexture> Cut = new();

    /// <summary>
    /// The empty stylebox, shared.
    /// </summary>
    /// <remarks>
    /// One instance rather than one per control. Every focus ring and every flat
    /// button wants the same nothing, and a fresh <c>StyleBoxEmpty</c> per call
    /// leaves hundreds of them referenced by theme overrides at shutdown — which
    /// Godot reports as leaked unsafe references on the way out.
    /// </remarks>
    public static readonly StyleBoxEmpty Nothing = new();

    public static readonly Color Muted = KitTheme.Muted;
    public static readonly Color Title = KitTheme.Amber;

    /// <summary>The game's mark, for the menu and the splash.</summary>
    public const string LogoPath = "res://assets/brand/logo.png";

    /// <summary>The key art, for the splash and the menu behind the panels.</summary>
    public const string SplashPath = "res://assets/brand/splash.png";

    /// <summary>
    /// A nine-patch stylebox over one cut piece.
    /// </summary>
    /// <param name="edge">
    /// How much of the piece is corner. Everything inside it is what stretches,
    /// so this is the number that decides whether a bolt stays a bolt.
    /// </param>
    /// <summary>
    /// A nine-patch stylebox over one cut piece, with content held clear of the
    /// rim the piece draws.
    /// </summary>
    /// <param name="edge">
    /// How much of the piece is corner, left and right. It has to cover the
    /// bolt: slice inside it and the corner bolt lands in the stretchable middle
    /// and smears across the whole edge.
    /// </param>
    /// <param name="top">The same, top and bottom.</param>
    /// <param name="padX">
    /// How far content sits from the edge. <b>This is not decoration.</b> The
    /// plates draw a bevel and a bolted rim at their edges, and content inset by
    /// less than that is printed on top of the border — which is what made every
    /// field look like its text had been pushed into the frame.
    /// </param>
    /// <param name="padY">The same, vertically.</param>
    /// <param name="lift">
    /// How much deeper the bottom content margin is than the top. <b>The plates
    /// are not vertically symmetric</b> — the button pieces carry a drop shadow
    /// along their lower edge — so the face a reader sees is centred ABOVE the
    /// middle of the piece. Content centred in the box lands below the face and
    /// reads as text that has slipped downwards, which is exactly what it is.
    /// </param>
    public static StyleBoxTexture Patch(
        string piece, int edge, int top, int padX, int padY = -1, int lift = 0)
    {
        int downY = padY < 0 ? Mathf.Max(4, top - 2) : padY;
        string key = $"{piece}|{edge}|{top}|{padX}|{downY}|{lift}";

        if (Cut.TryGetValue(key, out StyleBoxTexture? kept))
            return kept;

        var box = new StyleBoxTexture
        {
            Texture = GD.Load<Texture2D>($"{Nine}/{piece}.png"),
            TextureMarginLeft = edge,
            TextureMarginRight = edge,
            TextureMarginTop = top,
            TextureMarginBottom = top,
            ContentMarginLeft = padX,
            ContentMarginRight = padX,
            ContentMarginTop = downY,

            // Centring splits the difference, so shifting the face up by N means
            // adding twice N to the bottom margin.
            ContentMarginBottom = downY + (lift * 2),
            AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.Stretch,
            AxisStretchVertical = StyleBoxTexture.AxisStretchMode.Stretch,
        };

        Cut[key] = box;

        return box;
    }

    /// <summary>
    /// The borders the cut pieces actually draw, measured off the art.
    /// </summary>
    /// <remarks>
    /// The bolted panel carries a rim about twenty-four pixels deep with a bolt
    /// in each corner; the field plate is shallower, eighteen across and twelve
    /// down. Slicing wider than the piece is tall — the first pass sliced thirty
    /// from a ninety-pixel plate — leaves the middle band too thin to stretch and
    /// squashes the whole thing.
    /// </remarks>
    public static StyleBoxTexture PanelPlate(int pad = 26) =>
        Patch("panel", 24, 24, pad, pad, LiftOf("panel"));

    public static StyleBoxTexture FieldPlate(int padX = 18, int padY = 10) =>
        Patch("field", 18, 12, padX, padY, LiftOf("field"));

    public static StyleBoxTexture RolePlate(UiSurface.Role role, int padX = 22, int padY = 10) =>
        Patch(PlateFor(role), 34, 20, padX, padY, LiftOf(PlateFor(role)));

    /// <summary>
    /// How deep the drawn frame is on each cut piece, measured off the art.
    /// </summary>
    /// <remarks>
    /// <b>The rim is not the slice.</b> A nine-patch is sliced wide enough to
    /// keep a corner bolt or a rounded end whole, which on the button plates is
    /// most of the cap — but the frame a reader can see, and that content has to
    /// clear, is much shallower. Confusing the two makes content margins either
    /// absurdly large or, going the other way, printed on the bevel.
    /// </remarks>
    public static (float Across, float Down) RimOf(string piece) => piece switch
    {
        "panel" => (22.0f, 22.0f),
        "field" => (14.0f, 8.0f),
        _ => (16.0f, 8.0f),
    };

    /// <summary>
    /// How far above the middle of its piece each plate's drawn face sits.
    /// </summary>
    /// <remarks>
    /// Measured off the art. The button plates carry a shadow along the bottom
    /// and a highlight along the top, so the face is not where the box is; the
    /// recessed field plate is close to symmetric and needs almost nothing.
    /// </remarks>
    public static int LiftOf(string piece) => piece switch
    {
        "panel" => 0,
        "field" => 1,
        _ => 4,
    };

    /// <summary>
    /// Lay a control across a plated one, clear of the frame it draws.
    /// </summary>
    /// <remarks>
    /// <para>A row anchored to its parent's full rect gets the WHOLE rect,
    /// including the rim — the stylebox's content margins are what a container
    /// honours, and an anchored child is not laid out by a container at all. So
    /// every list row built this way printed its icon and its text over the
    /// frame, top and bottom especially, where the offsets were left at
    /// zero.</para>
    ///
    /// <para>This is the one place that knows a plate's rim, so a caller cannot
    /// forget the vertical half of it.</para>
    /// </summary>
    public static void LayAcross(Control child, string piece, float extra = 2.0f)
    {
        (float across, float down) = RimOf(piece);

        child.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        child.OffsetLeft = across + extra;
        child.OffsetRight = -(across + extra);
        child.OffsetTop = down + extra;
        child.OffsetBottom = -(down + extra);
    }

    /// <summary>The plate a role is drawn on.</summary>
    private static string PlateFor(UiSurface.Role role) => role switch
    {
        UiSurface.Role.Success => "plate-green",
        UiSurface.Role.Danger => "plate-red",
        UiSurface.Role.Warning or UiSurface.Role.Accent => "plate-amber",
        UiSurface.Role.Info or UiSurface.Role.Accent2 => "plate-blue",
        _ => "plate-slate",
    };

    /// <summary>Ink that reads on a plate: dark on amber, light on the rest.</summary>
    private static Color InkFor(UiSurface.Role role) =>
        role is UiSurface.Role.Warning or UiSurface.Role.Accent
            ? Color.FromHtml("2A1C06")
            : KitTheme.Ink;

    public static Label Heading(string text) => Line(text, 26, Title);

    public static Label SectionHead(string text) => Line(text, 15, KitTheme.Sky);

    /// <summary>The small grey word above a control.</summary>
    public static Label Caption(string text) => Line(text, 14, Muted);

    /// <summary>
    /// A caption that wraps, and reports the height its wrapping needs.
    /// </summary>
    /// <remarks>
    /// A Godot <c>Label</c> works its minimum height out from its current width,
    /// and inside a column that width is not known until the first sort has
    /// already placed it. Re-asking for a layout whenever the width changes is
    /// what makes the paragraph take the room it actually occupies instead of
    /// the one line it guessed at.
    /// </remarks>
    public static Label Paragraph(string text, float width)
    {
        Label label = Caption(text);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(width, 0);
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        label.Resized += label.UpdateMinimumSize;

        return label;
    }

    public static Label Line(string text, int size, Color colour)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", colour);

        return label;
    }

    /// <summary>
    /// One box in the mockups' 1 Mode → 2 World Setup → … breadcrumb.
    /// </summary>
    /// <param name="state">-1 done, 0 to come, 1 the step being filled in.</param>
    public static Control StepChip(string text, int state)
    {
        UiSurface.Role role = state switch
        {
            < 0 => UiSurface.Role.Success,
            0 => UiSurface.Role.Neutral,
            _ => UiSurface.Role.Warning,
        };

        var chip = new PanelContainer { CustomMinimumSize = new Vector2(0, 42) };
        chip.AddThemeStyleboxOverride("panel", RolePlate(role));
        chip.AddChild(Line(state < 0 ? text + "  ✓" : text, 16, InkFor(role)));

        return chip;
    }

    /// <summary>
    /// The frame every group sits in: the atlas's bolted plate as a nine-patch,
    /// with a flush coloured header bar across the top when it is titled.
    /// </summary>
    /// <remarks>
    /// A <c>MarginContainer</c> rides inside a styleboxless <c>PanelContainer</c>
    /// because the container hands every child its full rect — so the plate
    /// paints, the header sits on top of it, and the content lays out inside the
    /// margins, all without any of the three having to know the others' size.
    /// </remarks>
    public static Container Frame(Vector2 size, string title = "", UiSurface.Role header = UiSurface.Role.Neutral)
    {
        var carrier = new SelfSizingPanel { CustomMinimumSize = size };
        carrier.AddThemeStyleboxOverride("panel", PanelPlate(0));

        var inset = new MarginContainer();
        // Wider than the rim the plate draws, so nothing is printed on the
        // bevel or over a corner bolt.
        inset.AddThemeConstantOverride("margin_left", 26);
        inset.AddThemeConstantOverride("margin_right", 26);
        inset.AddThemeConstantOverride("margin_top", 20);
        inset.AddThemeConstantOverride("margin_bottom", 24);
        carrier.AddChild(inset);

        if (title.Length == 0)
            return inset;

        var column = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 4);
        inset.AddChild(column);

        // A gold uppercase title over a hairline — which is how the supplied
        // setup and summary panels are headed. The solid coloured bar is the
        // CARD treatment and belongs to Card; putting it on every panel painted
        // the screen in blocks the reference sheets do not have.
        column.AddChild(Line(title.ToUpperInvariant(), 15, InkFor(header) == KitTheme.Ink ? Title : Title));
        column.AddChild(Rule());

        // The heading and its rule take what they need; the body takes the rest,
        // which is what lets a panel hold something that fills — a map, a list —
        // rather than collapsing to the height of its title.
        var body = new MarginContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("margin_top", 2);
        column.AddChild(body);

        return body;
    }

    /// <summary>
    /// A card: the atlas's coloured header bar flush across the top of a frame,
    /// body beneath — the Objective, Event and status cards of the sheets.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Frame"/> on purpose. A card announces one thing
    /// and colours its header by what that thing is; a panel groups controls and
    /// heads them with a line of type. The sheets use both and never confuse
    /// them.
    /// </remarks>
    public static Container Card(Vector2 size, string title, UiSurface.Role header, string icon = "")
    {
        var carrier = new SelfSizingPanel { CustomMinimumSize = size };
        carrier.AddThemeStyleboxOverride("panel", PanelPlate(0));

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 0);
        carrier.AddChild(stack);

        var bar = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, 44),
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        bar.AddThemeStyleboxOverride("panel", RolePlate(header));

        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 8);

        if (icon.Length > 0)
            head.AddChild(Icon(icon, 26.0f));

        head.AddChild(Line(title, 17, InkFor(header)));
        bar.AddChild(head);

        var top = new MarginContainer();
        top.AddThemeConstantOverride("margin_left", 7);
        top.AddThemeConstantOverride("margin_right", 7);
        top.AddThemeConstantOverride("margin_top", 7);
        top.AddChild(bar);
        stack.AddChild(top);

        var inset = new MarginContainer();
        inset.AddThemeConstantOverride("margin_left", 16);
        inset.AddThemeConstantOverride("margin_right", 16);
        inset.AddThemeConstantOverride("margin_top", 10);
        inset.AddThemeConstantOverride("margin_bottom", 18);
        inset.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        stack.AddChild(inset);

        return inset;
    }

    /// <summary>The hairline the sheets rule their panels and rows with.</summary>
    public static Control Rule() => new ColorRect
    {
        Color = new Color(1.0f, 1.0f, 1.0f, 0.09f),
        CustomMinimumSize = new Vector2(0, 1),
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };

    /// <summary>One of the game's icons, at a size.</summary>
    public static TextureRect Icon(string name, float size) => new()
    {
        Texture = GD.Load<Texture2D>($"res://assets/icons/{name}.png"),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        CustomMinimumSize = new Vector2(size, size),
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };

    /// <summary>
    /// The panel a <see cref="Frame"/> or <see cref="Card"/> laid out, for
    /// positioning it.
    /// </summary>
    /// <remarks>
    /// It walks up rather than taking the immediate parent: a titled frame puts a
    /// heading and a rule between the carrier and the slot content goes into, and
    /// a caller that added the heading's box to a layout would be adding a node
    /// that already has a parent.
    /// </remarks>
    public static Control PanelOf(Container inset)
    {
        Node? at = inset;

        while (at is not null)
        {
            if (at is PanelContainer carrier && at != inset)
                return carrier;

            at = at.GetParent();
        }

        throw new InvalidOperationException(
            "the container did not come from Frame or Card: it has no panel above it");
    }

    /// <summary>
    /// A button on the atlas's own plate, nine-patched so a wide one keeps the
    /// rounded ends the artist drew rather than stretching them into ovals.
    /// </summary>
    public static Button Chunk(string text, UiSurface.Role accent, Vector2 size, int fontSize = 18)
    {
        string plate = PlateFor(accent);
        Color ink = InkFor(accent);

        var button = new Button { Text = text, CustomMinimumSize = size };

        button.AddThemeFontSizeOverride("font_size", fontSize);
        button.AddThemeColorOverride("font_color", ink);
        button.AddThemeColorOverride("font_hover_color", ink);
        button.AddThemeColorOverride("font_pressed_color", ink);
        button.AddThemeColorOverride("font_disabled_color", new Color(ink, 0.35f));

        StyleBoxTexture face = RolePlate(accent);

        button.AddThemeStyleboxOverride("normal", face);
        button.AddThemeStyleboxOverride("hover", Lit(plate, 1.14f));
        button.AddThemeStyleboxOverride("pressed", Lit(plate, 0.86f));
        button.AddThemeStyleboxOverride("disabled", Lit(plate, 0.55f));
        button.AddThemeStyleboxOverride("focus", Nothing);

        return button;
    }

    /// <summary>The same plate, lifted or dimmed — hover and press without a second cut.</summary>
    private static StyleBoxTexture Lit(string plate, float by)
    {
        string key = $"{plate}|lit|{by}";

        if (Cut.TryGetValue(key, out StyleBoxTexture? kept))
            return kept;

        // The field plate is a recessed panel and the coloured plates are
        // buttons; they are cut with different edges, so a lifted copy has to
        // start from the same geometry the flat one uses or the rim jumps.
        StyleBoxTexture box = plate == "field"
            ? (StyleBoxTexture)FieldPlate().Duplicate()
            : (StyleBoxTexture)Patch(plate, 34, 20, 22, 10, LiftOf(plate)).Duplicate();
        box.ModulateColor = new Color(by, by, by);
        Cut[key] = box;

        return box;
    }

    /// <summary>
    /// A settings or summary row: icon, label, then the value hard right.
    /// </summary>
    /// <remarks>
    /// The sheets rule these rows with a hairline and set the value in gold
    /// against a muted label. The kit's <c>KitLabelValue</c> draws a filled pill
    /// instead, which stacked into a wall of lozenges nothing in the reference
    /// art has — so the row is built here and the kit keeps the widgets it draws
    /// well.
    /// </remarks>
    public static Control Row2(
        string label, string value, UiSurface.Role accent = UiSurface.Role.Accent, string icon = "")
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 3);

        var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 26) };
        row.AddThemeConstantOverride("separation", 8);

        if (icon.Length > 0)
            row.AddChild(Icon(icon, 22.0f));

        Label name = Line(label, 15, Muted);
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        name.VerticalAlignment = VerticalAlignment.Center;
        name.ClipText = true;
        name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        row.AddChild(name);

        // The value trims and the label does not: a row is read left to right, so
        // losing the end of a long figure is recoverable and losing the name of
        // what it measures is not.
        Label read = Line(value, 15, Semantic(accent));
        read.HorizontalAlignment = HorizontalAlignment.Right;
        read.VerticalAlignment = VerticalAlignment.Center;
        // The value takes what it needs and no more. Given ExpandFill it claimed
        // half the row and squeezed labels that had room into ellipses — the
        // trimming was working, on the wrong control.
        read.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        row.AddChild(read);

        column.AddChild(row);
        column.AddChild(Rule());

        return column;
    }

    /// <summary>The palette colour a role reads in.</summary>
    public static Color Semantic(UiSurface.Role role) => role switch
    {
        UiSurface.Role.Success => KitTheme.Green.Lightened(0.28f),
        UiSurface.Role.Danger => KitTheme.Red.Lightened(0.28f),
        UiSurface.Role.Info or UiSurface.Role.Accent2 => KitTheme.Sky,
        UiSurface.Role.Neutral => KitTheme.Muted,
        _ => KitTheme.Amber,
    };

    /// <summary>
    /// A titled panel whose header folds it away.
    /// </summary>
    /// <remarks>
    /// <para>Built here rather than with <c>KitCollapsiblePanel</c>, which draws
    /// its own panel and its own handle from the kit's geometry — a good widget
    /// that would put a second, differently-shaped frame in the middle of a
    /// screen made of atlas nine-patches.</para>
    ///
    /// <para>Collapsing hides the body and nothing else. The frame self-sizes, so
    /// what is left is the header alone and the panels below it move up to
    /// close the gap — which is the point: a HUD over a playfield should be able
    /// to get out of the way.</para>
    /// </remarks>
    public static VBoxContainer Collapsible(
        string title, Container parent, float width, UiSurface.Role header = UiSurface.Role.Neutral,
        bool startFolded = false)
    {
        Container inset = Frame(new Vector2(width, 0));
        parent.AddChild(PanelOf(inset));

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 4);
        inset.AddChild(stack);

        var body = new VBoxContainer { Visible = !startFolded };
        body.AddThemeConstantOverride("separation", 4);

        var handle = new Button
        {
            ToggleMode = true,
            ButtonPressed = !startFolded,
            CustomMinimumSize = new Vector2(0, 26),
            Flat = true,
            FocusMode = Control.FocusModeEnum.None,
        };

        handle.AddThemeStyleboxOverride("normal", Nothing);
        handle.AddThemeStyleboxOverride("hover", Nothing);
        handle.AddThemeStyleboxOverride("pressed", Nothing);
        handle.AddThemeStyleboxOverride("focus", Nothing);

        var head = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        head.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        head.AddThemeConstantOverride("separation", 6);

        Label chevron = Line(startFolded ? "▸" : "▾", 13, Semantic(header));
        chevron.VerticalAlignment = VerticalAlignment.Center;
        head.AddChild(chevron);

        Label caption = Line(title.ToUpperInvariant(), 15, Title);
        caption.VerticalAlignment = VerticalAlignment.Center;
        caption.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        head.AddChild(caption);

        handle.AddChild(head);

        handle.Toggled += open =>
        {
            body.Visible = open;
            chevron.Text = open ? "▾" : "▸";
        };

        stack.AddChild(handle);
        stack.AddChild(Rule());
        stack.AddChild(body);

        return body;
    }

    /// <summary>A titled panel — the atlas's card, header and all.</summary>
    public static VBoxContainer Group(
        string title, Container parent, float width, UiSurface.Role header = UiSurface.Role.Neutral)
    {
        Container inset = Frame(new Vector2(width, 0), title, header);
        parent.AddChild(PanelOf(inset));

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 4);
        inset.AddChild(column);

        return column;
    }

    /// <summary>
    /// A titled panel placed by preset — the shape the boards were built around.
    /// </summary>
    /// <remarks>
    /// Returns the panel rather than its content because callers position it and
    /// sometimes flip its grow direction; <see cref="ContentOf"/> reaches the
    /// column inside. It is the same nine-patch frame <see cref="Frame"/> builds,
    /// wrapped for screens that lay out by anchor instead of by container.
    /// </remarks>
    public static PanelContainer Sign(
        string title, Vector2 size, Control.LayoutPreset preset, Vector2 offset,
        UiSurface.Role header = UiSurface.Role.Neutral)
    {
        Container inset = title.Length > 0
            ? Frame(size, title, header)
            : Frame(size);

        var panel = (PanelContainer)PanelOf(inset);
        panel.SetAnchorsPreset(preset);
        panel.Position = offset;

        // Grow away from whatever edge it was pinned to, so a panel that gets
        // taller than the size it was asked for opens inwards instead of off the
        // screen.
        panel.GrowHorizontal = preset is Control.LayoutPreset.CenterRight
            or Control.LayoutPreset.TopRight or Control.LayoutPreset.BottomRight
            ? Control.GrowDirection.Begin
            : Control.GrowDirection.End;

        panel.GrowVertical = preset is Control.LayoutPreset.BottomLeft
            or Control.LayoutPreset.BottomRight or Control.LayoutPreset.CenterBottom
            ? Control.GrowDirection.Begin
            : Control.GrowDirection.End;

        var column = new VBoxContainer { Name = "Content" };
        column.AddThemeConstantOverride("separation", 8);
        inset.AddChild(column);

        return panel;
    }

    /// <summary>
    /// The column inside a <see cref="Sign"/>.
    /// </summary>
    /// <remarks>
    /// Searched rather than fetched by path. <c>GetNode("%Content")</c> resolves
    /// a scene-unique name, which only exists for nodes an editor authored — a
    /// panel built in code has none, so the lookup logs a "node not found" for
    /// every panel on screen before falling through.
    /// </remarks>
    public static VBoxContainer ContentOf(PanelContainer sign)
    {
        VBoxContainer? column = Find(sign);

        if (column is not null)
            return column;

        throw new InvalidOperationException(
            "the panel did not come from Sign: it has no content column");
    }

    private static VBoxContainer? Find(Node at)
    {
        foreach (Node child in at.GetChildren())
        {
            if (child is VBoxContainer column && column.Name == "Content")
                return column;

            if (child.GetChildCount() > 0 && Find(child) is VBoxContainer deeper)
                return deeper;
        }

        return null;
    }

    /// <summary>A line of type, aligned.</summary>
    public static Label Text(
        string text, int size, Color colour, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        Label label = Line(text, size, colour);
        label.HorizontalAlignment = align;

        return label;
    }

    /// <summary>A paragraph that wraps.</summary>
    public static Label Body(string text, int size = 16)
    {
        Label label = Line(text, size, KitTheme.Ink);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;

        return label;
    }

    /// <summary>A dimmed ground for a board that covers the world.</summary>
    public static Control Backdrop(float dim = 0.9f)
    {
        var shade = new ColorRect
        {
            Color = new Color(KitTheme.Void, dim),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };

        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        return shade;
    }

    /// <summary>A round plate carrying a rank or a place.</summary>
    public static Control Rosette(string text, Color colour, float size = 54.0f)
    {
        var plate = new PanelContainer { CustomMinimumSize = new Vector2(size, size) };

        var round = new StyleBoxFlat
        {
            BgColor = colour,
            CornerRadiusTopLeft = (int)(size / 2),
            CornerRadiusTopRight = (int)(size / 2),
            CornerRadiusBottomLeft = (int)(size / 2),
            CornerRadiusBottomRight = (int)(size / 2),
            BorderColor = colour.Lightened(0.35f),
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
        };

        plate.AddThemeStyleboxOverride("panel", round);

        Label label = Text(text, (int)(size * 0.42f), KitTheme.Void, HorizontalAlignment.Center);
        label.VerticalAlignment = VerticalAlignment.Center;
        plate.AddChild(label);

        return plate;
    }

    /// <summary>Gold, silver, bronze, then plain.</summary>
    public static Control Medal(int place) => Rosette(
        place.ToString(System.Globalization.CultureInfo.InvariantCulture),
        place switch
        {
            1 => KitTheme.Amber,
            2 => Color.FromHtml("BFC7CE"),
            3 => Color.FromHtml("C08A4A"),
            _ => KitTheme.Muted,
        },
        44.0f);

    /// <summary>A labelled bar, for a score or a fill.</summary>
    public static Control Meter(string label, double value, Color fill, string readout, float width = 320.0f)
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 2);

        var head = new HBoxContainer();
        Label name = Line(label, 15, Muted);
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        head.AddChild(name);
        head.AddChild(Line(readout, 15, fill));
        column.AddChild(head);

        var bar = new ProgressBar
        {
            MinValue = 0.0,
            MaxValue = 1.0,
            Value = Mathf.Clamp(value, 0.0, 1.0),
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(width, 14),
        };

        bar.AddThemeStyleboxOverride("background", Solid(new Color(0.06f, 0.10f, 0.14f)));
        bar.AddThemeStyleboxOverride("fill", Solid(fill));
        column.AddChild(bar);

        return column;
    }

    private static StyleBoxFlat Solid(Color colour) => new()
    {
        BgColor = colour,
        CornerRadiusTopLeft = 7,
        CornerRadiusTopRight = 7,
        CornerRadiusBottomLeft = 7,
        CornerRadiusBottomRight = 7,
    };

    /// <summary>
    /// A button named by colour rather than role.
    /// </summary>
    /// <remarks>
    /// The role is the honest handle and <see cref="Chunk"/> takes it. This
    /// exists for call sites that compute a colour — a tab lit by whether it is
    /// current, an action tinted by a verdict — where forcing a role would mean
    /// mapping back to one at every caller.
    /// </remarks>
    public static Button Action(string text, Color accent, Vector2 size, int fontSize = 18) =>
        Chunk(text, RoleOf(accent), size, fontSize);

    private static UiSurface.Role RoleOf(Color accent) =>
        accent == KitTheme.Green ? UiSurface.Role.Success
        : accent == KitTheme.Red ? UiSurface.Role.Danger
        : accent == KitTheme.Amber ? UiSurface.Role.Warning
        : accent == KitTheme.Sky ? UiSurface.Role.Info
        : UiSurface.Role.Neutral;

    /// <summary>
    /// A list row with an icon, lines of detail, a state plate and a stamp.
    /// </summary>
    public static Button IconCard(
        string icon, string title, string[] lines, string state, Color stateColour,
        string? stamp, Color stampColour, bool selected, bool dimmed, Vector2 size)
    {
        Button plate = Slab(string.Empty, selected, dimmed, size);

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        LayAcross(row, "field");
        row.AddThemeConstantOverride("separation", 10);
        plate.AddChild(row);

        TextureRect art = Icon(icon, 40.0f);

        if (dimmed)
            art.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.4f);

        row.AddChild(art);

        var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 1);
        column.AddChild(Clipped(title, 16, dimmed ? KitTheme.Muted : KitTheme.Ink));

        foreach (string line in lines)
            column.AddChild(Clipped(line, 12, KitTheme.Muted));

        row.AddChild(column);

        if (stamp is not null)
            row.AddChild(Tag(stamp, stampColour));

        row.AddChild(Tag(state, stateColour));

        return plate;
    }

    /// <summary>
    /// The two halves of a bar: a sunk track and a solid fill.
    /// </summary>
    /// <remarks>
    /// Flat, not nine-patched. A bar is twelve pixels tall and the atlas's plates
    /// carry a bevel and bolts drawn for something four times that, so patching
    /// them into a track gives two identical-looking plates and no readable
    /// fill.
    /// </remarks>
    public static StyleBoxFlat Track() => Solid(new Color(0.05f, 0.09f, 0.13f));

    public static StyleBoxFlat Fill(Color colour) => Solid(colour);

    /// <summary>A list row's plate: flat, or lifted when it is the current one.</summary>
    public static StyleBoxTexture Row(bool selected) =>
        selected ? Lit("field", 1.45f) : FieldPlate();

    /// <summary>A word on a small plate, at the end of a row.</summary>
    public static Control Tag(string text, Color colour)
    {
        var plate = new PanelContainer
        {
            CustomMinimumSize = new Vector2(96, 28),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        plate.AddThemeStyleboxOverride("panel", FieldPlate());

        Label label = Text(text, 12, colour, HorizontalAlignment.Center);
        plate.AddChild(label);

        return plate;
    }

    /// <summary>A line that trims rather than pushing its row wider.</summary>
    private static Label Clipped(string text, int size, Color colour)
    {
        Label label = Line(text, size, colour);
        label.ClipText = true;
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        label.CustomMinimumSize = new Vector2(120, 0);

        return label;
    }

    /// <summary>
    /// A selectable plate — a row a player picks from a list.
    /// </summary>
    /// <remarks>
    /// Named apart from <see cref="Card"/> on purpose. A card is a titled panel
    /// that describes one thing; a slab is a button in a list. They were the same
    /// word in the painted chrome and that made every call site ambiguous about
    /// which behaviour it was asking for.
    /// </remarks>
    public static Button Slab(string text, bool selected, bool dimmed, Vector2 size)
    {
        var button = new Button { Text = text, CustomMinimumSize = size, Disabled = dimmed };

        // SELECTION IS A TINT, NOT A BUTTON. The atlas's coloured plates are its
        // BUTTONS — primary, confirm, cancel — and dropping one behind a list row
        // makes the row read as a giant button rather than as the selected item.
        // A row stays on the recessed field plate and is lifted a little when it
        // is the current one; the amber title carries the rest of the signal.
        button.AddThemeFontSizeOverride("font_size", 16);
        button.AddThemeColorOverride(
            "font_color", dimmed ? KitTheme.Muted : selected ? KitTheme.Amber : KitTheme.Ink);

        button.AddThemeColorOverride("font_hover_color", KitTheme.Amber);
        button.AddThemeColorOverride("font_disabled_color", KitTheme.Muted.Darkened(0.2f));
        button.AddThemeStyleboxOverride("normal", selected ? Lit("field", 1.45f) : FieldPlate());
        button.AddThemeStyleboxOverride("hover", Lit("field", 1.3f));
        button.AddThemeStyleboxOverride("pressed", Lit("field", 1.45f));
        button.AddThemeStyleboxOverride("disabled", Lit("field", 0.72f));
        button.AddThemeStyleboxOverride("focus", Nothing);

        return button;
    }

    /// <summary>A typed field: the seed and the company name.</summary>
    public static LineEdit Entry(string value, float width)
    {
        var entry = new LineEdit { Text = value, CustomMinimumSize = new Vector2(width, 46) };
        entry.AddThemeFontSizeOverride("font_size", 17);
        entry.AddThemeColorOverride("font_color", KitTheme.Ink);
        entry.AddThemeStyleboxOverride("normal", FieldPlate());
        entry.AddThemeStyleboxOverride("focus", Lit("field", 1.25f));

        return entry;
    }

    public static OptionButton Choice(string[] items, int selected, float width)
    {
        var choice = new OptionButton { CustomMinimumSize = new Vector2(width, 46) };

        foreach (string item in items)
            choice.AddItem(item);

        choice.Selected = selected;
        choice.AddThemeFontSizeOverride("font_size", 17);
        choice.AddThemeColorOverride("font_color", KitTheme.Ink);
        choice.AddThemeColorOverride("font_hover_color", Title);

        choice.AddThemeStyleboxOverride("normal", FieldPlate());
        choice.AddThemeStyleboxOverride("hover", Lit("field", 1.22f));
        choice.AddThemeStyleboxOverride("pressed", Lit("field", 1.22f));
        choice.AddThemeStyleboxOverride("disabled", Lit("field", 0.6f));
        choice.AddThemeStyleboxOverride("focus", Nothing);

        return choice;
    }

    public static KitCheckBox Tick(string text, bool pressed)
    {
        var box = new KitCheckBox { Text = text, ButtonPressed = pressed, OnRole = UiSurface.Role.Success };
        box.AddThemeFontSizeOverride("font_size", 15);

        return box;
    }

    /// <summary>The kit's slider, filled in a role rather than a colour.</summary>
    public static KitSlider Bar(double min, double max, double step, double value, float width) =>
        new()
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Value = value,
            Fill = UiSurface.Role.Accent,
            CustomMinimumSize = new Vector2(width, 26),
        };

    /// <summary>
    /// A five-pip rating — the mockups' Oil / Gas / NGL potential meter, drawn by
    /// <see cref="KitStarRating"/>.
    /// </summary>
    public static Control Pips(string label, int lit, UiSurface.Role role)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        Label name = Line(label, 15, Muted);
        name.CustomMinimumSize = new Vector2(130, 0);
        row.AddChild(name);

        var rating = new KitStarRating
        {
            Total = 5,
            Earned = Mathf.Clamp(lit, 0, 5),
            Role = role,
            CustomMinimumSize = new Vector2(150, 26),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        row.AddChild(rating);

        return row;
    }
}
