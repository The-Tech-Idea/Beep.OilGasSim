#nullable enable

using Beep.ECS.UI;
using Beep.ECS.UI.Kit;
using Godot;

namespace OilfieldDays.App;

/// <summary>
/// The look of Oilfield Days, taken off the mockups in
/// <c>referenceart/Mockup/Oilfield Days</c>.
///
/// <para><b>Wood and parchment, not a control room.</b> Every panel in those
/// five images is a painted wooden sign with a lighter rim; every list is a card
/// of cream paper with a coloured edge; every button is chunky and reads as a
/// thing you press. Plan 12 §2 makes that the test each screen has to pass —
/// "warm Stardew-like life-sim, not dark control-room UI" — and it is the
/// opposite of the professional mode's industrial skin.</para>
///
/// <para>One place holds the palette so a screen never invents its own.</para>
/// </summary>
public static class ScreenChrome
{
    // Read off the mockups: the sign browns, the cream of a job card, the
    // painted green and red of the dispatch buttons, the gold of a cash figure.
    public static readonly Color Wood = Color.FromHtml("6B4423");
    public static readonly Color WoodRim = Color.FromHtml("9C6B3C");
    public static readonly Color WoodDark = Color.FromHtml("422914");
    public static readonly Color Paper = Color.FromHtml("F5E7C8");
    public static readonly Color PaperRim = Color.FromHtml("C9AC7C");
    public static readonly Color Ink = Color.FromHtml("3B2A17");
    public static readonly Color Cream = Color.FromHtml("F7EBD2");
    public static readonly Color Faded = Color.FromHtml("C6B292");
    public static readonly Color Gold = Color.FromHtml("F2C14E");
    public static readonly Color Good = Color.FromHtml("5CA84B");
    public static readonly Color Bad = Color.FromHtml("C0392B");
    public static readonly Color Cash = Color.FromHtml("3E8E41");

    /// <summary>A painted wooden sign — the shape every HUD panel in the mockups is.</summary>
    public static StyleBoxFlat SignBox(int radius = 10) => new()
    {
        BgColor = Wood,
        BorderColor = WoodRim,
        BorderWidthTop = 4,
        BorderWidthBottom = 4,
        BorderWidthLeft = 4,
        BorderWidthRight = 4,
        CornerRadiusTopLeft = radius,
        CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius,
        CornerRadiusBottomRight = radius,
        ContentMarginLeft = 16,
        ContentMarginRight = 16,
        ContentMarginTop = 12,
        ContentMarginBottom = 12,
        ShadowColor = new Color(0, 0, 0, 0.4f),
        ShadowSize = 8,
        ShadowOffset = new Vector2(0, 4),
    };

    /// <summary>A card of cream paper — a job, a build item, a leaderboard row.</summary>
    public static StyleBoxFlat PaperBox(Color? rim = null, int radius = 8) => new()
    {
        BgColor = Paper,
        BorderColor = rim ?? PaperRim,
        BorderWidthTop = 3,
        BorderWidthBottom = 3,
        BorderWidthLeft = 3,
        BorderWidthRight = 3,
        CornerRadiusTopLeft = radius,
        CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius,
        CornerRadiusBottomRight = radius,
        ContentMarginLeft = 14,
        ContentMarginRight = 14,
        ContentMarginTop = 10,
        ContentMarginBottom = 10,
    };

    public static StyleBoxFlat FlatBox(Color colour, int radius = 8) => new()
    {
        BgColor = colour,
        BorderColor = colour.Darkened(0.25f),
        BorderWidthTop = 3,
        BorderWidthBottom = 3,
        BorderWidthLeft = 3,
        BorderWidthRight = 3,
        CornerRadiusTopLeft = radius,
        CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius,
        CornerRadiusBottomRight = radius,
        ContentMarginLeft = 12,
        ContentMarginRight = 12,
        ContentMarginTop = 8,
        ContentMarginBottom = 8,
    };

    /// <summary>A full-screen root that dims whatever is behind it.</summary>
    public static Control Backdrop(float dim = 0.55f)
    {
        var root = new Control();
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var shade = new ColorRect { Color = new Color(0.10f, 0.07f, 0.04f, dim) };
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(shade);

        return root;
    }

    /// <summary>
    /// A wooden sign: a title plate at the top and a column under it.
    /// </summary>
    /// <remarks>
    /// The content column is a child named "Content" — a PanelContainer gives
    /// its single child the whole rectangle, so the title has to live INSIDE
    /// that column rather than floating over it, or it lands on top of the
    /// first line of text.
    /// </remarks>
    public static PanelContainer Sign(string title, Vector2 size, Control.LayoutPreset preset, Vector2 offset)
    {
        var panel = new PanelContainer { CustomMinimumSize = size };
        panel.AddThemeStyleboxOverride("panel", SignBox());
        panel.SetAnchorsPreset(preset);
        panel.Position = offset;

        var content = new VBoxContainer { Name = "Content" };
        content.AddThemeConstantOverride("separation", 8);
        panel.AddChild(content);

        if (title.Length > 0)
        {
            var plate = new PanelContainer();
            plate.AddThemeStyleboxOverride("panel", FlatBox(WoodDark, radius: 6));
            plate.AddChild(Text(title, 15, Gold, HorizontalAlignment.Center));
            content.AddChild(plate);
        }

        return panel;
    }

    /// <summary>The column a sign's content goes in.</summary>
    public static VBoxContainer ContentOf(PanelContainer sign) => sign.GetNode<VBoxContainer>("Content");

    public static Label Text(string text, int size, Color colour, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var label = new Label { Text = text, HorizontalAlignment = align };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", colour);

        return label;
    }

    public static Label Body(string text, int size = 16)
    {
        Label label = Text(text, size, Ink);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;

        return label;
    }

    /// <summary>A chunky painted button, the shape the mockups' actions are.</summary>
    public static Button Action(string text, Color colour, Vector2 size, int fontSize = 18)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = size,
        };

        button.AddThemeStyleboxOverride("normal", FlatBox(colour));
        button.AddThemeStyleboxOverride("hover", FlatBox(colour.Lightened(0.14f)));
        button.AddThemeStyleboxOverride("pressed", FlatBox(colour.Darkened(0.14f)));
        button.AddThemeStyleboxOverride("disabled", FlatBox(new Color(0.44f, 0.40f, 0.35f)));
        button.AddThemeStyleboxOverride("focus", FlatBox(colour));
        button.AddThemeFontSizeOverride("font_size", fontSize);
        button.AddThemeColorOverride("font_color", Cream);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_pressed_color", Cream);
        button.AddThemeColorOverride("font_disabled_color", new Color(1, 1, 1, 0.6f));

        return button;
    }

    /// <summary>Kept for screens that ask the kit for a button by role.</summary>
    public static KitButton Button(string text, UiSurface.Role accent, Vector2 size)
    {
        var button = new KitButton { Text = text, Accent = accent, CustomMinimumSize = size };

        return button;
    }

    /// <summary>A selectable paper card — every list in the mockups is a stack of these.</summary>
    public static Button Card(string text, bool selected, bool dimmed, Vector2 size)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = size,
            Alignment = HorizontalAlignment.Left,
            ClipText = false,
        };

        Color rim = selected ? Good : dimmed ? new Color(0.62f, 0.58f, 0.52f) : PaperRim;

        button.AddThemeStyleboxOverride("normal", PaperBox(rim));
        button.AddThemeStyleboxOverride("hover", PaperBox(Gold));
        button.AddThemeStyleboxOverride("pressed", PaperBox(Gold));
        button.AddThemeStyleboxOverride("focus", PaperBox(rim));
        button.AddThemeFontSizeOverride("font_size", 16);
        button.AddThemeColorOverride("font_color", dimmed ? new Color(0.45f, 0.40f, 0.34f) : Ink);
        button.AddThemeColorOverride("font_hover_color", Ink);
        button.AddThemeColorOverride("font_pressed_color", Ink);

        return button;
    }

    /// <summary>A labelled bar, as the scorecard and fleet mockups draw them.</summary>
    public static Control Meter(string label, double value, Color fill, string readout, float width = 320.0f)
    {
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(width, 0) };
        column.AddThemeConstantOverride("separation", 3);

        var head = new HBoxContainer { CustomMinimumSize = new Vector2(width, 0) };
        Label name = Text(label, 15, Faded);
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        head.AddChild(name);
        head.AddChild(Text(readout, 15, Cream));
        column.AddChild(head);

        var bar = new ProgressBar
        {
            MinValue = 0.0,
            MaxValue = 1.0,
            Value = Mathf.Clamp(value, 0.0, 1.0),
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(width, 18),
        };

        bar.AddThemeStyleboxOverride("background", FlatBox(WoodDark, radius: 9));
        bar.AddThemeStyleboxOverride("fill", FlatBox(fill, radius: 9));
        column.AddChild(bar);

        return column;
    }

    /// <summary>Kept so the older boards keep compiling while they are restyled.</summary>
    public static KitPanel Panel(string title, Vector2 size, Control.LayoutPreset preset, Vector2 offset)
    {
        var panel = new KitPanel
        {
            Title = title,
            Intent = KitPanelIntent.Sheet,
            CustomMinimumSize = size,
            Size = size,
        };

        panel.SetAnchorsPreset(preset);
        panel.Position = offset;

        return panel;
    }

    /// <summary>
    /// A piece of art from the 256 library, sized for a card.
    /// </summary>
    /// <remarks>
    /// Every card in the mockups carries a thumbnail — a truck on a job, a
    /// pumpjack on a build item — and it is what makes a list readable at a
    /// glance instead of a wall of sentences.
    /// </remarks>
    public static TextureRect Icon(string name, float size)
    {
        var rect = new TextureRect
        {
            Texture = GD.Load<Texture2D>($"res://assets/icons/{name}.png"),
            CustomMinimumSize = new Vector2(size, size),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
        };

        return rect;
    }

    /// <summary>A small state plate: AVAILABLE, ACTIVE, LOCKED.</summary>
    public static PanelContainer Badge(string text, Color colour)
    {
        var badge = new PanelContainer();
        badge.AddThemeStyleboxOverride("panel", FlatBox(colour, radius: 5));

        Label label = Text(text, 13, Cream, HorizontalAlignment.Center);
        badge.AddChild(label);

        return badge;
    }

    /// <summary>
    /// The mockups' rosette — a difficulty stamp, or a rank.
    /// </summary>
    public static Control Rosette(string text, Color colour, float size = 54.0f) =>
        new RosetteMark { Text = text, Colour = colour, CustomMinimumSize = new Vector2(size, size) };

    /// <summary>A leaderboard medal: gold, silver, bronze, then plain.</summary>
    public static Control Medal(int place) => Rosette(
        place.ToString(System.Globalization.CultureInfo.InvariantCulture),
        place switch
        {
            1 => Color.FromHtml("E8B33A"),
            2 => Color.FromHtml("B9BDC2"),
            3 => Color.FromHtml("BE7B3F"),
            _ => WoodRim,
        },
        44.0f);

    /// <summary>
    /// The strip along the top of every board mockup: date, cash, the field, and
    /// how long is left.
    /// </summary>
    public static Control TopBar(FieldSnapshotLine line)
    {
        var bar = new HBoxContainer();
        bar.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        bar.Position = new Vector2(-560, 14);
        bar.CustomMinimumSize = new Vector2(1120, 0);
        bar.AddThemeConstantOverride("separation", 12);

        bar.AddChild(Chip(line.Date, Cream, 250));
        bar.AddChild(Chip(line.Cash, Gold, 220));
        bar.AddChild(Chip(line.Field, Cream, 380));
        bar.AddChild(Chip(line.Remaining, Gold, 240));

        return bar;
    }

    /// <summary>One reading on the top strip.</summary>
    public static PanelContainer Chip(string text, Color colour, float width)
    {
        var chip = new PanelContainer { CustomMinimumSize = new Vector2(width, 44) };
        chip.AddThemeStyleboxOverride("panel", SignBox(radius: 8));
        chip.AddChild(Text(text, 18, colour, HorizontalAlignment.Center));

        return chip;
    }

    /// <summary>What the top strip shows, already worded.</summary>
    public readonly record struct FieldSnapshotLine(string Date, string Cash, string Field, string Remaining);

    /// <summary>
    /// A mockup card: art on the left, lines in the middle, a state plate and a
    /// stamp on the right.
    /// </summary>
    public static Button IconCard(
        string? icon,
        string title,
        string[] lines,
        string? badge,
        Color badgeColour,
        string? rosette,
        Color rosetteColour,
        bool selected,
        bool dimmed,
        Vector2 size)
    {
        Button card = Card(string.Empty, selected, dimmed, size);

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        row.AddThemeConstantOverride("separation", 12);
        row.OffsetLeft = 12;
        row.OffsetRight = -12;
        row.OffsetTop = 8;
        row.OffsetBottom = -8;
        card.AddChild(row);

        if (icon is not null)
            row.AddChild(Icon(icon, Mathf.Min(size.Y - 26.0f, 60.0f)));

        var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 2);
        column.AddChild(Text(title, 17, dimmed ? new Color(0.45f, 0.40f, 0.34f) : Ink));

        foreach (string line in lines)
            column.AddChild(Text(line, 14, new Color(0.38f, 0.33f, 0.26f)));

        row.AddChild(column);

        if (badge is not null)
        {
            var stack = new VBoxContainer();
            stack.AddThemeConstantOverride("separation", 6);
            stack.AddChild(Badge(badge, badgeColour));
            row.AddChild(stack);
        }

        if (rosette is not null)
            row.AddChild(Rosette(rosette, rosetteColour, 46.0f));

        return card;
    }
}

/// <summary>
/// A painted rosette — the ribboned stamp the mockups put a difficulty or a rank
/// on. Drawn rather than an image so any word fits any colour.
/// </summary>
public sealed partial class RosetteMark : Control
{
    public string Text { get; set; } = string.Empty;

    public Color Colour { get; set; } = Colors.White;

    public override void _Draw()
    {
        Vector2 middle = Size * 0.5f;
        float radius = Mathf.Min(Size.X, Size.Y) * 0.46f;

        // The scallops that make it read as a stamp rather than a dot.
        for (int i = 0; i < 12; i++)
        {
            float angle = Mathf.Tau * i / 12.0f;
            DrawCircle(middle + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * 0.92f,
                radius * 0.30f, Colour.Darkened(0.18f));
        }

        DrawCircle(middle, radius, Colour);
        DrawArc(middle, radius * 0.78f, 0.0f, Mathf.Tau, 32, Colour.Lightened(0.35f), 2.0f);

        Font font = ThemeDB.FallbackFont;
        int size = (int)(radius * 0.62f);
        Vector2 measured = font.GetStringSize(Text, HorizontalAlignment.Center, -1, size);
        DrawString(font, middle + new Vector2(-measured.X * 0.5f, measured.Y * 0.32f),
            Text, HorizontalAlignment.Left, -1, size, ScreenChrome.Cream);
    }
}
