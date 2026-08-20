#nullable enable

using System;
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
/// <para>Drawn with <see cref="SlateChrome.Card"/> — a coloured header bar flush
/// across the top of the frame, body beneath — which is the treatment the
/// supplied sheets give their objective, event, vehicle and facility cards, and
/// the reason that shape exists separately from a panel.</para>
///
/// <para><b>A prospect's card is the exploration game in one panel.</b> The five
/// factors are what the company believes about source, reservoir, seal, trap and
/// timing, each already a probability the engine published — not a truth, and not
/// something recomputed here. Their product is the chance of success, and the
/// point of showing them apart is that two prospects at the same odds fail for
/// different reasons and are worth different measurements.</para>
/// </summary>
public sealed partial class SelectionCard : Control
{
    private Control? _card;

    // No _Ready anchoring here on purpose. _Ready runs after the owner has
    // placed this control, so a preset applied at that point silently discards
    // the offsets it was given and the card reappears wherever the preset
    // happens to put it — which is how it ended up underneath the side column.

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

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 2);
        body.AddChild(column);

        column.AddChild(SlateChrome.Row2(
            "Chance of success",
            prospect.ProbabilityOfSuccess.ToString("P0", CultureInfo.InvariantCulture),
            UiSurface.Role.Warning));

        column.AddChild(SlateChrome.Row2(
            "Location",
            $"{prospect.At.X / 1000.0:N0} km, {prospect.At.Y / 1000.0:N0} km",
            UiSurface.Role.Neutral));

        column.AddChild(SlateChrome.Caption("What the company believes"));

        Factor(column, "Source", prospect.Source);
        Factor(column, "Reservoir", prospect.Reservoir);
        Factor(column, "Seal", prospect.Seal);
        Factor(column, "Trap", prospect.Trap);
        Factor(column, "Timing", prospect.Timing);

        column.AddChild(SlateChrome.Caption(
            "Five beliefs, multiplied. Survey, log or core to sharpen the weakest one."));
    }

    public void ShowWell(WellStatusView well)
    {
        ArgumentNullException.ThrowIfNull(well);

        Container body = Open(
            well.DisplayId, "pumpjack",
            well.Status == WellStatus.Producing ? UiSurface.Role.Success : UiSurface.Role.Warning);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 2);
        body.AddChild(column);

        column.AddChild(SlateChrome.Row2(
            "Status",
            well.Status.ToString(),
            well.Status == WellStatus.Producing ? UiSurface.Role.Success : UiSurface.Role.Warning));

        column.AddChild(SlateChrome.Row2(
            "This month",
            $"{well.ProducedThisTick.CubicMetres:N0} m3",
            UiSurface.Role.Warning));

        column.AddChild(SlateChrome.Row2(
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

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 2);
        body.AddChild(column);

        column.AddChild(SlateChrome.Row2(
            "State",
            element.Failed ? "out of service" : "running",
            element.Failed ? UiSurface.Role.Danger : UiSurface.Role.Success));

        column.AddChild(SlateChrome.Row2(
            "This month",
            $"{element.Throughput.Kilograms / 1000.0:N0} t",
            UiSurface.Role.Warning));

        // Null is UNMEASURED, never "as new": a company has not bought the kit
        // that would tell it, and printing a number would report truth nobody
        // paid for.
        column.AddChild(SlateChrome.Row2(
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
            column.AddChild(SlateChrome.Row2(
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

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 2);
        body.AddChild(column);

        column.AddChild(SlateChrome.Row2("State", unit.State.ToString(), UiSurface.Role.Info));
        column.AddChild(SlateChrome.Row2("Carries", unit.Kind.Carries.ToString(), UiSurface.Role.Neutral));

        // What, and since when. NOT how far along: the read model publishes a
        // count of running activities and nothing per activity (gap G-15), so a
        // bar here would be a guess dressed as a measurement.
        if (unit.State == World.UnitState.Working)
        {
            column.AddChild(SlateChrome.Row2(
                "Started", $"month {unit.StartedOn}", UiSurface.Role.Warning));

            column.AddChild(SlateChrome.Caption(
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

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 2);
        body.AddChild(column);

        int failed = 0;

        for (int i = 0; i < snapshot.Chain.Count; i++)
        {
            if (snapshot.Chain[i].Failed)
                failed++;
        }

        column.AddChild(SlateChrome.Row2(
            "Chain", $"{snapshot.Chain.Count} elements", UiSurface.Role.Neutral));

        column.AddChild(SlateChrome.Row2(
            "Out of service",
            failed == 0 ? "none" : failed.ToString(CultureInfo.InvariantCulture),
            failed == 0 ? UiSurface.Role.Success : UiSurface.Role.Danger));

        column.AddChild(SlateChrome.Row2(
            "Holding it back",
            jammed ? snapshot.Bottlenecks[0].DisplayId : "nothing",
            jammed ? UiSurface.Role.Warning : UiSurface.Role.Success));

        column.AddChild(SlateChrome.Row2(
            "Out this month",
            $"{snapshot.ProducedThisTick.CubicMetres:N0} m3",
            UiSurface.Role.Warning));
    }

    /// <summary>
    /// One belief, with a bar. Weakest reads red, because the weakest factor is
    /// the one worth spending a measurement on.
    /// </summary>
    private static void Factor(Container parent, string name, double value)
    {
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 22) };
        row.AddThemeConstantOverride("separation", 8);

        Label label = SlateChrome.Line(name, 14, KitTheme.Muted);
        label.CustomMinimumSize = new Vector2(92, 0);
        label.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(label);

        var bar = new ProgressBar
        {
            MinValue = 0.0,
            MaxValue = 1.0,
            Value = Mathf.Clamp(value, 0.0, 1.0),
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(150, 12),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };

        Color fill = value < 0.4 ? KitTheme.Red.Lightened(0.25f)
            : value < 0.7 ? KitTheme.Amber
            : KitTheme.Green.Lightened(0.25f);

        bar.AddThemeStyleboxOverride("background", Flat(new Color(0.06f, 0.10f, 0.14f)));
        bar.AddThemeStyleboxOverride("fill", Flat(fill));
        row.AddChild(bar);

        Label reading = SlateChrome.Line(value.ToString("P0", CultureInfo.InvariantCulture), 13, fill);
        reading.CustomMinimumSize = new Vector2(46, 0);
        reading.HorizontalAlignment = HorizontalAlignment.Right;
        reading.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(reading);

        parent.AddChild(row);
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
        Clear();

        Container body = SlateChrome.Card(new Vector2(340, 0), title, role, icon);
        _card = SlateChrome.PanelOf(body);
        // Pinned to this control's own bottom-right corner and grown inwards, so
        // a taller card rises rather than spilling off the screen.
        _card.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
        _card.GrowHorizontal = GrowDirection.Begin;
        _card.GrowVertical = GrowDirection.Begin;
        AddChild(_card);

        return body;
    }

    private void Clear()
    {
        if (_card is null)
            return;

        RemoveChild(_card);
        _card.QueueFree();
        _card = null;
    }
}
