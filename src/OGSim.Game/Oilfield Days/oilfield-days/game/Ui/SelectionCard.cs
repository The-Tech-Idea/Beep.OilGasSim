#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Beep.ECS.UI;
using Godot;
using OGSim.Composition;
using OGSim.Contracts;
using OilfieldDays.App;

namespace OilfieldDays.Ui;

/// <summary>
/// The selected-entity card of the gameplay mockups: whatever the truck is
/// standing at, described.
///
/// <para>Authored as a design-time scene panel with a coloured header bar flush
/// across the top of the frame and body beneath. This matches the supplied
/// objective, event, vehicle and facility card treatments.</para>
///
/// <para><b>A prospect's card is the exploration game in one panel.</b> The five
/// factors are what the company believes about source, reservoir, seal, trap and
/// timing, each already a probability the engine published — not a truth, and not
/// something recomputed here. Their product is the chance of success, and the
/// point of showing them apart is that two prospects at the same odds fail for
/// different reasons and are worth different measurements.</para>
/// </summary>
[Tool]
public sealed partial class SelectionCard : Control
{
    private PanelContainer _card = null!;
    private PanelContainer _header = null!;
    private TextureRect _icon = null!;
    private Label _title = null!;
    private VBoxContainer _rows = null!;
    private PanelContainer _infoRowTemplate = null!;
    private Label _captionTemplate = null!;
    private HBoxContainer _factorTemplate = null!;

    // No _Ready anchoring here on purpose. _Ready runs after the owner has
    // placed this control, so a preset applied at that point silently discards
    // the offsets it was given and the card reappears wherever the preset
    // happens to put it — which is how it ended up underneath the side column.

    public override void _Ready()
    {
        BindFrame();
        _card.Visible = Godot.Engine.IsEditorHint();

        if (Godot.Engine.IsEditorHint())
            StyleFrame(UiSurface.Role.Warning);
    }

    /// <summary>Nothing is under the wheels.</summary>
    public void ShowNothing() => Clear();

    public void ShowProspect(ProspectView prospect)
    {
        ArgumentNullException.ThrowIfNull(prospect);

        Container body = Open(
            $"{prospect.Play}", "drilling-rig-derrick",
            prospect.ProbabilityOfSuccess >= 0.35 ? UiSurface.Role.Success
                : prospect.ProbabilityOfSuccess >= 0.2 ? UiSurface.Role.Warning
                : UiSurface.Role.Danger);

        body.AddChild(InfoRow(
            "Chance of success",
            prospect.ProbabilityOfSuccess.ToString("P0", CultureInfo.InvariantCulture),
            UiSurface.Role.Warning));

        body.AddChild(InfoRow(
            "Location",
            $"{prospect.At.X / 1000.0:N0} km, {prospect.At.Y / 1000.0:N0} km",
            UiSurface.Role.Neutral));

        body.AddChild(Caption("What the company believes"));

        body.AddChild(Factor("Source", prospect.Source));
        body.AddChild(Factor("Reservoir", prospect.Reservoir));
        body.AddChild(Factor("Seal", prospect.Seal));
        body.AddChild(Factor("Trap", prospect.Trap));
        body.AddChild(Factor("Timing", prospect.Timing));

        body.AddChild(Caption(
            "Five beliefs, multiplied. Survey, log or core to sharpen the weakest one."));
    }

    public void ShowWell(WellStatusView well)
    {
        ArgumentNullException.ThrowIfNull(well);

        Container body = Open(
            well.DisplayId, "pumpjack",
            well.Status == WellStatus.Producing ? UiSurface.Role.Success : UiSurface.Role.Warning);

        body.AddChild(InfoRow(
            "Status",
            well.Status.ToString(),
            well.Status == WellStatus.Producing ? UiSurface.Role.Success : UiSurface.Role.Warning));

        body.AddChild(InfoRow(
            "This month",
            $"{well.ProducedThisTick.CubicMetres:N0} m3",
            UiSurface.Role.Warning));

        body.AddChild(InfoRow(
            "A day",
            $"{well.ProducedThisTick.CubicMetres / 30.0:N0} m3",
            UiSurface.Role.Neutral));
    }

    /// <summary>One piece of the chain, and what is known about its wear.</summary>
    public void ShowElement(ChainElementView element)
    {
        ArgumentNullException.ThrowIfNull(element);

        Container body = Open(
            element.DisplayId,
            "three-phase-separator",
            element.Failed ? UiSurface.Role.Danger : UiSurface.Role.Info);

        body.AddChild(InfoRow(
            "State",
            element.Failed ? "out of service" : "running",
            element.Failed ? UiSurface.Role.Danger : UiSurface.Role.Success));

        body.AddChild(InfoRow(
            "This month",
            $"{element.Throughput.Kilograms / 1000.0:N0} t",
            UiSurface.Role.Warning));

        // Null is UNMEASURED, never "as new": a company has not bought the kit
        // that would tell it, and printing a number would report truth nobody
        // paid for.
        body.AddChild(InfoRow(
            "Condition",
            element.Condition is double condition
                ? $"{condition * 100.0:F0}%"
                : "not measured",
            element.Condition is double worn && worn < 0.5
                ? UiSurface.Role.Danger
                : UiSurface.Role.Neutral));

        double held = 0.0;

        for (int i = 0; i < element.Deferred.Count; i++)
            held += element.Deferred[i].Deferred.Kilograms;

        if (held > 0.0)
        {
            body.AddChild(InfoRow(
                "Holding back", $"{held / 1000.0:N0} t", UiSurface.Role.Warning));
        }
    }

    /// <summary>A unit: where it is in the life of its job.</summary>
    public void ShowUnit(World.Unit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        Container body = Open(
            unit.Kind.DisplayName,
            "mobile-crane-truck",
            unit.IsIdle ? UiSurface.Role.Neutral : UiSurface.Role.Info);

        body.AddChild(InfoRow("State", unit.State.ToString(), UiSurface.Role.Info));
        body.AddChild(InfoRow("Carries", unit.Kind.Carries.ToString(), UiSurface.Role.Neutral));

        // What, and since when. NOT how far along: the read model publishes a
        // count of running activities and nothing per activity (gap G-15), so a
        // bar here would be a guess dressed as a measurement.
        if (unit.State == World.UnitState.Working)
        {
            body.AddChild(InfoRow(
                "Started", $"month {unit.StartedOn}", UiSurface.Role.Warning));

            body.AddChild(Caption(
                "How far along is not published, so it is not shown."));
        }
    }

    public void ShowPlant(FieldReadModel snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        bool jammed = snapshot.Bottlenecks.Count > 0;

        Container body = Open(
            "The plant", "three-phase-separator",
            jammed ? UiSurface.Role.Warning : UiSurface.Role.Info);

        int failed = 0;

        for (int i = 0; i < snapshot.Chain.Count; i++)
        {
            if (snapshot.Chain[i].Failed)
                failed++;
        }

        body.AddChild(InfoRow(
            "Chain", $"{snapshot.Chain.Count} elements", UiSurface.Role.Neutral));

        body.AddChild(InfoRow(
            "Out of service",
            failed == 0 ? "none" : failed.ToString(CultureInfo.InvariantCulture),
            failed == 0 ? UiSurface.Role.Success : UiSurface.Role.Danger));

        body.AddChild(InfoRow(
            "Holding it back",
            jammed ? snapshot.Bottlenecks[0].DisplayId : "nothing",
            jammed ? UiSurface.Role.Warning : UiSurface.Role.Success));

        body.AddChild(InfoRow(
            "Out this month",
            $"{snapshot.ProducedThisTick.CubicMetres:N0} m3",
            UiSurface.Role.Warning));
    }

    /// <summary>
    /// One belief, with a bar. Weakest reads red, because the weakest factor is
    /// the one worth spending a measurement on.
    /// </summary>
    private HBoxContainer Factor(string name, double value)
    {
        Color fill = value < 0.4 ? KitTheme.Red.Lightened(0.25f)
            : value < 0.7 ? KitTheme.Amber
            : KitTheme.Green.Lightened(0.25f);

        var row = (HBoxContainer)_factorTemplate.Duplicate();
        row.Name = "Factor";
        row.Visible = true;
        StyleFactor(row);

        RequireNamed<Label>(row, "Name").Text = name;

        ProgressBar bar = RequireNamed<ProgressBar>(row, "Bar");
        bar.Value = Mathf.Clamp(value, 0.0, 1.0);
        bar.AddThemeStyleboxOverride("background", Flat(new Color(0.06f, 0.10f, 0.14f)));
        bar.AddThemeStyleboxOverride("fill", Flat(fill));

        Label reading = RequireNamed<Label>(row, "Value");
        reading.Text = value.ToString("P0", CultureInfo.InvariantCulture);
        reading.AddThemeColorOverride("font_color", fill);

        return row;
    }

    private static StyleBoxFlat Flat(Color colour) => new()
    {
        BgColor = colour,
        CornerRadiusTopLeft = 6,
        CornerRadiusTopRight = 6,
        CornerRadiusBottomLeft = 6,
        CornerRadiusBottomRight = 6,
    };

    /// <summary>Start a fresh card, replacing whatever was shown.</summary>
    private Container Open(string title, string icon, UiSurface.Role role)
    {
        BindFrame();
        ClearRows();

        _card.Visible = true;
        _title.Text = title;
        _icon.Texture = GD.Load<Texture2D>($"res://assets/icons/{icon}.png");
        StyleFrame(role);

        return _rows;
    }

    private void Clear()
    {
        BindFrame();
        ClearRows();
        _card.Visible = false;
    }

    private void BindFrame()
    {
        if (_card is not null)
            return;

        _card = RequireNamed<PanelContainer>(this, "SelectionCardPanel");
        _header = RequireNamed<PanelContainer>(_card, "PreviewHeader");
        _icon = RequireNamed<TextureRect>(_header, "PreviewIcon");
        _title = RequireNamed<Label>(_header, "PreviewTitle");
        _rows = RequireNamed<VBoxContainer>(_card, "Rows");
        _infoRowTemplate = RequireNamed<PanelContainer>(_rows, "InfoRowTemplate");
        _captionTemplate = RequireNamed<Label>(_rows, "CaptionTemplate");
        _factorTemplate = RequireNamed<HBoxContainer>(_rows, "FactorTemplate");

        StyleInfoRow(_infoRowTemplate, UiSurface.Role.Info);
        StyleCaption(_captionTemplate);
        StyleFactor(_factorTemplate);

        _infoRowTemplate.Visible = Godot.Engine.IsEditorHint();
        _captionTemplate.Visible = Godot.Engine.IsEditorHint();
        _factorTemplate.Visible = Godot.Engine.IsEditorHint();
    }

    private void StyleFrame(UiSurface.Role role)
    {
        _card.AddThemeStyleboxOverride("panel", SlateChrome.PanelPlate(0));
        _header.AddThemeStyleboxOverride("panel", SlateChrome.RolePlate(role));
        _title.AddThemeFontSizeOverride("font_size", 17);
        _title.AddThemeColorOverride("font_color", Color.FromHtml("2A1C06"));

        foreach (Label label in FindAll<Label>(_card))
        {
            if (label.Name.ToString() == "PreviewTitle")
                continue;

            label.AddThemeFontSizeOverride("font_size", 14);
            label.AddThemeColorOverride("font_color", KitTheme.Ink);
        }
    }

    private PanelContainer InfoRow(string label, string value, UiSurface.Role role)
    {
        var row = (PanelContainer)_infoRowTemplate.Duplicate();
        row.Name = "InfoRow";
        row.Visible = true;
        StyleInfoRow(row, role);
        RequireNamed<Label>(row, "Label").Text = label;
        RequireNamed<Label>(row, "Value").Text = value;
        return row;
    }

    private Label Caption(string text)
    {
        var label = (Label)_captionTemplate.Duplicate();
        label.Name = "Caption";
        label.Visible = true;
        label.Text = text;
        StyleCaption(label);
        return label;
    }

    private static void StyleInfoRow(PanelContainer row, UiSurface.Role role)
    {
        row.AddThemeStyleboxOverride("panel", SlateChrome.FieldPlate());

        HBoxContainer line = RequireNamed<HBoxContainer>(row, "Line");
        line.AddThemeConstantOverride("separation", 8);

        Label label = RequireNamed<Label>(row, "Label");
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", KitTheme.Muted);

        Label value = RequireNamed<Label>(row, "Value");
        value.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        value.HorizontalAlignment = HorizontalAlignment.Right;
        value.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        value.AddThemeFontSizeOverride("font_size", 13);
        value.AddThemeColorOverride("font_color", role switch
        {
            UiSurface.Role.Success => KitTheme.Green.Lightened(0.35f),
            UiSurface.Role.Warning => KitTheme.Amber,
            UiSurface.Role.Danger => KitTheme.Red.Lightened(0.35f),
            UiSurface.Role.Info => KitTheme.Sky,
            _ => KitTheme.Ink,
        });
    }

    private static void StyleCaption(Label label)
    {
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", KitTheme.Muted);
    }

    private static void StyleFactor(HBoxContainer row)
    {
        row.CustomMinimumSize = new Vector2(0, 22);
        row.AddThemeConstantOverride("separation", 8);

        Label label = RequireNamed<Label>(row, "Name");
        label.CustomMinimumSize = new Vector2(92, 0);
        label.VerticalAlignment = VerticalAlignment.Center;
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", KitTheme.Muted);

        ProgressBar bar = RequireNamed<ProgressBar>(row, "Bar");
        bar.MinValue = 0.0;
        bar.MaxValue = 1.0;
        bar.ShowPercentage = false;
        bar.CustomMinimumSize = new Vector2(150, 12);
        bar.SizeFlagsVertical = SizeFlags.ShrinkCenter;

        Label reading = RequireNamed<Label>(row, "Value");
        reading.CustomMinimumSize = new Vector2(46, 0);
        reading.HorizontalAlignment = HorizontalAlignment.Right;
        reading.VerticalAlignment = VerticalAlignment.Center;
        reading.AddThemeFontSizeOverride("font_size", 13);
    }

    private void ClearRows()
    {
        foreach (Node child in _rows.GetChildren())
        {
            if (child == _infoRowTemplate || child == _captionTemplate || child == _factorTemplate)
                continue;

            _rows.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static T RequireNamed<T>(Node at, string name) where T : Node =>
        FindNamed<T>(at, name) ?? throw new InvalidOperationException(
            $"{at.GetPath()} requires an authored {typeof(T).Name} named '{name}'.");

    private static T? FindNamed<T>(Node at, string name) where T : Node
    {
        if (at is T typed && at.Name == name)
            return typed;

        foreach (Node child in at.GetChildren())
        {
            T? found = FindNamed<T>(child, name);

            if (found is not null)
                return found;
        }

        return null;
    }

    private static IEnumerable<T> FindAll<T>(Node at) where T : Node
    {
        if (at is T typed)
            yield return typed;

        foreach (Node child in at.GetChildren())
        {
            foreach (T found in FindAll<T>(child))
                yield return found;
        }
    }
}
