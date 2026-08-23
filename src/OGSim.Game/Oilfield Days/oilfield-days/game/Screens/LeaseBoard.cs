#nullable enable

using Beep.ECS.UI;
using Godot;
using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Kernel;
using OilfieldDays.App;
using OilfieldDays.Host;
using OilfieldDays.World;

namespace OilfieldDays.Screens;

/// <summary>
/// The lease board — the construction/placement mockup, built to its layout.
///
/// <para>A menu of things down the left, the ground in the middle with the
/// chosen spot lit, a parchment card on the right saying what it is and what it
/// costs, and a green confirm beside a red cancel along the bottom. That is
/// mockup 3.</para>
///
/// <para><b>What is placed is a hole, not a purchase.</b> The engine has no shop
/// — the menu is the basin's own structures, put there by world generation, and
/// confirming sends <c>DrillWellCommand</c> or <c>SeismicSurveyCommand</c>. The
/// mockup's padlock becomes DRILLED: a structure already holed is one the rig
/// has nothing left to prove on.</para>
/// </summary>
[Tool]
public sealed partial class LeaseBoard : Control
{
	private static readonly Length WellDepth = new(2000.0);

	private VBoxContainer _list = null!;
	private VBoxContainer _detail = null!;
	private LeaseMap _map = null!;
	private Label _mode = null!;
	private Label _status = null!;
	private Button _prospectTemplate = null!;
	private HBoxContainer _detailHeaderTemplate = null!;
	private VBoxContainer _detailLineTemplate = null!;
	private HBoxContainer _factorTemplate = null!;
	private Label _detailTextTemplate = null!;
	private Control? _topBar;
	private ulong _selected;

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		EnsureBackdrop();

		BuildList();
		BuildMap();
		BuildDetail();
		BuildFooter();
		_topBar = RequireNamed<Control>(this, "TopBar");

		if (!Godot.Engine.IsEditorHint())
			Refresh();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed(GameInput.Cancel))
		{
			SceneRouter.Instance.CloseOverlay();
			GetViewport().SetInputAsHandled();
		}
	}

	private void BuildList()
	{
		PanelContainer authored = RequireNamed<PanelContainer>(this, "StructuresPanel");
		StyleSign(authored);
		ScrollContainer scroll = RequireNamed<ScrollContainer>(authored, "ListScroll");
		scroll.CustomMinimumSize = new Vector2(396, (5 * RowHeight) + (4 * RowGap));
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;

		_list = RequireNamed<VBoxContainer>(scroll, "List");
		_list.CustomMinimumSize = new Vector2(382, 0);
		_list.AddThemeConstantOverride("separation", 8);

		_prospectTemplate = RequireNamed<Button>(_list, "ProspectTemplate");
		StyleProspectCard(_prospectTemplate, selected: false, dimmed: false);
		_prospectTemplate.Visible = Godot.Engine.IsEditorHint();
	}

	private void BuildMap()
	{
		PanelContainer authored = RequireNamed<PanelContainer>(this, "LeasePanel");
		StyleSign(authored);
		_map = RequireNamed<LeaseMap>(authored, "LeaseMap");
		_map.CustomMinimumSize = new Vector2(466, (5 * RowHeight) + (4 * RowGap));
	}

	/// <summary>How tall one structure row is, and the gap between two.</summary>
	private const int RowHeight = 84;
	private const int RowGap = 8;

	private void BuildDetail()
	{
		PanelContainer authored = RequireNamed<PanelContainer>(this, "BeliefPanel");
		StyleSign(authored);
		authored.GrowHorizontal = GrowDirection.Begin;

		PanelContainer paper = RequireNamed<PanelContainer>(authored, "DetailPaper");
		paper.AddThemeStyleboxOverride("panel", SlateChrome.FieldPlate());

		_detail = RequireNamed<VBoxContainer>(paper, "Detail");
		_detail.AddThemeConstantOverride("separation", 8);

		_detailHeaderTemplate = RequireNamed<HBoxContainer>(_detail, "DetailHeaderTemplate");
		StyleDetailHeader(_detailHeaderTemplate);
		_detailHeaderTemplate.Visible = Godot.Engine.IsEditorHint();

		_detailLineTemplate = RequireNamed<VBoxContainer>(_detail, "DetailLineTemplate");
		StyleDetailLine(_detailLineTemplate);
		_detailLineTemplate.Visible = Godot.Engine.IsEditorHint();

		_factorTemplate = RequireNamed<HBoxContainer>(_detail, "FactorTemplate");
		StyleFactor(_factorTemplate);
		_factorTemplate.Visible = Godot.Engine.IsEditorHint();

		_detailTextTemplate = RequireNamed<Label>(_detail, "DetailTextTemplate");
		StyleDetailText(_detailTextTemplate, KitTheme.Ink);
		_detailTextTemplate.Visible = Godot.Engine.IsEditorHint();
	}

	private void BuildFooter()
	{
		PanelContainer authored = RequireNamed<PanelContainer>(this, "FooterPanel");
		StyleSign(authored);
		authored.GrowVertical = GrowDirection.Begin;

		_mode = RequireNamed<Label>(authored, "Mode");
		_mode.CustomMinimumSize = new Vector2(330, 0);
		_mode.AddThemeFontSizeOverride("font_size", 19);
		_mode.AddThemeColorOverride("font_color", KitTheme.Ink);

		WireAction(authored, "SurveyButton", "SHOOT SEISMIC", UiSurface.Role.Neutral, new Vector2(230, 50), () => Order(false));
		WireAction(authored, "DrillButton", "CONFIRM - DRILL", UiSurface.Role.Success, new Vector2(250, 50), () => Order(true));
		WireAction(authored, "CancelButton", "CANCEL", UiSurface.Role.Danger, new Vector2(150, 50), () => SceneRouter.Instance.CloseOverlay());

		_status = RequireNamed<Label>(authored, "Status");
		_status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_status.CustomMinimumSize = new Vector2(960, 0);
		_status.AddThemeFontSizeOverride("font_size", 15);
		_status.AddThemeColorOverride("font_color", KitTheme.Amber);
	}

	private void Refresh()
	{
		FieldReadModel? snapshot = EngineHost.Instance.Snapshot;

		if (snapshot is null)
			return;

		DispatchBoard.BindTopBar(_topBar!, snapshot);

		foreach (Node child in _list.GetChildren())
		{
			if (child == _prospectTemplate)
				continue;

			child.QueueFree();
		}

		if (_selected == 0 && snapshot.Prospects.Count > 0)
			_selected = snapshot.Prospects[0].Prospect.Value;

		for (int i = 0; i < snapshot.Prospects.Count; i++)
		{
			ProspectView prospect = snapshot.Prospects[i];
			ulong id = prospect.Prospect.Value;

			// A structure whose source has been disproved is one a hole has
			// already answered — the mockup's padlock, earned rather than set.
			bool spent = prospect.Source < 0.10;

			Button card = ProspectCard(
				spent ? "blowout-preventer" : "wellhead-tree",
				$"{prospect.Play}",
				$"({prospect.At.X / 1000.0:F0} km, {prospect.At.Y / 1000.0:F0} km)",
				$"{prospect.ToMarket.Metres / 1000.0:F0} km to market",
				spent ? "DRILLED" : $"POS {prospect.ProbabilityOfSuccess * 100:F0}%",
				spent ? KitTheme.Muted : Odds(prospect.ProbabilityOfSuccess),
				id == _selected,
				spent);

			card.Pressed += () =>
			{
				_selected = id;
				Refresh();
			};

			_list.AddChild(card);
		}

		_map.Bind(snapshot, _selected);
		ShowDetail(Selected(snapshot));
	}

	private Button ProspectCard(
		string icon, string title, string line1, string line2, string state, Color stateColour, bool selected, bool dimmed)
	{
		var card = (Button)_prospectTemplate.Duplicate();
		card.Name = "Prospect";
		card.Visible = true;
		StyleProspectCard(card, selected, dimmed);

		TextureRect art = RequireNamed<TextureRect>(card, "Icon");
		art.Texture = GD.Load<Texture2D>($"res://assets/icons/{icon}.png");
		art.Modulate = dimmed ? new Color(1.0f, 1.0f, 1.0f, 0.4f) : Colors.White;

		Label name = RequireNamed<Label>(card, "Title");
		name.Text = title;
		name.AddThemeColorOverride("font_color", dimmed ? KitTheme.Muted : KitTheme.Ink);

		RequireNamed<Label>(card, "Line1").Text = line1;
		RequireNamed<Label>(card, "Line2").Text = line2;

		Label tag = RequireNamed<Label>(card, "State");
		tag.Text = state;
		tag.AddThemeColorOverride("font_color", stateColour);

		return card;
	}

	private void ShowDetail(ProspectView? prospect)
	{
		ClearDetail();

		if (prospect is null)
		{
			_detail.AddChild(DetailText("Nothing left in this basin to put a hole in.", KitTheme.Ink));
			_mode.Text = "Placement mode: nothing selected";
			return;
		}

		_mode.Text = $"Placement mode: a well on {prospect.Play}";

		_detail.AddChild(DetailHeader(
			"wellhead-tree",
			prospect.Play.ToString(),
			$"Probability of success {prospect.ProbabilityOfSuccess * 100:F0}%",
			Odds(prospect.ProbabilityOfSuccess).Darkened(0.2f)));

		_detail.AddChild(DetailLine("Cost", "Months of rig time, and the hole is paid for whether or not it finds anything."));
		_detail.AddChild(DetailLine("Required area", "A cleared pad, five tiles square. The ground is levelled when the rig arrives."));
		_detail.AddChild(DetailLine("Placement", "The structure is where world generation put it — confirm sends the rig there."));

		_detail.AddChild(DetailText("The five factors", new Color(0.45f, 0.40f, 0.34f)));
		_detail.AddChild(Factor("Source", prospect.Source));
		_detail.AddChild(Factor("Reservoir", prospect.Reservoir));
		_detail.AddChild(Factor("Seal", prospect.Seal));
		_detail.AddChild(Factor("Trap", prospect.Trap));
		_detail.AddChild(Factor("Timing", prospect.Timing));
	}

	private Control DetailHeader(string icon, string title, string subtitle, Color subtitleColour)
	{
		var head = (HBoxContainer)_detailHeaderTemplate.Duplicate();
		head.Name = "DetailHeader";
		head.Visible = true;
		StyleDetailHeader(head);

		TextureRect art = RequireNamed<TextureRect>(head, "Icon");
		art.Texture = GD.Load<Texture2D>($"res://assets/icons/{icon}.png");

		Label name = RequireNamed<Label>(head, "Title");
		name.Text = title;

		Label note = RequireNamed<Label>(head, "Subtitle");
		note.Text = subtitle;
		note.AddThemeColorOverride("font_color", subtitleColour);

		return head;
	}

	private Control DetailLine(string heading, string body)
	{
		var column = (VBoxContainer)_detailLineTemplate.Duplicate();
		column.Name = "DetailLine";
		column.Visible = true;
		StyleDetailLine(column);

		RequireNamed<Label>(column, "Heading").Text = heading;
		RequireNamed<Label>(column, "Body").Text = body;

		return column;
	}

	private Control Factor(string name, double value)
	{
		// Weakest reads red: the point of showing the five apart is that the low
		// one is where a measurement is worth spending.
		Color colour = value < 0.4 ? KitTheme.Red.Lightened(0.25f)
			: value < 0.7 ? KitTheme.Amber
			: KitTheme.Green.Lightened(0.25f);

		var row = (HBoxContainer)_factorTemplate.Duplicate();
		row.Name = "Factor";
		row.Visible = true;
		StyleFactor(row);

		RequireNamed<Label>(row, "Name").Text = name;

		ProgressBar bar = RequireNamed<ProgressBar>(row, "Bar");
		bar.Value = value;
		bar.AddThemeStyleboxOverride("background", SlateChrome.Track());
		bar.AddThemeStyleboxOverride("fill", SlateChrome.Fill(colour));

		RequireNamed<Label>(row, "Value").Text = $"{value * 100:F0}%";

		return row;
	}

	private Label DetailText(string text, Color colour)
	{
		var label = (Label)_detailTextTemplate.Duplicate();
		label.Name = "DetailText";
		label.Visible = true;
		label.Text = text;
		StyleDetailText(label, colour);
		return label;
	}

	private static Color Odds(double probability) => probability switch
	{
		< 0.20 => KitTheme.Red,
		< 0.35 => KitTheme.Amber,
		_ => KitTheme.Green,
	};

	private ProspectView? Selected(FieldReadModel snapshot)
	{
		for (int i = 0; i < snapshot.Prospects.Count; i++)
		{
			if (snapshot.Prospects[i].Prospect.Value == _selected)
				return snapshot.Prospects[i];
		}

		return null;
	}

	private void Order(bool drill)
	{
		FieldReadModel? snapshot = EngineHost.Instance.Snapshot;
		ProspectView? prospect = snapshot is null ? null : Selected(snapshot);

		if (prospect is null)
			return;

		var target = new EntityId<IProspect>(prospect.Prospect.Value);

		CommandResult result = EngineHost.Instance.Submit(
			drill ? new DrillWellCommand(target, WellDepth) : new SeismicSurveyCommand(target));

		if (result is Accepted)
		{
			if (drill)
				Gameplay.Current?.RecordDrill(prospect);

			_status.Text = drill ? "The rig is moving. It takes months." : "The survey crew is out.";
			Refresh();
			return;
		}

		if (result is Rejected rejected && rejected.Reasons.Count > 0)
			_status.Text = rejected.Reasons[0].Detail;
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

	private static void StyleProspectCard(Button card, bool selected, bool dimmed)
	{
		card.Text = string.Empty;
		card.CustomMinimumSize = new Vector2(382, 84);
		card.Disabled = dimmed;
		card.AddThemeFontSizeOverride("font_size", 16);
		card.AddThemeColorOverride("font_color", dimmed ? KitTheme.Muted : selected ? KitTheme.Amber : KitTheme.Ink);
		card.AddThemeColorOverride("font_hover_color", KitTheme.Amber);
		card.AddThemeColorOverride("font_disabled_color", KitTheme.Muted.Darkened(0.2f));
		card.AddThemeStyleboxOverride("normal", SlateChrome.Row(selected));
		card.AddThemeStyleboxOverride("hover", SlateChrome.Row(true));
		card.AddThemeStyleboxOverride("pressed", SlateChrome.Row(true));
		card.AddThemeStyleboxOverride("disabled", SlateChrome.Row(false));
		card.AddThemeStyleboxOverride("focus", SlateChrome.Nothing);

		HBoxContainer row = RequireNamed<HBoxContainer>(card, "Row");
		row.MouseFilter = MouseFilterEnum.Ignore;
		SlateChrome.LayAcross(row, "field");
		row.AddThemeConstantOverride("separation", 10);

		TextureRect icon = RequireNamed<TextureRect>(card, "Icon");
		icon.CustomMinimumSize = new Vector2(40, 40);
		icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;

		VBoxContainer lines = RequireNamed<VBoxContainer>(card, "Lines");
		lines.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		lines.AddThemeConstantOverride("separation", 1);

		RequireNamed<Label>(card, "Title").AddThemeFontSizeOverride("font_size", 16);

		Label line1 = RequireNamed<Label>(card, "Line1");
		line1.AddThemeFontSizeOverride("font_size", 12);
		line1.AddThemeColorOverride("font_color", KitTheme.Muted);

		Label line2 = RequireNamed<Label>(card, "Line2");
		line2.AddThemeFontSizeOverride("font_size", 12);
		line2.AddThemeColorOverride("font_color", KitTheme.Muted);

		PanelContainer statePlate = RequireNamed<PanelContainer>(card, "StatePlate");
		statePlate.CustomMinimumSize = new Vector2(96, 28);
		statePlate.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		statePlate.MouseFilter = MouseFilterEnum.Ignore;
		statePlate.AddThemeStyleboxOverride("panel", SlateChrome.FieldPlate());

		Label state = RequireNamed<Label>(card, "State");
		state.AddThemeFontSizeOverride("font_size", 12);
		state.HorizontalAlignment = HorizontalAlignment.Center;
	}

	private static void StyleDetailHeader(HBoxContainer head)
	{
		head.AddThemeConstantOverride("separation", 10);

		TextureRect icon = RequireNamed<TextureRect>(head, "Icon");
		icon.CustomMinimumSize = new Vector2(52, 52);
		icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;

		VBoxContainer titles = RequireNamed<VBoxContainer>(head, "Titles");
		titles.AddThemeConstantOverride("separation", 2);

		Label title = RequireNamed<Label>(head, "Title");
		title.AddThemeFontSizeOverride("font_size", 22);
		title.AddThemeColorOverride("font_color", KitTheme.Ink);

		Label subtitle = RequireNamed<Label>(head, "Subtitle");
		subtitle.AddThemeFontSizeOverride("font_size", 16);
	}

	private static void StyleDetailLine(VBoxContainer column)
	{
		column.AddThemeConstantOverride("separation", 1);

		Label heading = RequireNamed<Label>(column, "Heading");
		heading.AddThemeFontSizeOverride("font_size", 14);
		heading.AddThemeColorOverride("font_color", new Color(0.45f, 0.40f, 0.34f));

		Label body = RequireNamed<Label>(column, "Body");
		body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		body.CustomMinimumSize = new Vector2(366, 0);
		body.AddThemeFontSizeOverride("font_size", 15);
		body.AddThemeColorOverride("font_color", KitTheme.Ink);
	}

	private static void StyleFactor(HBoxContainer row)
	{
		row.CustomMinimumSize = new Vector2(370, 0);
		row.AddThemeConstantOverride("separation", 8);

		Label name = RequireNamed<Label>(row, "Name");
		name.CustomMinimumSize = new Vector2(100, 0);
		name.AddThemeFontSizeOverride("font_size", 15);
		name.AddThemeColorOverride("font_color", KitTheme.Ink);

		ProgressBar bar = RequireNamed<ProgressBar>(row, "Bar");
		bar.MinValue = 0.0;
		bar.MaxValue = 1.0;
		bar.ShowPercentage = false;
		bar.CustomMinimumSize = new Vector2(200, 16);

		Label value = RequireNamed<Label>(row, "Value");
		value.AddThemeFontSizeOverride("font_size", 14);
		value.AddThemeColorOverride("font_color", new Color(0.42f, 0.36f, 0.28f));
	}

	private static void StyleDetailText(Label label, Color colour)
	{
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.CustomMinimumSize = new Vector2(366, 0);
		label.AddThemeFontSizeOverride("font_size", 15);
		label.AddThemeColorOverride("font_color", colour);
	}

	private void ClearDetail()
	{
		foreach (Node child in _detail.GetChildren())
		{
			if (child == _detailHeaderTemplate ||
				child == _detailLineTemplate ||
				child == _factorTemplate ||
				child == _detailTextTemplate)
				continue;

			child.QueueFree();
		}
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
			$"{nameof(LeaseBoard)} requires a design-time {typeof(T).Name} named '{name}' under {at.GetPath()}.");
}
