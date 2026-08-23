#nullable enable

using Beep.ECS.UI;
using Godot;
using OGSim.Composition;
using OGSim.Contracts;
using OilfieldDays.App;
using OilfieldDays.Host;

namespace OilfieldDays.Screens;

/// <summary>
/// The yard — the fleet/garage mockup, built to its layout.
///
/// <para>A wooden panel of rows down the left, each with art, a name, two bars
/// and a state; a parchment card on the right with the selected thing large, its
/// meters, and what it is doing; a row of actions along the bottom. That is
/// mockup 4.</para>
///
/// <para><b>The rows are what the company owns and the engine reports.</b> There
/// are no vehicles or fuel gauges in the engine and plan 11 §11 forbids inventing
/// them, so the two bars carry real numbers: for a well, its share of the field's
/// month and whether it is flowing; for a chain element, how much of what it was
/// offered it actually passed.</para>
/// </summary>
[Tool]
public sealed partial class FleetBoard : Control
{
	private enum Tab
	{
		Wells,
		Chain,
		Rig,
	}

	private VBoxContainer _rows = null!;
	private VBoxContainer _detail = null!;
	private HBoxContainer _tabs = null!;
	private Control? _topBar;
	private Button _rowTemplate = null!;
	private Label _emptyTemplate = null!;
	private TextureRect _detailIcon = null!;
	private Label _detailTitle = null!;
	private VBoxContainer _meterTemplate = null!;
	private Label _detailBody = null!;
	private Tab _tab = Tab.Wells;
	private int _selected;

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		EnsureBackdrop();

		BuildList();
		BuildDetail();
		BuildActions();
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

	private void BuildList()
	{
		PanelContainer authored = RequireNamed<PanelContainer>(this, "FleetListPanel");
		StyleSign(authored);
		_tabs = RequireNamed<HBoxContainer>(authored, "Tabs");
		_tabs.AddThemeConstantOverride("separation", 8);
		StyleTab("WellsTab", "WELLS", Tab.Wells, connect: true);
		StyleTab("ChainTab", "CHAIN", Tab.Chain, connect: true);
		StyleTab("RigTab", "RIG", Tab.Rig, connect: true);

		_rows = RequireNamed<VBoxContainer>(authored, "Rows");
		_rows.AddThemeConstantOverride("separation", 8);
		_rows.CustomMinimumSize = new Vector2(612, 0);

		_rowTemplate = RequireNamed<Button>(_rows, "FleetRowTemplate");
		StyleFleetRow(_rowTemplate, selected: true, dimmed: false);
		_rowTemplate.Visible = Godot.Engine.IsEditorHint();

		_emptyTemplate = RequireNamed<Label>(_rows, "EmptyTemplate");
		StyleEmpty(_emptyTemplate);
		_emptyTemplate.Visible = Godot.Engine.IsEditorHint();
	}

	private void BuildDetail()
	{
		PanelContainer authored = RequireNamed<PanelContainer>(this, "FleetDetailPanel");
		StyleSign(authored);
		authored.GrowHorizontal = GrowDirection.Begin;

		PanelContainer paper = RequireNamed<PanelContainer>(authored, "DetailPaper");
		paper.AddThemeStyleboxOverride("panel", SlateChrome.FieldPlate());

		_detail = RequireNamed<VBoxContainer>(paper, "Detail");
		_detail.AddThemeConstantOverride("separation", 10);

		_detailIcon = RequireNamed<TextureRect>(_detail, "DetailIcon");
		StyleDetailIcon(_detailIcon);

		_detailTitle = RequireNamed<Label>(_detail, "DetailTitle");
		StyleDetailTitle(_detailTitle);

		_meterTemplate = RequireNamed<VBoxContainer>(_detail, "MeterTemplate");
		StyleDetailMeter(_meterTemplate);
		_meterTemplate.Visible = Godot.Engine.IsEditorHint();

		_detailBody = RequireNamed<Label>(_detail, "DetailBody");
		StyleDetailBody(_detailBody);
	}

	private void BuildActions()
	{
		PanelContainer authored = RequireNamed<PanelContainer>(this, "ActionsPanel");
		StyleSign(authored);
		authored.GrowVertical = GrowDirection.Begin;

		WireAction(authored, "DispatchButton", "DISPATCH BOARD", UiSurface.Role.Success, new Vector2(280, 50),
			() => SceneRouter.Instance.OpenOverlay(SceneRouter.DispatchBoard));
		WireAction(authored, "LeaseButton", "THE LEASE", UiSurface.Role.Neutral, new Vector2(200, 50),
			() => SceneRouter.Instance.OpenOverlay(SceneRouter.LeaseBoard));
		WireAction(authored, "BackButton", "BACK", UiSurface.Role.Danger, new Vector2(150, 50),
			() => SceneRouter.Instance.CloseOverlay());
	}

	private void StyleTab(string name, string text, Tab tab, bool connect = false)
	{
		Button button = RequireNamed<Button>(_tabs, name);

		SlateChrome.ApplyChunk(
			button,
			text,
			_tab == tab ? UiSurface.Role.Success : UiSurface.Role.Neutral,
			new Vector2(200, 42),
			fontSize: 16);

		if (connect && !Godot.Engine.IsEditorHint())
		{
			button.Pressed += () =>
			{
				_tab = tab;
				_selected = 0;
				Refresh();
			};
		}
	}

	private void EnsureBackdrop()
	{
		Control backdrop = RequireNamed<Control>(this, "Backdrop");
		backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	private static void StyleSign(PanelContainer panel)
	{
		panel.AddThemeStyleboxOverride("panel", SlateChrome.PanelPlate(0));

		if (FindNamed<PanelContainer>(panel, "TitlePlate") is { } plate)
		{
			plate.CustomMinimumSize = new Vector2(0, 42);
			plate.AddThemeStyleboxOverride("panel", SlateChrome.RolePlate(UiSurface.Role.Warning, 16, 8));
		}

		if (FindNamed<Label>(panel, "Title") is { } title)
		{
			title.AddThemeFontSizeOverride("font_size", 15);
			title.AddThemeColorOverride("font_color", Color.FromHtml("2A1C06"));
			title.HorizontalAlignment = HorizontalAlignment.Center;
			title.VerticalAlignment = VerticalAlignment.Center;
		}
	}

	private static void WireAction(Node root, string name, string text, UiSurface.Role role, Vector2 size, System.Action action)
	{
		Button button = RequireNamed<Button>(root, name);

		SlateChrome.ApplyChunk(button, text, role, size);
		button.Pressed += () => action();
	}

	private void Refresh()
	{
		FieldReadModel? snapshot = EngineHost.Instance.Snapshot;

		if (snapshot is null)
			return;

		DispatchBoard.BindTopBar(_topBar!, snapshot);

		StyleTab("WellsTab", "WELLS", Tab.Wells);
		StyleTab("ChainTab", "CHAIN", Tab.Chain);
		StyleTab("RigTab", "RIG", Tab.Rig);

		foreach (Node child in _rows.GetChildren())
		{
			if (child == _rowTemplate || child == _emptyTemplate)
				continue;

			child.QueueFree();
		}

		switch (_tab)
		{
			case Tab.Wells:
				ShowWells(snapshot);
				break;

			case Tab.Chain:
				ShowChain(snapshot);
				break;

			default:
				ShowRig(snapshot);
				break;
		}
	}

	private void ShowWells(FieldReadModel snapshot)
	{
		if (snapshot.Wellbores.Count == 0)
		{
			_rows.AddChild(Empty("Nothing drilled yet."));
			ShowDetail("drilling-rig-derrick", "No wells", "The company owns a rig and a lease, and that is all.", []);
			return;
		}

		double most = 1.0;

		for (int i = 0; i < snapshot.Wellbores.Count; i++)
			most = Mathf.Max(most, snapshot.Wellbores[i].ProducedThisTick.CubicMetres);

		for (int i = 0; i < snapshot.Wellbores.Count; i++)
		{
			WellStatusView well = snapshot.Wellbores[i];
			int index = i;
			bool flowing = well.Status == WellStatus.Producing;

			Button row = Row(
				flowing ? "pumpjack" : "wellhead-tree",
				well.DisplayId,
				well.Status.ToString(),
				("output", well.ProducedThisTick.CubicMetres / most, flowing ? KitTheme.Green : KitTheme.Muted),
				("status", flowing ? 1.0 : 0.0, flowing ? KitTheme.Green : KitTheme.Red),
				i == _selected);

			row.Pressed += () =>
			{
				_selected = index;
				Refresh();
			};

			_rows.AddChild(row);
		}

		WellStatusView chosen = snapshot.Wellbores[Mathf.Clamp(_selected, 0, snapshot.Wellbores.Count - 1)];

		ShowDetail(
			chosen.Status == WellStatus.Producing ? "pumpjack" : "wellhead-tree",
			chosen.DisplayId,
			"Open, shut, test, log, core or abandon it from the truck, standing at the wellhead.",
			[
				("Output this month", chosen.ProducedThisTick.CubicMetres / most, $"{chosen.ProducedThisTick.CubicMetres:N0} m3", KitTheme.Green),
				("Flowing", chosen.Status == WellStatus.Producing ? 1.0 : 0.0, chosen.Status.ToString(), KitTheme.Green),
			]);
	}

	private void ShowChain(FieldReadModel snapshot)
	{
		for (int i = 0; i < snapshot.Chain.Count; i++)
		{
			ChainElementView element = snapshot.Chain[i];
			int index = i;
			double held = Held(element);
			double offered = element.Throughput.Kilograms + held;
			double passed = offered <= 0.0 ? 1.0 : element.Throughput.Kilograms / offered;

			Button row = Row(
				IconFor(element.DisplayId),
				element.DisplayId,
				held > 0.0 ? "throttling" : "clear",
				("passed", passed, held > 0.0 ? KitTheme.Red : KitTheme.Green),
				("throughput", offered <= 0.0 ? 0.0 : element.Throughput.Kilograms / Mathf.Max(1.0, Busiest(snapshot)),
					KitTheme.Green),
				i == _selected);

			row.Pressed += () =>
			{
				_selected = index;
				Refresh();
			};

			_rows.AddChild(row);
		}

		if (snapshot.Chain.Count == 0)
			return;

		ChainElementView chosen = snapshot.Chain[Mathf.Clamp(_selected, 0, snapshot.Chain.Count - 1)];
		double chosenHeld = Held(chosen);
		double chosenOffered = chosen.Throughput.Kilograms + chosenHeld;

		ShowDetail(
			IconFor(chosen.DisplayId),
			chosen.DisplayId,
			chosenHeld > 0.0
				? "This is where the field is jammed. Install another separator or expand export, from the truck at the plant."
				: "Passing everything it is offered.",
			[
				("Passed of what it was offered", chosenOffered <= 0.0 ? 1.0 : chosen.Throughput.Kilograms / chosenOffered,
					$"{chosen.Throughput.Kilograms / 1000.0:N0} t", chosenHeld > 0.0 ? KitTheme.Red : KitTheme.Green),
				("Held back", chosenOffered <= 0.0 ? 0.0 : chosenHeld / chosenOffered,
					$"{chosenHeld / 1000.0:N0} t", KitTheme.Amber),
			]);
	}

	private void ShowRig(FieldReadModel snapshot)
	{
		bool busy = snapshot.ActivitiesRunning > 0;

		_rows.AddChild(Row(
			"drilling-rig-derrick",
			"Drilling rig",
			busy ? "out on a job" : "in the yard",
			("busy", busy ? 1.0 : 0.0, busy ? KitTheme.Amber : KitTheme.Green),
			("activities", Mathf.Min(1.0, snapshot.ActivitiesRunning / 3.0), KitTheme.Green),
			selected: true));

		ShowDetail(
			"drilling-rig-derrick",
			"One rig",
			"The company owns a single rig, and that is what makes drilling a decision rather than a list. " +
			"While it is out, another hole waits — the engine refuses the order rather than queueing it.",
			[("Working", busy ? 1.0 : 0.0, busy ? "out" : "idle", KitTheme.Amber)]);
	}

	/// <summary>The mockup's row: art, name, state, and two bars.</summary>
	private Button Row(
		string icon,
		string name,
		string state,
		(string Label, double Value, Color Colour) first,
		(string Label, double Value, Color Colour) second,
		bool selected)
	{
		var card = (Button)_rowTemplate.Duplicate();
		card.Name = "FleetRow";
		card.Visible = true;
		StyleFleetRow(card, selected, dimmed: false);

		RequireNamed<TextureRect>(card, "Icon").Texture = GD.Load<Texture2D>($"res://assets/icons/{icon}.png");
		RequireNamed<Label>(card, "Name").Text = name;
		RequireNamed<Label>(card, "State").Text = state;

		BindRowBar(RequireNamed<HBoxContainer>(card, "FirstBar"), first.Label, first.Value, first.Colour);
		BindRowBar(RequireNamed<HBoxContainer>(card, "SecondBar"), second.Label, second.Value, second.Colour);

		return card;
	}

	private Label Empty(string text)
	{
		var label = (Label)_emptyTemplate.Duplicate();
		label.Name = "Empty";
		label.Visible = true;
		label.Text = text;
		StyleEmpty(label);
		return label;
	}

	private void ShowDetail(string icon, string title, string body, (string Label, double Value, string Readout, Color Colour)[] meters)
	{
		foreach (Node child in _detail.GetChildren())
		{
			if (child == _detailIcon || child == _detailTitle || child == _meterTemplate || child == _detailBody)
				continue;

			child.QueueFree();
		}

		_detailIcon.Texture = GD.Load<Texture2D>($"res://assets/icons/{icon}.png");
		_detailTitle.Text = title;

		int insertAt = _detailBody.GetIndex();

		foreach ((string label, double value, string readout, Color colour) in meters)
		{
			var column = (VBoxContainer)_meterTemplate.Duplicate();
			column.Name = "Meter";
			column.Visible = true;
			StyleDetailMeter(column);
			BindDetailMeter(column, $"{label}: {readout}", value, colour);
			_detail.AddChild(column);
			_detail.MoveChild(column, insertAt++);
		}

		_detailBody.Text = body;
	}

	private static void BindRowBar(HBoxContainer row, string label, double value, Color colour)
	{
		StyleRowBar(row);
		RequireNamed<Label>(row, "Label").Text = label;

		ProgressBar bar = RequireNamed<ProgressBar>(row, "Bar");
		bar.Value = Mathf.Clamp(value, 0.0, 1.0);
		bar.AddThemeStyleboxOverride("background", SlateChrome.Track());
		bar.AddThemeStyleboxOverride("fill", SlateChrome.Fill(colour));

		RequireNamed<Label>(row, "Value").Text = $"{value * 100:F0}%";
	}

	private static void BindDetailMeter(VBoxContainer column, string label, double value, Color colour)
	{
		RequireNamed<Label>(column, "Label").Text = label;

		ProgressBar bar = RequireNamed<ProgressBar>(column, "Bar");
		bar.Value = Mathf.Clamp(value, 0.0, 1.0);
		bar.AddThemeStyleboxOverride("background", SlateChrome.Track());
		bar.AddThemeStyleboxOverride("fill", SlateChrome.Fill(colour));
	}

	private static void StyleFleetRow(Button card, bool selected, bool dimmed)
	{
		card.Text = string.Empty;
		card.CustomMinimumSize = new Vector2(632, 92);
		card.Disabled = dimmed;
		card.AddThemeStyleboxOverride("normal", SlateChrome.Row(selected));
		card.AddThemeStyleboxOverride("hover", SlateChrome.Row(true));
		card.AddThemeStyleboxOverride("pressed", SlateChrome.Row(true));
		card.AddThemeStyleboxOverride("disabled", SlateChrome.Row(false));
		card.AddThemeStyleboxOverride("focus", SlateChrome.Nothing);

		HBoxContainer row = RequireNamed<HBoxContainer>(card, "Row");
		row.MouseFilter = MouseFilterEnum.Ignore;
		SlateChrome.LayAcross(row, "field");
		row.OffsetTop = 8;
		row.OffsetBottom = -8;
		row.AddThemeConstantOverride("separation", 12);

		TextureRect icon = RequireNamed<TextureRect>(card, "Icon");
		icon.CustomMinimumSize = new Vector2(58, 58);
		icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;

		VBoxContainer names = RequireNamed<VBoxContainer>(card, "Names");
		names.CustomMinimumSize = new Vector2(180, 0);

		Label name = RequireNamed<Label>(card, "Name");
		name.AddThemeFontSizeOverride("font_size", 17);
		name.AddThemeColorOverride("font_color", KitTheme.Ink);

		Label state = RequireNamed<Label>(card, "State");
		state.AddThemeFontSizeOverride("font_size", 14);
		state.AddThemeColorOverride("font_color", new Color(0.42f, 0.36f, 0.28f));

		VBoxContainer bars = RequireNamed<VBoxContainer>(card, "Bars");
		bars.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		bars.AddThemeConstantOverride("separation", 6);

		StyleRowBar(RequireNamed<HBoxContainer>(card, "FirstBar"));
		StyleRowBar(RequireNamed<HBoxContainer>(card, "SecondBar"));
	}

	private static void StyleRowBar(HBoxContainer row)
	{
		row.AddThemeConstantOverride("separation", 8);

		Label label = RequireNamed<Label>(row, "Label");
		label.CustomMinimumSize = new Vector2(86, 0);
		label.AddThemeFontSizeOverride("font_size", 13);
		label.AddThemeColorOverride("font_color", new Color(0.45f, 0.40f, 0.34f));

		ProgressBar bar = RequireNamed<ProgressBar>(row, "Bar");
		bar.MinValue = 0.0;
		bar.MaxValue = 1.0;
		bar.ShowPercentage = false;
		bar.CustomMinimumSize = new Vector2(176, 16);
		bar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		bar.AddThemeStyleboxOverride("background", SlateChrome.Track());

		Label value = RequireNamed<Label>(row, "Value");
		value.AddThemeFontSizeOverride("font_size", 13);
		value.AddThemeColorOverride("font_color", new Color(0.42f, 0.36f, 0.28f));
	}

	private static void StyleEmpty(Label label)
	{
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.AddThemeFontSizeOverride("font_size", 18);
		label.AddThemeColorOverride("font_color", KitTheme.Ink);
	}

	private static void StyleDetailIcon(TextureRect icon)
	{
		icon.CustomMinimumSize = new Vector2(150, 150);
		icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
	}

	private static void StyleDetailTitle(Label label)
	{
		label.AddThemeFontSizeOverride("font_size", 24);
		label.AddThemeColorOverride("font_color", KitTheme.Ink);
		label.HorizontalAlignment = HorizontalAlignment.Center;
	}

	private static void StyleDetailMeter(VBoxContainer column)
	{
		column.AddThemeConstantOverride("separation", 2);

		Label label = RequireNamed<Label>(column, "Label");
		label.AddThemeFontSizeOverride("font_size", 15);
		label.AddThemeColorOverride("font_color", KitTheme.Ink);

		ProgressBar bar = RequireNamed<ProgressBar>(column, "Bar");
		bar.MinValue = 0.0;
		bar.MaxValue = 1.0;
		bar.ShowPercentage = false;
		bar.CustomMinimumSize = new Vector2(400, 18);
		bar.AddThemeStyleboxOverride("background", SlateChrome.Track());
	}

	private static void StyleDetailBody(Label label)
	{
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.CustomMinimumSize = new Vector2(404, 0);
		label.AddThemeFontSizeOverride("font_size", 16);
		label.AddThemeColorOverride("font_color", KitTheme.Ink);
	}

	private static double Held(ChainElementView element)
	{
		double held = 0.0;

		for (int i = 0; i < element.Deferred.Count; i++)
			held += element.Deferred[i].Deferred.Kilograms;

		return held;
	}

	private static double Busiest(FieldReadModel snapshot)
	{
		double most = 1.0;

		for (int i = 0; i < snapshot.Chain.Count; i++)
			most = Mathf.Max(most, snapshot.Chain[i].Throughput.Kilograms);

		return most;
	}

	private static string IconFor(string displayId)
	{
		if (displayId.StartsWith("gathering", System.StringComparison.Ordinal))
			return "choke-manifold";

		if (displayId.StartsWith("well", System.StringComparison.Ordinal))
			return "wellhead-tree";

		return displayId switch
		{
			"separator" => "three-phase-separator",
			"tank" => "crude-oil-storage-tank",
			"flare" => "flare-stack",
			"water-disposal" => "water-injection-pump",
			"custody-meter" => "metering-station",
			"flowline" => "pipe-rack-section",
			_ => "pipeline-manifold",
		};
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
			$"{nameof(FleetBoard)} requires a design-time {typeof(T).Name} named '{name}' under {at.GetPath()}.");
}
