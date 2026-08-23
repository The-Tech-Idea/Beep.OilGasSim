#nullable enable

using System;
using Beep.ECS.UI;
using Godot;
using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Kernel;
using OilfieldDays.App;
using OilfieldDays.Host;

namespace OilfieldDays.Screens;

/// <summary>
/// The dispatch terminal — the job-board mockup, built to its layout.
///
/// <para>Board on the left with icon cards, state plates and difficulty stamps;
/// the order on the right on parchment with objective, equipment, destination,
/// reward and time; a green dispatch and a red back beneath it; the equipment
/// strip along the bottom. That is mockup 2, panel for panel.</para>
///
/// <para><b>The cards are the engine's nine commands, not invented jobs.</b>
/// Plan 11 §7 forbids faking engine state, so a card's reward is what the engine
/// will actually do, its state is whether the engine would take it now, and its
/// stamp — EASY, FAIR, HARD — is read off the target's probability of success
/// rather than assigned.</para>
/// </summary>
[Tool]
public sealed partial class DispatchBoard : Control
{
    private static readonly Length WellDepth = new(2000.0);

    private sealed record Order(
        string Title,
        string Icon,
        string Objective,
        string Equipment,
        string EquipmentIcon,
        string Reward,
        bool NeedsRig,
        Func<FieldReadModel, bool> Possible,
        Func<FieldReadModel, Command?> Build);

    private static readonly Order[] Catalogue =
    {
        new("Shoot seismic", "well-testing-skid",
            "Send the survey crew over a structure. It sharpens the trap and structure beliefs without putting a hole down.",
            "Survey crew", "well-testing-skid",
            "A sharper probability of success", false,
            s => s.Prospects.Count > 0,
            s => new SeismicSurveyCommand(new EntityId<IProspect>(Best(s).Prospect.Value))),

        new("Drill a well", "drilling-rig-derrick",
            "Put a 2,000 m hole into the best structure the company has. Months of rig time, and it may come back dry.",
            "The rig", "drilling-rig-derrick",
            "A well, or the knowledge there is nothing there", true,
            s => s.Prospects.Count > 0,
            s => new DrillWellCommand(new EntityId<IProspect>(Best(s).Prospect.Value), WellDepth)),

        new("Run a well test", "well-testing-skid",
            "Shut the well in and watch the pressure build back. The sharpest thing the company can learn about the rock.",
            "Well testing skid", "well-testing-skid",
            "Reservoir pressure, much better known", true,
            s => Compartment(s) is not null,
            s => Compartment(s) is EntityId<IReservoirCompartmentEntity> c ? new WellTestCommand(c) : null),

        new("Run a wireline log", "wireline-service-truck",
            "Run tools down the hole on a cable. Refines porosity and permeability.",
            "Wireline truck", "wireline-service-truck",
            "Porosity and permeability, refined", true,
            s => Compartment(s) is not null,
            s => Compartment(s) is EntityId<IReservoirCompartmentEntity> c ? new WirelineLogCommand(c) : null),

        new("Cut a core", "power-swivel-unit",
            "Bring rock to surface. Slow, expensive, and the least uncertain measurement there is.",
            "Coring assembly", "power-swivel-unit",
            "The rock itself, measured", true,
            s => Compartment(s) is not null,
            s => Compartment(s) is EntityId<IReservoirCompartmentEntity> c ? new CutCoreCommand(c) : null),

        new("Install another separator", "three-phase-separator",
            "Debottleneck separation so the chain stops holding production back.",
            "Construction crew", "mobile-crane-truck",
            "Separation capacity the field is short of", false,
            _ => true,
            _ => new InstallSeparatorCommand()),

        new("Expand export capacity", "metering-station",
            "Take the ceiling off what can leave the field.",
            "Construction crew", "mobile-crane-truck",
            "A higher export ceiling", false,
            _ => true,
            _ => new ExpandExportCommand()),

		// A failure without a repair is an ending, not a mechanic — the engine's
		// own words above RepairEquipment. The hazard pass takes an element out
		// and the route law shuts in everything behind it, so a field with no
		// repair order on the board stops for good the first unlucky month.
		new("Repair failed equipment", "mobile-crane-truck",
			"Emergency work on something that has already stopped. The chain behind it is shut in " +
			"until it is back, and the crew is mobilised rather than scheduled, so it is dear.",
			"Maintenance crew", "mobile-crane-truck",
			"The chain moving again", false,
			s => Broken(s) is not null,
			s => Broken(s) is ChainElementView e ? new RepairEquipmentCommand(e.Element) : null),

		new("Overhaul working equipment", "mobile-crane-truck",
			"Planned work on the worst thing still running. Cheaper than the emergency job, and it " +
			"only exists as a choice while the equipment still works.",
			"Maintenance crew", "mobile-crane-truck",
			"Condition back to new, at the planned price", false,
			s => Worn(s) is not null,
			s => Worn(s) is ChainElementView e ? new ServiceEquipmentCommand(e.Element) : null),

		new("Fit condition monitoring", "well-testing-skid",
			"Instrument a vessel so its wear can be read. Without a kit fitted the company does not " +
			"know what condition anything is in — the engine publishes nothing it has not paid to measure.",
			"Instrument crew", "wireline-service-truck",
			"A condition reading where there was none", false,
			s => Unmonitored(s) is not null,
			s => Unmonitored(s) is ChainElementView e ? new InstallMonitoringCommand(e.Element) : null),

		new("Install a manifold", "pipeline-manifold",
			"Another manifold, so the gathering system stops being the thing that decides the rate.",
			"Construction crew", "mobile-crane-truck",
			"Gathering capacity", false,
			_ => true,
			_ => new InstallManifoldCommand()),

		new("Install a gas plant", "gas-compressor-unit",
			"Somewhere for the gas to go other than the flare.",
			"Construction crew", "mobile-crane-truck",
			"Gas processing capacity", false,
			_ => true,
			_ => new InstallGasPlantCommand()),

		new("Install a treater", "three-phase-separator",
			"Take the water out to sales specification so the oil can be sold.",
			"Construction crew", "mobile-crane-truck",
			"Treating capacity", false,
			_ => true,
			_ => new InstallTreaterCommand()),

		new("Install a tank", "crude-oil-storage-tank",
			"Storage, so a stoppage downstream does not immediately stop the wells.",
			"Construction crew", "mobile-crane-truck",
			"Storage between production and export", false,
			_ => true,
			_ => new InstallTankCommand()),

		new("Remediate the injector", "water-injection-pump",
			"Clean up an injector that has stopped taking water.",
			"Well services", "wireline-service-truck",
			"Injection back where it was", false,
			_ => true,
			_ => new RemediateInjectorCommand()),

		new("Borrow $20M", "crude-oil-storage-tank",
			"Take on debt. It is spendable this month and it is owed with interest whatever the field does.",
			"The bank", "metering-station",
			"$20M now, a liability after", false,
			_ => true,
			_ => new BorrowCommand(Money.FromMillions(20.0))),

		new("Start the water flood", "water-injection-pump",
			"Replace every reservoir cubic metre the field takes out. It holds pressure up, and the " +
			"water is charged by the cubic metre in the month it is lifted.",
			"Injection plant", "water-injection-pump",
			"Reservoir pressure held, at a cost per cubic metre", false,
			_ => true,
			_ => new SetVoidageReplacementCommand(1.0)),

		new("Stop the water flood", "water-injection-pump",
			"Take the injection back to nothing and stop paying for the water.",
			"Injection plant", "water-injection-pump",
			"No more water bill, and pressure left to decline", false,
			_ => true,
			_ => new SetVoidageReplacementCommand(0.0)),

		new("Repay $20M", "crude-oil-storage-tank",
			"Pay debt down while the field is earning, so the interest stops.",
			"The bank", "metering-station",
			"Less owed", false,
			s => s.Cash >= Money.FromMillions(20.0),
			_ => new RepayCommand(Money.FromMillions(20.0))),
	};

	/// <summary>The first element the engine reports out of service.</summary>
	private static ChainElementView? Broken(FieldReadModel snapshot)
	{
		for (int i = 0; i < snapshot.Chain.Count; i++)
			if (snapshot.Chain[i].Failed) return snapshot.Chain[i];

		return null;
	}

	/// <summary>
	/// The worst-condition element that is still running.
	/// </summary>
	/// <remarks>
	/// Only elements whose condition is <b>known</b> are candidates. A null
	/// condition is not "as new", it is unmeasured — the company has not fitted
	/// the kit — and treating it as a number would report truth nobody bought.
	/// </remarks>
	private static ChainElementView? Worn(FieldReadModel snapshot)
	{
		ChainElementView? worst = null;

		for (int i = 0; i < snapshot.Chain.Count; i++)
		{
			ChainElementView element = snapshot.Chain[i];

			if (element.Failed || element.Condition is not double condition)
				continue;

			if (worst is null || condition < worst.Condition)
				worst = element;
		}

		return worst;
	}

	/// <summary>The first element whose wear nobody can read.</summary>
	private static ChainElementView? Unmonitored(FieldReadModel snapshot)
	{
		for (int i = 0; i < snapshot.Chain.Count; i++)
			if (snapshot.Chain[i].Condition is null) return snapshot.Chain[i];

		return null;
	}

	private VBoxContainer _cards = null!;
	private VBoxContainer _detail = null!;
	private HBoxContainer _fleet = null!;
	private Control? _topBar;
	private Button _cardTemplate = null!;
	private HBoxContainer _detailHeaderTemplate = null!;
	private ColorRect _detailRuleTemplate = null!;
	private VBoxContainer _sectionTemplate = null!;
	private HBoxContainer _detailRowTemplate = null!;
	private PanelContainer _fleetChipTemplate = null!;
	private Label _status = null!;
	private Button _dispatch = null!;
	private int _selected;

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		// The board covers the world, so it needs a ground of its own rather than
		// a dim over the yard: the supplied dispatch mockup is a full screen, not
		// a window onto one.
		var ground = RequireNamed<ColorRect>(this, "Ground");
		ground.Color = new Color(KitTheme.Void, 0.96f);
		ground.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		BuildBoard();
		BuildDetail();
		BuildFleetStrip();
		_topBar = RequireNamed<Control>(this, "TopBar");

		if (!Godot.Engine.IsEditorHint())
			Refresh();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed(World.GameInput.Cancel))
		{
			SceneRouter.Instance.CloseOverlay();
			GetViewport().SetInputAsHandled();
		}
	}

	private void BuildBoard()
	{
		PanelContainer authored = RequireNamed<PanelContainer>(this, "BoardPanel");
		StyleFrame(authored);
		ScrollContainer scroll = RequireNamed<ScrollContainer>(authored, "Scroll");
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;

		_cards = RequireNamed<VBoxContainer>(scroll, "Cards");
		_cards.CustomMinimumSize = new Vector2(600, 0);
		_cards.AddThemeConstantOverride("separation", 6);

		_cardTemplate = RequireNamed<Button>(_cards, "OrderCardTemplate");
		StyleOrderCard(_cardTemplate, selected: true, locked: false);
		_cardTemplate.Visible = Godot.Engine.IsEditorHint();
	}

	private void BuildDetail()
	{
		PanelContainer authored = RequireNamed<PanelContainer>(this, "DetailPanel");
		StyleFrame(authored);
		ScrollContainer scroll = RequireNamed<ScrollContainer>(authored, "DetailScroll");
		scroll.CustomMinimumSize = new Vector2(580, 420);
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;

		_detail = RequireNamed<VBoxContainer>(scroll, "Detail");
		_detail.CustomMinimumSize = new Vector2(566, 0);
		_detail.AddThemeConstantOverride("separation", 8);

		_detailHeaderTemplate = RequireNamed<HBoxContainer>(_detail, "DetailHeaderTemplate");
		StyleDetailHeader(_detailHeaderTemplate);
		_detailHeaderTemplate.Visible = Godot.Engine.IsEditorHint();

		_detailRuleTemplate = RequireNamed<ColorRect>(_detail, "DetailRuleTemplate");
		StyleRule(_detailRuleTemplate);
		_detailRuleTemplate.Visible = Godot.Engine.IsEditorHint();

		_sectionTemplate = RequireNamed<VBoxContainer>(_detail, "SectionTemplate");
		StyleSection(_sectionTemplate);
		_sectionTemplate.Visible = Godot.Engine.IsEditorHint();

		_detailRowTemplate = RequireNamed<HBoxContainer>(_detail, "DetailRowTemplate");
		StyleDetailRow(_detailRowTemplate, UiSurface.Role.Info);
		_detailRowTemplate.Visible = Godot.Engine.IsEditorHint();

		_dispatch = RequireNamed<Button>(authored, "DispatchButton");
		SlateChrome.ApplyChunk(_dispatch, "DISPATCH", UiSurface.Role.Success, new Vector2(380, 50));
		_dispatch.Pressed += () => Send(Catalogue[_selected]);

		Button authoredBack = RequireNamed<Button>(authored, "BackButton");
		SlateChrome.ApplyChunk(authoredBack, "BACK", UiSurface.Role.Danger, new Vector2(170, 50));
		authoredBack.Pressed += () => SceneRouter.Instance.CloseOverlay();

		_status = RequireNamed<Label>(authored, "Status");
		_status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_status.CustomMinimumSize = new Vector2(566, 0);
		_status.AddThemeFontSizeOverride("font_size", 13);
		_status.AddThemeColorOverride("font_color", KitTheme.Muted);
	}

	private void BuildFleetStrip()
	{
		PanelContainer authored = RequireNamed<PanelContainer>(this, "FleetPanel");
		StyleFrame(authored);
		authored.GrowVertical = GrowDirection.Begin;

		_fleet = RequireNamed<HBoxContainer>(authored, "Fleet");
		_fleet.AddThemeConstantOverride("separation", 8);

		_fleetChipTemplate = RequireNamed<PanelContainer>(_fleet, "FleetChipTemplate");
		StyleFleetChip(_fleetChipTemplate, ready: true);
		_fleetChipTemplate.Visible = Godot.Engine.IsEditorHint();
	}

	private static void StyleFrame(PanelContainer panel)
	{
		panel.AddThemeStyleboxOverride("panel", SlateChrome.PanelPlate(0));

		foreach (Node child in panel.GetChildren())
		{
			if (child is not MarginContainer inset)
				continue;

			inset.AddThemeConstantOverride("margin_left", 26);
			inset.AddThemeConstantOverride("margin_right", 26);
			inset.AddThemeConstantOverride("margin_top", 20);
			inset.AddThemeConstantOverride("margin_bottom", 24);
		}

		if (FindNamed<Label>(panel, "Header") is { } header)
			SlateChrome.PromoteHeader(header, UiSurface.Role.Warning);

		if (FindNamed<ColorRect>(panel, "Rule") is { } rule)
			rule.Visible = false;
	}

	private void Refresh()
	{
		FieldReadModel? snapshot = EngineHost.Instance.Snapshot;

		if (snapshot is null)
			return;

		// The board's own header, in the shell's register rather than the yard's:
		// a board is a company screen, and the wood belongs to the field.
		BindHeader(snapshot);

        foreach (Node child in _cards.GetChildren())
        {
            if (child == _cardTemplate)
                continue;

            child.QueueFree();
        }

        for (int i = 0; i < Catalogue.Length; i++)
        {
            Order order = Catalogue[i];
            bool possible = order.Possible(snapshot);
            bool rigBusy = order.NeedsRig && snapshot.ActivitiesRunning > 0;
            int index = i;

            (string? stamp, Color stampColour) = Difficulty(order, snapshot);

            Button card = Card(
                order,
                !possible ? "LOCKED" : rigBusy ? "RIG OUT" : "AVAILABLE",
                !possible ? UiSurface.Role.Neutral : rigBusy ? UiSurface.Role.Warning : UiSurface.Role.Success,
                stamp,
                stampColour,
                i == _selected,
                !possible || rigBusy);

            card.Pressed += () =>
            {
                _selected = index;
                Refresh();
            };

            _cards.AddChild(card);
        }

        ShowDetail(snapshot);
        ShowFleet(snapshot);
    }

    private void ShowDetail(FieldReadModel snapshot)
    {
        foreach (Node child in _detail.GetChildren())
        {
            if (child == _detailHeaderTemplate ||
                child == _detailRuleTemplate ||
                child == _sectionTemplate ||
                child == _detailRowTemplate)
                continue;

            child.QueueFree();
        }

        Order order = Catalogue[_selected];

        _detail.AddChild(DetailHeader(order.Icon, order.Title.ToUpperInvariant()));
        _detail.AddChild(DetailRule());

        _detail.AddChild(Section("Objective", order.Objective));

        _detail.AddChild(DetailRow("Equipment", order.Equipment, UiSurface.Role.Info, order.EquipmentIcon));
        _detail.AddChild(DetailRow("Destination", Destination(order, snapshot), UiSurface.Role.Neutral));
        _detail.AddChild(DetailRow("Reward", order.Reward, UiSurface.Role.Success));

        _detail.AddChild(DetailRow(
            "Rig time",
            order.NeedsRig ? "months, and the rig is out" : "runs alongside everything else",
            order.NeedsRig ? UiSurface.Role.Warning : UiSurface.Role.Neutral));

        bool rigBusy = order.NeedsRig && snapshot.ActivitiesRunning > 0;
        _dispatch.Disabled = !order.Possible(snapshot);
        _dispatch.Text = rigBusy ? "DISPATCH (RIG IS OUT)" : "DISPATCH";
    }

    private void ShowFleet(FieldReadModel snapshot)
    {
        foreach (Node child in _fleet.GetChildren())
        {
            if (child == _fleetChipTemplate)
                continue;

            child.QueueFree();
        }

        bool rigOut = snapshot.ActivitiesRunning > 0;
        bool hasWell = snapshot.Wells > 0;

        _fleet.AddChild(Chip("drilling-rig-derrick", "Rig", rigOut ? "out" : "ready", !rigOut));
        _fleet.AddChild(Chip("well-testing-skid", "Test skid", hasWell ? "ready" : "no well", hasWell));
        _fleet.AddChild(Chip("wireline-service-truck", "Wireline", hasWell ? "ready" : "no well", hasWell));
        _fleet.AddChild(Chip("mobile-crane-truck", "Crane", "ready", true));
        _fleet.AddChild(Chip("tanker-truck", "Tanker", "ready", true));
    }

    /// <summary>
    /// One row of the board: icon, title, what it needs and what it pays, with
    /// the state plate and difficulty stamp the mockup puts on the right.
    /// </summary>
    private Button Card(
        Order order, string state, UiSurface.Role stateRole, string? stamp, Color stampColour,
        bool selected, bool locked)
    {
        var card = (Button)_cardTemplate.Duplicate();
        card.Name = "OrderCard";
        card.Visible = true;
        StyleOrderCard(card, selected, locked);

        TextureRect icon = RequireNamed<TextureRect>(card, "Icon");
        icon.Texture = GD.Load<Texture2D>($"res://assets/icons/{order.Icon}.png");
        if (locked)
            icon.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.4f);
        else
            icon.Modulate = Colors.White;

        BindTrimmed(RequireNamed<Label>(card, "Title"), order.Title, 17, locked ? KitTheme.Muted : selected ? KitTheme.Amber : KitTheme.Ink);
        BindTrimmed(RequireNamed<Label>(card, "Equipment"), order.Equipment, 13, KitTheme.Muted);

        PanelContainer difficulty = RequireNamed<PanelContainer>(card, "DifficultyStamp");
        difficulty.Visible = stamp is not null;

        if (stamp is not null)
            BindStamp(difficulty, stamp, stampColour);

        BindStamp(RequireNamed<PanelContainer>(card, "StateStamp"), state, Tint(stateRole));

        return card;
    }

    /// <summary>
    /// A line that gives way rather than pushing its neighbours off the card.
    /// </summary>
    /// <remarks>
    /// A Label reports its full text as its minimum width, so a long reward
    /// description grows the row until the stamps on the right are clipped by the
    /// panel. Clipping the text instead puts the loss where a reader can see it —
    /// an ellipsis — rather than in a stamp that silently vanished.
    /// </remarks>
    private static void BindTrimmed(Label label, string text, int size, Color colour)
    {
        label.Text = text;
        label.ClipText = true;
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        label.CustomMinimumSize = new Vector2(150, 0);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", colour);
    }

    private static Color Tint(UiSurface.Role role) => role switch
    {
        UiSurface.Role.Success => KitTheme.Green.Lightened(0.4f),
        UiSurface.Role.Warning => KitTheme.Amber,
        UiSurface.Role.Danger => KitTheme.Red.Lightened(0.35f),
        _ => KitTheme.Muted,
    };

    private PanelContainer Chip(string icon, string name, string state, bool ready)
    {
        var plate = (PanelContainer)_fleetChipTemplate.Duplicate();
        plate.Name = "FleetChip";
        plate.Visible = true;
        StyleFleetChip(plate, ready);

        RequireNamed<TextureRect>(plate, "Icon").Texture = GD.Load<Texture2D>($"res://assets/icons/{icon}.png");
        RequireNamed<Label>(plate, "Name").Text = name;
        RequireNamed<Label>(plate, "State").Text = state;
        return plate;
    }

    private VBoxContainer Section(string heading, string body)
    {
        var column = (VBoxContainer)_sectionTemplate.Duplicate();
        column.Name = "Section";
        column.Visible = true;
        StyleSection(column);
        RequireNamed<Label>(column, "Heading").Text = heading;
        RequireNamed<Label>(column, "Body").Text = body;
        return column;
    }

    private HBoxContainer DetailHeader(string icon, string title)
    {
        var head = (HBoxContainer)_detailHeaderTemplate.Duplicate();
        head.Name = "DetailHeader";
        head.Visible = true;
        StyleDetailHeader(head);
        RequireNamed<TextureRect>(head, "Icon").Texture = GD.Load<Texture2D>($"res://assets/icons/{icon}.png");
        RequireNamed<Label>(head, "Title").Text = title;
        return head;
    }

    private ColorRect DetailRule()
    {
        var rule = (ColorRect)_detailRuleTemplate.Duplicate();
        rule.Name = "DetailRule";
        rule.Visible = true;
        StyleRule(rule);
        return rule;
    }

    private HBoxContainer DetailRow(string heading, string body, UiSurface.Role role, string? icon = null)
    {
        var row = (HBoxContainer)_detailRowTemplate.Duplicate();
        row.Name = "DetailRow";
        row.Visible = true;
        StyleDetailRow(row, role);

        TextureRect art = RequireNamed<TextureRect>(row, "Icon");
        art.Visible = icon is not null;

        if (icon is not null)
            art.Texture = GD.Load<Texture2D>($"res://assets/icons/{icon}.png");

        RequireNamed<Label>(row, "Heading").Text = heading.ToUpperInvariant();
        RequireNamed<Label>(row, "Body").Text = body;
        return row;
    }

    private static void StyleOrderCard(Button card, bool selected, bool locked)
    {
        card.Text = string.Empty;
        card.CustomMinimumSize = new Vector2(596, 74);
        card.Disabled = locked;
        card.AddThemeStyleboxOverride("normal", SlateChrome.Row(selected));
        card.AddThemeStyleboxOverride("hover", SlateChrome.Row(true));
        card.AddThemeStyleboxOverride("pressed", SlateChrome.Row(true));
        card.AddThemeStyleboxOverride("disabled", SlateChrome.Row(false));
        card.AddThemeStyleboxOverride("focus", SlateChrome.Nothing);

        HBoxContainer row = RequireNamed<HBoxContainer>(card, "Row");
        row.MouseFilter = MouseFilterEnum.Ignore;
        SlateChrome.LayAcross(row, "field");
        row.AddThemeConstantOverride("separation", 12);

        TextureRect icon = RequireNamed<TextureRect>(card, "Icon");
        icon.CustomMinimumSize = new Vector2(42, 42);
        icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;

        VBoxContainer lines = RequireNamed<VBoxContainer>(card, "Lines");
        lines.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        lines.AddThemeConstantOverride("separation", 1);

        BindTrimmed(RequireNamed<Label>(card, "Title"), RequireNamed<Label>(card, "Title").Text, 17,
            locked ? KitTheme.Muted : selected ? KitTheme.Amber : KitTheme.Ink);
        BindTrimmed(RequireNamed<Label>(card, "Equipment"), RequireNamed<Label>(card, "Equipment").Text, 13, KitTheme.Muted);

        StyleStamp(RequireNamed<PanelContainer>(card, "DifficultyStamp"), KitTheme.Amber);
        StyleStamp(RequireNamed<PanelContainer>(card, "StateStamp"), Tint(UiSurface.Role.Success));
    }

    private static void StyleStamp(PanelContainer plate, Color colour)
    {
        plate.CustomMinimumSize = new Vector2(94, 28);
        plate.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        plate.MouseFilter = MouseFilterEnum.Ignore;
        plate.AddThemeStyleboxOverride("panel", SlateChrome.FieldPlate());

        Label label = RequireNamed<Label>(plate, "Label");
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", colour);
    }

    private static void BindStamp(PanelContainer plate, string text, Color colour)
    {
        StyleStamp(plate, colour);
        RequireNamed<Label>(plate, "Label").Text = text;
    }

    private static void StyleDetailHeader(HBoxContainer head)
    {
        head.AddThemeConstantOverride("separation", 10);

        TextureRect icon = RequireNamed<TextureRect>(head, "Icon");
        icon.CustomMinimumSize = new Vector2(42, 42);
        icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;

        Label title = RequireNamed<Label>(head, "Title");
        title.AddThemeFontSizeOverride("font_size", 20);
        title.AddThemeColorOverride("font_color", KitTheme.Amber);
        title.ClipText = true;
        title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
    }

    private static void StyleRule(ColorRect rule)
    {
        rule.Color = new Color(1.0f, 1.0f, 1.0f, 0.09f);
        rule.CustomMinimumSize = new Vector2(0, 1);
    }

    private static void StyleSection(VBoxContainer column)
    {
        column.AddThemeConstantOverride("separation", 2);

        Label heading = RequireNamed<Label>(column, "Heading");
        heading.Text = heading.Text.ToUpperInvariant();
        heading.AddThemeFontSizeOverride("font_size", 12);
        heading.AddThemeColorOverride("font_color", KitTheme.Muted);

        Label body = RequireNamed<Label>(column, "Body");
        body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.CustomMinimumSize = new Vector2(556, 0);
        body.AddThemeFontSizeOverride("font_size", 15);
        body.AddThemeColorOverride("font_color", KitTheme.Ink);
    }

    private static void StyleDetailRow(HBoxContainer row, UiSurface.Role role)
    {
        row.CustomMinimumSize = new Vector2(0, 54);
        row.AddThemeConstantOverride("separation", 10);

        TextureRect icon = RequireNamed<TextureRect>(row, "Icon");
        icon.CustomMinimumSize = new Vector2(34, 34);
        icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;

        VBoxContainer lines = RequireNamed<VBoxContainer>(row, "Lines");
        lines.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        lines.AddThemeConstantOverride("separation", 1);

        Label heading = RequireNamed<Label>(row, "Heading");
        heading.AddThemeFontSizeOverride("font_size", 12);
        heading.AddThemeColorOverride("font_color", Tint(role));

        Label body = RequireNamed<Label>(row, "Body");
        body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.AddThemeFontSizeOverride("font_size", 15);
        body.AddThemeColorOverride("font_color", KitTheme.Ink);
    }

    private static void StyleFleetChip(PanelContainer plate, bool ready)
    {
        plate.CustomMinimumSize = new Vector2(106, 94);
        plate.AddThemeStyleboxOverride("panel", SlateChrome.FieldPlate());

        VBoxContainer column = RequireNamed<VBoxContainer>(plate, "Column");
        column.Alignment = BoxContainer.AlignmentMode.Center;
        column.AddThemeConstantOverride("separation", 2);

        TextureRect art = RequireNamed<TextureRect>(plate, "Icon");
        art.CustomMinimumSize = new Vector2(34, 34);
        art.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        art.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        art.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

        Label title = RequireNamed<Label>(plate, "Name");
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 13);
        title.AddThemeColorOverride("font_color", ready ? KitTheme.Ink : KitTheme.Muted);

        Label reading = RequireNamed<Label>(plate, "State");
        reading.HorizontalAlignment = HorizontalAlignment.Center;
        reading.AddThemeFontSizeOverride("font_size", 12);
        reading.AddThemeColorOverride("font_color", ready ? KitTheme.Green.Lightened(0.4f) : KitTheme.Muted);
    }

    /// <summary>
	/// The mockup's difficulty stamp, read off a real number.
	/// </summary>
	/// <remarks>
	/// For anything aimed at a structure it is that structure's probability of
    /// success — the one number the exploration game turns on. Work on the plant
    /// has nothing uncertain to stamp, so it carries none.
    /// </remarks>
    private static (string?, Color) Difficulty(Order order, FieldReadModel snapshot)
    {
        if (!order.Possible(snapshot) || snapshot.Prospects.Count == 0)
            return (null, ScreenChrome.Faded);

        if (!order.Title.Contains("seismic", StringComparison.Ordinal)
            && !order.Title.Contains("Drill", StringComparison.Ordinal))
        {
            return (null, ScreenChrome.Faded);
        }

        double pos = Best(snapshot).ProbabilityOfSuccess;

        return pos switch
        {
            >= 0.35 => ("EASY", ScreenChrome.Good),
            >= 0.20 => ("FAIR", ScreenChrome.Gold),
            _ => ("HARD", ScreenChrome.Bad),
        };
    }

    private static string Destination(Order order, FieldReadModel snapshot)
    {
        if (!order.Possible(snapshot))
            return "nothing to aim it at yet";

        if (order.Title.Contains("seismic", StringComparison.Ordinal)
            || order.Title.Contains("Drill", StringComparison.Ordinal))
        {
            ProspectView best = Best(snapshot);

            return $"{best.Play} at ({best.At.X / 1000.0:F0} km, {best.At.Y / 1000.0:F0} km), " +
                   $"{best.ToMarket.Metres / 1000.0:F0} km to market";
        }

        return order.NeedsRig ? "the well the company has drilled" : "the surface facilities";
    }

    private void Send(Order order)
    {
        FieldReadModel? snapshot = EngineHost.Instance.Snapshot;

        if (snapshot is null)
            return;

        Command? command = order.Build(snapshot);

        if (command is null)
        {
            _status.Text = "There is nothing to aim that at yet.";
            return;
        }

        ProspectView? aimed = command is DrillWellCommand && snapshot.Prospects.Count > 0 ? Best(snapshot) : null;
        CommandResult result = EngineHost.Instance.Submit(command);

        if (result is Accepted)
        {
            if (aimed is not null)
                Gameplay.Current?.RecordDrill(aimed);

            _status.Text = "Ordered. It runs over the coming months.";
            Refresh();
            return;
        }

        if (result is Rejected rejected)
        {
            var text = new System.Text.StringBuilder();

            for (int i = 0; i < rejected.Reasons.Count; i++)
                text.AppendLine(rejected.Reasons[i].Detail);

            _status.Text = text.ToString().TrimEnd();
        }
    }

	/// <summary>The top strip's four readings, shared by every board.</summary>
	internal static ScreenChrome.FieldSnapshotLine Line(FieldReadModel snapshot)
	{
		int left = Mathf.Max(0, 120 - snapshot.Tick.Value);

		return new ScreenChrome.FieldSnapshotLine(
			$"{snapshot.Date.Year}-{snapshot.Date.Month:00}  -  month {snapshot.Tick.Value}",
			$"${snapshot.Cash.Cents / 100.0 / 1_000_000.0:N1}M",
			$"{snapshot.Wells} wells  -  {snapshot.ProducedThisTick.CubicMetres:N0} m3  -  {snapshot.ActivitiesRunning} running",
			$"{left / 12}y {left % 12}m left");
	}

	private void BindHeader(FieldReadModel snapshot)
	{
		BindTopBar(_topBar!, snapshot);
	}

	internal static void BindTopBar(Control topBar, FieldReadModel snapshot)
	{
		if (topBar is PanelContainer panel)
			panel.AddThemeStyleboxOverride("panel", SlateChrome.PanelPlate(0));

		if (FindNamed<MarginContainer>(topBar, "Inset") is { } inset)
		{
			inset.AddThemeConstantOverride("margin_left", 18);
			inset.AddThemeConstantOverride("margin_right", 18);
			inset.AddThemeConstantOverride("margin_top", 6);
			inset.AddThemeConstantOverride("margin_bottom", 6);
		}

		if (FindNamed<HBoxContainer>(topBar, "TopRow") is { } row)
			row.AddThemeConstantOverride("separation", 8);

		Label company = RequireNamed<Label>(topBar, "Company");
		company.Text = "THE COMPANY";
		company.AddThemeFontSizeOverride("font_size", 20);
		company.AddThemeColorOverride("font_color", Color.FromHtml("2A1C06"));
		PlateLabel(company, new Vector2(0, 42), SlateChrome.RolePlate(UiSurface.Role.Warning, 16, 8), expand: true);

		SetStamp(topBar, "Date", $"{snapshot.Date.Year}-{snapshot.Date.Month:00}");
		SetStamp(topBar, "Cash", $"${snapshot.Cash.Cents / 100.0 / 1e6:N1}M", KitTheme.Green.Lightened(0.4f));
		SetStamp(topBar, "Wells", $"{snapshot.Wells} wells", KitTheme.Sky);
		SetStamp(
			topBar,
			"Activity",
			snapshot.ActivitiesRunning > 0 ? $"{snapshot.ActivitiesRunning} running" : "idle",
			snapshot.ActivitiesRunning > 0 ? KitTheme.Amber : KitTheme.Muted);
	}

	private static void SetStamp(Control topBar, string name, string text) => SetStamp(topBar, name, text, KitTheme.Ink);

	private static void SetStamp(Control topBar, string name, string text, Color colour)
	{
		Label label = RequireNamed<Label>(topBar, name);
		label.Text = text;
		label.CustomMinimumSize = name == "Wells" || name == "Activity" ? new Vector2(150, 0) : new Vector2(170, 0);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.AddThemeFontSizeOverride("font_size", 17);
		label.AddThemeColorOverride("font_color", colour);
		PlateLabel(label, new Vector2(label.CustomMinimumSize.X, 42), SlateChrome.FieldPlate(14, 8), expand: false);
	}

	private static void PlateLabel(Label label, Vector2 size, StyleBox style, bool expand)
	{
		PanelContainer plate;

		if (label.GetParent() is PanelContainer existing)
		{
			plate = existing;
		}
		else
		{
			Node? parent = label.GetParent();

			if (parent is null)
				return;

			int index = label.GetIndex();
			parent.RemoveChild(label);

			plate = new PanelContainer
			{
				Name = $"{label.Name}Plate",
				MouseFilter = MouseFilterEnum.Ignore,
			};

			plate.AddChild(label);
			parent.AddChild(plate);
			parent.MoveChild(plate, index);
		}

		plate.CustomMinimumSize = size;
		plate.SizeFlagsHorizontal = expand ? SizeFlags.ExpandFill : SizeFlags.ShrinkBegin;
		plate.AddThemeStyleboxOverride("panel", style);
		label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		label.VerticalAlignment = VerticalAlignment.Center;
	}

	/// <summary>
	/// The structure an order aims at: the most promising one nothing has been
	/// sunk into yet, falling back to the best overall once they all have.
	/// </summary>
	private static ProspectView Best(FieldReadModel snapshot) =>
		EngineHost.Instance.Drilled.BestUndrilled(snapshot) ?? snapshot.Prospects[0];

	private static EntityId<IReservoirCompartmentEntity>? Compartment(FieldReadModel snapshot)
	{
		for (int i = 0; i < snapshot.Beliefs.Count; i++)
		{
			EntityRef subject = snapshot.Beliefs[i].Subject;

			if (subject.Kind == EntityKind.Compartment)
				return new EntityId<IReservoirCompartmentEntity>(subject.Value);
		}

		return null;
	}

	private static T? FindNamed<T>(Node at, string name) where T : Node
	{
		foreach (Node child in at.GetChildren())
		{
			if (child.Name == name && child is T typed)
				return typed;

			T? found = FindNamed<T>(child, name);

			if (found is not null)
				return found;
		}

		return null;
	}

	private static T RequireNamed<T>(Node at, string name) where T : Node =>
		FindNamed<T>(at, name) ?? throw new System.InvalidOperationException(
			$"{nameof(DispatchBoard)} requires a design-time {typeof(T).Name} named '{name}' under {at.GetPath()}.");

}
