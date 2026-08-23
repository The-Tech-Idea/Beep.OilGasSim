#nullable enable

using Godot;
using Beep.ECS.UI;

namespace OilfieldDays.App;

/// <summary>
/// The Beep UI kit, pointed at the mockups' palette.
///
/// <para>The kit's controls do not carry colours: <c>KitPanel</c>,
/// <c>KitButton</c>, <c>KitLabelValue</c>, <c>KitStarRating</c> and the rest all
/// draw from <c>UiSurface.Of</c> and <c>UiSurface.Semantic</c>, which read the
/// theme — the surface from the <c>neutral</c> role, and accent, success, warning
/// and danger from theirs, all under the <c>BeepSemantic</c> type. So a skin is
/// one theme, not a stylebox per control, and this is that theme.</para>
///
/// <para><b>Which is why the hand-rolled styleboxes went.</b> Every screen used
/// to build its own <c>StyleBoxFlat</c>, which meant the palette lived in as many
/// places as there were screens and the kit's own widgets — which read the theme
/// and not the stylebox — drew in colours nothing on screen shared. One theme
/// here, and a kit control dropped into any screen arrives already right.</para>
///
/// <para>Registered onto the window root by <see cref="SceneRouter"/> at startup,
/// so it reaches every scene including the ones a board opens over the game.</para>
/// </summary>
public static class KitTheme
{
    // The mockups' palette. Read off the supplied art rather than invented: the
    // navy grounds and panel faces of the setup and gameplay screens, the amber
    // that titles every panel in them, and the green and red on the two buttons
    // that mean go and stop.
    public static readonly Color Void = Color.FromHtml("071011");
    public static readonly Color Surface = Color.FromHtml("121A1B");
    public static readonly Color Amber = Color.FromHtml("E7A62F");
    public static readonly Color Sky = Color.FromHtml("4AA9D4");
    public static readonly Color Green = Color.FromHtml("83C660");
    public static readonly Color Red = Color.FromHtml("C84B3F");
    public static readonly Color Ink = Color.FromHtml("F4E7C9");
    public static readonly Color Muted = Color.FromHtml("7E877F");

    /// <summary>Build the theme and hang it on the window root.</summary>
    public static void Install(Node any)
    {
        Window root = any.GetTree().Root;

        if (root.Theme is not null)
            return;

        root.Theme = Build();
    }

    private static Theme Build()
    {
        var theme = new Theme { DefaultFontSize = 16 };

        // The seven roles the kit asks for by name. `neutral` is the one that
        // matters most: UiSurface.Of consults it FIRST and treats it as the
        // authoritative surface, so it is the panel face and nothing else.
        theme.SetColor("neutral", UiSurface.SemanticType, Surface);
        theme.SetColor("accent", UiSurface.SemanticType, Amber);
        theme.SetColor("accent2", UiSurface.SemanticType, Sky);
        theme.SetColor("success", UiSurface.SemanticType, Green);
        theme.SetColor("warning", UiSurface.SemanticType, Amber);
        theme.SetColor("danger", UiSurface.SemanticType, Red);
        theme.SetColor("info", UiSurface.SemanticType, Sky);

        theme.SetColor("font_color", "Label", Ink);

        // The stock controls the kit does not replace — dropdowns and text
        // fields — are dressed to match, so a screen mixing the two does not
        // read as two screens.
        StyleBoxFlat field = Box(Color.FromHtml("0B1213"), Color.FromHtml("334143"), radius: 2);
        StyleBoxFlat lit = Box(Color.FromHtml("172225"), Amber, radius: 2);

        foreach (string type in new[] { "OptionButton", "LineEdit", "Button" })
        {
            theme.SetStylebox("normal", type, field);
            theme.SetStylebox("hover", type, lit);
            theme.SetStylebox("pressed", type, lit);
            theme.SetStylebox("focus", type, SlateChrome.Nothing);
            theme.SetStylebox("disabled", type, Box(Color.FromHtml("11191A"), Color.FromHtml("263234"), radius: 2));
            theme.SetColor("font_color", type, Ink);
            theme.SetColor("font_hover_color", type, Amber);
            theme.SetColor("font_disabled_color", type, Muted.Darkened(0.3f));
        }

        theme.SetStylebox("panel", "PanelContainer", Box(Surface, Color.FromHtml("334143"), radius: 2));
        theme.SetStylebox("panel", "Panel", Box(Surface, Color.FromHtml("334143"), radius: 2));

        theme.SetStylebox("panel", "PopupMenu", Box(Color.FromHtml("0B1213"), Amber, radius: 2));
        theme.SetColor("font_color", "PopupMenu", Ink);
        theme.SetColor("font_hover_color", "PopupMenu", Amber);

        return theme;
    }

    private static StyleBoxFlat Box(Color fill, Color rim, int radius = 5) => new()
    {
        BgColor = fill,
        BorderColor = rim,
        BorderWidthTop = 2,
        BorderWidthBottom = 2,
        BorderWidthLeft = 2,
        BorderWidthRight = 2,
        CornerRadiusTopLeft = radius,
        CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius,
        CornerRadiusBottomRight = radius,
        ContentMarginLeft = 12,
        ContentMarginRight = 12,
        ContentMarginTop = 8,
        ContentMarginBottom = 8,
    };
}
