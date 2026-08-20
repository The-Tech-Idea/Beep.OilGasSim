#nullable enable

using System;
using Beep.ECS.UI;
using Godot;
using OilfieldDays.App;

namespace OilfieldDays.Ui;

/// <summary>
/// The left rail of the gameplay mockups: a column of icon-and-label buttons
/// down the side of the screen, the current one lit.
///
/// <para><b>It lists what exists.</b> The mockups' rail runs Overview, Map,
/// Build, Production, Finance, Contracts, Research, Fleet, Personnel. Four of
/// those open something today. The rest are shown greyed with the reason on the
/// tooltip rather than dropped, for the same reason the main menu keeps Load
/// Game: an entry that vanished would read as a plan that was never made, and an
/// entry that opened an empty screen would be worse.</para>
///
/// <para>Build is the one that is blocked rather than unwritten — gap G-02, the
/// engine has no command that places a facility at a coordinate, so a build mode
/// could draw a ghost and never put anything down.</para>
/// </summary>
public sealed partial class IconRail : PanelContainer
{
    private sealed record Stop(string Label, string Icon, string? Scene, string Note);

    private static readonly Stop[] Stops =
    {
        new("Map", "helipad-platform", null, "you are here: the field itself"),
        new("Jobs", "control-room-cabin", SceneRouter.DispatchBoard, "the dispatch board"),
        new("Leases", "security-checkpoint", SceneRouter.LeaseBoard, "the acreage and its structures"),
        new("Fleet", "mobile-crane-truck", SceneRouter.FleetBoard, "the yard's equipment"),
        new("Build", "pipeline-construction-excavator", null,
            "gap G-02: the engine has no command that places a facility at a coordinate"),
        new("Production", "pumpjack", null, "the chain is on the dispatch board until this has a screen"),
        new("Finance", "crude-oil-storage-tank", null, "borrowing and the ledger are not drawn yet"),
        new("Research", "well-testing-skid", null, "technology is gated by era; there is no research screen"),
    };

    public override void _Ready()
    {
        AddThemeStyleboxOverride("panel", SlateChrome.PanelPlate(0));

        var inset = new MarginContainer();
        inset.AddThemeConstantOverride("margin_left", 8);
        inset.AddThemeConstantOverride("margin_right", 8);
        inset.AddThemeConstantOverride("margin_top", 10);
        inset.AddThemeConstantOverride("margin_bottom", 10);
        AddChild(inset);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 6);
        inset.AddChild(column);

        for (int i = 0; i < Stops.Length; i++)
            column.AddChild(Button(Stops[i], here: i == 0));
    }

    private static Control Button(Stop stop, bool here)
    {
        var plate = new Button
        {
            CustomMinimumSize = new Vector2(78, 66),
            TooltipText = stop.Note,
            Disabled = !here && stop.Scene is null,
            Flat = false,
        };

        UiSurface.Role role = here ? UiSurface.Role.Warning : UiSurface.Role.Neutral;

        plate.AddThemeStyleboxOverride("normal", SlateChrome.RolePlate(role, 6, 6));
        plate.AddThemeStyleboxOverride("hover", SlateChrome.RolePlate(UiSurface.Role.Info, 6, 6));
        plate.AddThemeStyleboxOverride("pressed", SlateChrome.RolePlate(UiSurface.Role.Info, 6, 6));
        plate.AddThemeStyleboxOverride("disabled", SlateChrome.FieldPlate());
        plate.AddThemeStyleboxOverride("focus", SlateChrome.Nothing);

        var column = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.Center,
        };

        SlateChrome.LayAcross(column, "plate-slate", extra: 0.0f);
        column.AddThemeConstantOverride("separation", 2);
        plate.AddChild(column);

        TextureRect icon = SlateChrome.Icon(stop.Icon, 30.0f);
        icon.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

        if (stop.Scene is null && !here)
            icon.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.32f);

        column.AddChild(icon);

        Label label = SlateChrome.Line(
            stop.Label,
            12,
            here ? Color.FromHtml("2A1C06") : stop.Scene is null ? KitTheme.Muted.Darkened(0.25f) : KitTheme.Ink);

        label.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(label);

        if (stop.Scene is not null)
            plate.Pressed += () => SceneRouter.Instance.OpenOverlay(stop.Scene);

        return plate;
    }

    private static string Plate(UiSurface.Role role) => role switch
    {
        UiSurface.Role.Warning => "plate-amber",
        UiSurface.Role.Info => "plate-blue",
        _ => "plate-slate",
    };
}
