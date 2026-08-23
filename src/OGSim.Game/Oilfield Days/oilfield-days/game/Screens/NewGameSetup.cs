#nullable enable

using System;
using System.Globalization;
using Beep.ECS.UI;
using Beep.ECS.UI.Kit;
using Godot;
using OGSim.Composition;
using OGSim.Kernel;
using OilfieldDays.App;
using OilfieldDays.Host;
using OilfieldDays.World;

namespace OilfieldDays.Screens;

/// <summary>
/// New Game, built to the supplied setup mockups: five steps walked one at a
/// time under the breadcrumb across the top — mode, world setup, company,
/// review, generate — with the settings down the left in named values rather
/// than bare numbers, the seeded preview filling the right under its own
/// legend, and the world's measurements along the bottom.
///
/// <para><b>The five are pages of one screen, not five scenes.</b> Every page
/// reads and writes the same sheet, so routing between scenes would mean five
/// copies of it or a carrier passed along the chain; hiding four groups costs
/// nothing and keeps one control per setting. The breadcrumb is repainted from
/// the page the player is actually on — it used to paint a fixed pattern, which
/// was the screen admitting it was one page wearing five hats.</para>
///
/// <para>Drawn with the Beep UI kit — <see cref="KitPanel"/>, <see cref="KitSlider"/>,
/// <see cref="KitStarRating"/>, <see cref="KitButton"/> — over the theme in
/// <see cref="KitTheme"/>, so the palette lives in one file and a kit widget
/// dropped into any screen arrives already right.</para>
///
/// <para><b>Every control is a world parameter, or it says what it is.</b> §7A.3
/// requires the fields to map onto what the engine accepts and §9.1 requires a
/// refusal to name every reason. Two of the mockup's fields have no engine owner
/// and are shown as readings rather than controls: opening cash belongs to the
/// engine's opening position, and starting reputation is gap G-04.</para>
///
/// <para><b>The measurements are measured.</b> Land and water area are counted
/// off the terrain that was just generated, not estimated from the slider. The
/// mockup's "Estimated Fields" line is absent: the host cannot know how many
/// accumulations a seed holds — that is what drilling is for — and a plausible
/// range printed here would be the exact leak the information model exists to
/// stop. Resource potential is shown as what it is, the richness asked for read
/// back, and not as a survey nobody has run.</para>
/// </summary>
[Tool]
public sealed partial class NewGameSetup : Control
{
	private const float ColumnWidth = 430.0f;

	private const int StageMode = 0;
	private const int StageWorld = 1;
	private const int StageCompany = 2;
	private const int StageReview = 3;
	private const int StageGenerate = 4;

	private static readonly string[] Steps =
	{
		"1  Mode", "2  World Setup", "3  Company", "4  Review", "5  Generate",
	};

	/// <summary>The modes of the flow mockup, and what backs them.</summary>
	private static readonly (string Label, bool Playable, string Backing)[] Modes =
	{
		// The scenario states its own target; this screen composes no engine
		// and so has none to ask (see Host/Goal.cs).
		("Campaign", true, "first-field: hit the scenario target inside ten years, and stay solvent"),
		("Scenario", false, "no second scenario is composed yet"),
		("Sandbox", false, "needs a scenario with no objectives"),
		("Challenge", false, "needs a fixed-seed scenario with a deadline"),
	};

	private static readonly (string Label, int Cells)[] Sizes =
	{
		("Small  (24 x 24 km)", 24),
		("Medium  (32 x 32 km)", 32),
		("Large  (40 x 40 km)", 40),
	};

	private static readonly (string Label, double Severity, double Land)[] Climates =
	{
		("Temperate", 0.28, 0.72),
		("Coastal", 0.40, 0.52),
		("Arid", 0.66, 0.84),
		("Desert", 0.90, 0.93),
		("Sub-arctic", 0.78, 0.66),
	};

	private static readonly (string Label, Era Era, int Year)[] Eras =
	{
		("Era 1  (1960s - 1970s)", Era.E1, 1965),
		("Era 2  (1970s - 1980s)", Era.E2, 1985),
		("Era 3  (1990s - 2000s)", Era.E3, 2005),
		("Era 4  (2010s - today)", Era.E4, 2025),
	};

	/// <summary>Five stars, five richnesses — the control has no other values.</summary>
	private static readonly (double Value, string Word)[] Richness =
	{
		(0.50, "Poor"), (0.75, "Lean"), (1.00, "Normal"), (1.40, "Rich"), (1.90, "Prolific"),
	};

	private static readonly string[] MaturityWords =
	{
		"Frontier", "Early", "Developing", "Mature", "Worked over",
	};

	private static readonly (string Label, int Count)[] Rivals =
	{
		("None", 0), ("Light  (2 rivals)", 2), ("Normal  (4 rivals)", 4), ("Crowded  (7 rivals)", 7),
	};

	private LineEdit _company = null!;
	private LineEdit _seed = null!;
	private OptionButton _mode = null!;
	private OptionButton _size = null!;
	private OptionButton _climate = null!;
	private OptionButton _era = null!;
	private OptionButton _rivals = null!;
	private KitSlider _land = null!;
	private KitSlider _maturity = null!;
	private KitStarRating _richness = null!;
	private Label _landWord = null!;
	private Label _maturityWord = null!;
	private Label _richnessWord = null!;
	private Label _modeNote = null!;
	private Label _problem = null!;
	private Label _seedStamp = null!;
	private Button _generate = null!;
	private Button _next = null!;
	private Button _back = null!;
	private Button _randomize = null!;
	private WorldPreview _preview = null!;
	private Control _previewColumn = null!;
	private PanelContainer _modeGroup = null!;
	private PanelContainer _companyGroup = null!;
	private PanelContainer _worldGroup = null!;
	private PanelContainer _seedGroup = null!;
	private PanelContainer _reviewGroup = null!;
	private PanelContainer _generateGroup = null!;
	private VBoxContainer _modeItems = null!;
	private VBoxContainer _reviewItems = null!;
	private VBoxContainer _generateItems = null!;
	private Label _reviewNote = null!;
	private Label _generateNote = null!;
	private readonly PanelContainer[] _stepPlates = new PanelContainer[Steps.Length];
	private readonly Label[] _stepLabels = new Label[Steps.Length];
	private int _stage;
	private VBoxContainer _stats = null!;
	private VBoxContainer _potential = null!;
	private VBoxContainer _climateNote = null!;
	private PanelContainer _statsRowTemplate = null!;
	private PanelContainer _potentialRowTemplate = null!;
	private PanelContainer _potentialPipsTemplate = null!;
	private Label _potentialTextTemplate = null!;
	private PanelContainer _climateRowTemplate = null!;
	private Label _climateTextTemplate = null!;

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		var ground = RequireNamed<ColorRect>("Ground");
		ground.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		ground.Color = KitTheme.Void;

		var page = RequireNamed<VBoxContainer>("Page");
		page.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		page.OffsetLeft = 20;
		page.OffsetRight = -20;
		page.OffsetTop = 14;
		page.OffsetBottom = -14;
		page.AddThemeConstantOverride("separation", 10);

		BuildHeader(page);

		var body = RequireNamed<HBoxContainer>(page, "Body");
		body.SizeFlagsVertical = SizeFlags.ExpandFill;
		body.AddThemeConstantOverride("separation", 14);

		BuildSettings(body);
		BuildPreview(body);
		BuildFooter(page);

		ShowMode();
		Reseed(20260819UL);

		// A page can be opened directly, the way a screen can (DevOptions).
		_stage = Mathf.Clamp(DevOptions.Stage - 1, StageMode, StageGenerate);

		ShowStage();
	}

	private void BuildHeader(Container parent)
	{
		var row = RequireNamed<HBoxContainer>(parent, "Header");
		row.AddThemeConstantOverride("separation", 10);

		var mark = RequireNamed<TextureRect>(row, "Mark");
		mark.Texture ??= GD.Load<Texture2D>(SlateChrome.LogoPath);
		mark.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		mark.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		mark.CustomMinimumSize = new Vector2(54, 54);

		var title = RequireNamed<Label>(row, "Title");
		title.Text = "NEW GAME";
		title.VerticalAlignment = VerticalAlignment.Center;
		title.AddThemeFontSizeOverride("font_size", 28);
		title.AddThemeColorOverride("font_color", KitTheme.Amber);

		var spacer = RequireNamed<Control>(row, "Spacer");
		spacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;

		// The breadcrumb of the flow mockup, and it says where the player IS.
		// It used to paint a fixed pattern because the screen was one page
		// pretending to be five; the plates are kept and repainted by
		// ShowStage now that walking the five is what the screen does.
		for (int i = 0; i < Steps.Length; i++)
		{
			PanelContainer step = RequireNamed<PanelContainer>(row, $"Step{i + 1}");
			step.CustomMinimumSize = new Vector2(0, 42);

			Label label = RequireNamed<Label>(step, "Label");
			label.AddThemeFontSizeOverride("font_size", 16);
			label.AddThemeColorOverride("font_color", KitTheme.Ink);

			_stepPlates[i] = step;
			_stepLabels[i] = label;
		}
	}

	private void BuildSettings(Container parent)
	{
		var scroll = RequireNamed<ScrollContainer>(parent, "SettingsScroll");
		scroll.CustomMinimumSize = new Vector2(ColumnWidth + 22, 0);
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;

		var column = RequireNamed<VBoxContainer>(scroll, "SettingsColumn");
		column.CustomMinimumSize = new Vector2(ColumnWidth, 0);
		column.AddThemeConstantOverride("separation", 8);

		// STEP 1 IS ITS OWN PAGE. The mode decides which of the rest are even
		// worth asking about, which is why the mockup puts it first and alone.
		VBoxContainer mode = RequireGroup(column, "ModeGroup", "GAME MODE", ColumnWidth, UiSurface.Role.Warning);

		AddField(mode, "ModeField", "Game mode", "control-room-cabin");

		string[] modeLabels = new string[Modes.Length];

		for (int i = 0; i < Modes.Length; i++)
			modeLabels[i] = Modes[i].Playable ? Modes[i].Label : Modes[i].Label + "   (not yet)";

		_mode = BindChoice(mode, "ModeChoice", modeLabels, 0, ColumnWidth - 60);

		for (int i = 0; i < Modes.Length; i++)
			_mode.SetItemDisabled(i, !Modes[i].Playable);

		if (!Godot.Engine.IsEditorHint())
			_mode.ItemSelected += _ => ShowMode();

		_modeNote = BindParagraph(mode, "ModeNote", string.Empty);

		_modeItems = RequireNamed<VBoxContainer>(mode, "Items");
		_modeItems.AddThemeConstantOverride("separation", 4);

		VBoxContainer company = RequireGroup(column, "CompanyGroup", "COMPANY", ColumnWidth, UiSurface.Role.Info);

		AddField(company, "CompanyNameField", "Company name", "main-operations-building");
		_company = BindEntry(company, "CompanyName", "Beep Energy Co.", ColumnWidth - 60);

		AddReadout(company, "StartingCapital", "Starting capital", "the opening position");
		AddReadout(company, "StartingReputation", "Starting reputation", "no engine owner");

		VBoxContainer world = RequireGroup(column, "WorldGroup", "WORLD SETTINGS", ColumnWidth, UiSurface.Role.Success);

		AddField(world, "SizeField", "World size", "helipad-platform");
		_size = BindChoice(world, "SizeChoice", Labels(Sizes), 0, ColumnWidth - 60);
		if (!Godot.Engine.IsEditorHint())
			_size.ItemSelected += _ => RefreshPreview();

		AddField(world, "ClimateField", "Climate profile", "cooling-tower");
		_climate = BindChoice(world, "ClimateChoice", Labels(Climates), 0, ColumnWidth - 60);
		if (!Godot.Engine.IsEditorHint())
			_climate.ItemSelected += _ => OnClimatePicked();

		_landWord = BindCaption(world, "LandWord", "Land / water ratio");
		_land = BindSlider(world, "LandSlider", 0.30, 0.95, 0.01, 0.72, ColumnWidth - 64);
		if (!Godot.Engine.IsEditorHint())
		{
			_land.DragEnded += _ => RefreshPreview();
			_land.ValueChanged += _ => ShowWords();
		}

		_richnessWord = BindCaption(world, "RichnessWord", "Oil & gas richness");
		_richness = BindRating(world, "RichnessRating", 5, 3, UiSurface.Role.Warning, new Vector2(190, 28));
		if (!Godot.Engine.IsEditorHint())
			_richness.Changed += ShowWords;

		_maturityWord = BindCaption(world, "MaturityWord", "Basin maturity");
		_maturity = BindSlider(world, "MaturitySlider", 0.0, 1.0, 0.05, 0.5, ColumnWidth - 64);
		if (!Godot.Engine.IsEditorHint())
			_maturity.ValueChanged += _ => ShowWords();

		AddField(world, "RivalsField", "Third-party industry", "worker-accommodation-cabin");
		_rivals = BindChoice(world, "RivalsChoice", Labels(Rivals), 2, ColumnWidth - 60);

		AddField(world, "EraField", "Starting era", "communications-tower");
		_era = BindChoice(world, "EraChoice", Labels(Eras), 0, ColumnWidth - 60);

		VBoxContainer seed = RequireGroup(column, "SeedGroup", "WORLD SEED", ColumnWidth, UiSurface.Role.Warning);

		var seedRow = RequireNamed<HBoxContainer>(seed, "SeedRow");
		seedRow.AddThemeConstantOverride("separation", 6);

		_seed = BindEntry(seedRow, "SeedEntry", string.Empty, ColumnWidth - 170);
		if (!Godot.Engine.IsEditorHint())
			_seed.TextSubmitted += _ => RefreshPreview();

		Button roll = BindChunk(seedRow, "RollButton", "ROLL", UiSurface.Role.Info, new Vector2(100, 40), 14);
		roll.TooltipText = "Draw a new seed";
		if (!Godot.Engine.IsEditorHint())
			roll.Pressed += () => Reseed(RollSeed());

		var seedButtons = RequireNamed<HBoxContainer>(seed, "SeedButtons");
		seedButtons.AddThemeConstantOverride("separation", 6);

		Button copy = BindChunk(seedButtons, "CopyButton", "COPY", UiSurface.Role.Neutral, new Vector2(120, 38), 14);
		if (!Godot.Engine.IsEditorHint())
			copy.Pressed += () => DisplayServer.ClipboardSet(_seed.Text);

		CheckBox show = RequireNamed<CheckBox>(seedButtons, "ShowSeed");
		show.Text = "Show seed on map";
		show.ButtonPressed = true;
		show.AddThemeFontSizeOverride("font_size", 15);

		if (!Godot.Engine.IsEditorHint())
			show.Toggled += on => _seedStamp.Visible = on;

		BindParagraph(seed, "SeedNote",
			"The same seed always builds the same world. Share it to play the one someone else played.");

		// STEPS 4 AND 5 READ BACK WHAT WAS CHOSEN. Neither takes an input:
		// a review that can be edited in place is a settings page with a
		// different heading, and the point of the step is to look before
		// committing. Both are filled from the same reading of the sheet.
		_reviewItems = RequireGroupItems(
			column, "ReviewGroup", "WORLD SETTINGS SUMMARY", ColumnWidth, UiSurface.Role.Info);
		_reviewNote = BindParagraph(
			RequireNamed<PanelContainer>(column, "ReviewGroup"), "Note", string.Empty);

		_generateItems = RequireGroupItems(
			column, "GenerateGroup", "READY TO GENERATE", ColumnWidth, UiSurface.Role.Success);
		_generateNote = BindParagraph(
			RequireNamed<PanelContainer>(column, "GenerateGroup"), "Note", string.Empty);

		_problem = BindParagraph(column, "Problem", string.Empty);

		_modeGroup = RequireNamed<PanelContainer>(column, "ModeGroup");
		_companyGroup = RequireNamed<PanelContainer>(column, "CompanyGroup");
		_worldGroup = RequireNamed<PanelContainer>(column, "WorldGroup");
		_seedGroup = RequireNamed<PanelContainer>(column, "SeedGroup");
		_reviewGroup = RequireNamed<PanelContainer>(column, "ReviewGroup");
		_generateGroup = RequireNamed<PanelContainer>(column, "GenerateGroup");
	}

	private void BuildPreview(Container parent)
	{
		var right = RequireNamed<VBoxContainer>(parent, "PreviewColumn");
		_previewColumn = right;
		right.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		right.AddThemeConstantOverride("separation", 10);

		var panel = RequireNamed<PanelContainer>(right, "PreviewPanel");
		panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		panel.SizeFlagsVertical = SizeFlags.ExpandFill;
		panel.AddThemeStyleboxOverride("panel", SlateChrome.PanelPlate());

		var previewColumn = RequireNamed<VBoxContainer>(panel, "Content");
		previewColumn.AddThemeConstantOverride("separation", 8);

		var header = RequireNamed<Label>(previewColumn, "Header");
		header.Text = "WORLD PREVIEW";
		SlateChrome.PromoteHeader(header, UiSurface.Role.Success, centered: true);

		var stack = RequireNamed<Control>(previewColumn, "PreviewStack");
		stack.SizeFlagsVertical = SizeFlags.ExpandFill;

		_preview = RequireNamed<WorldPreview>(stack, "WorldPreview");
		_preview.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		_seedStamp = RequireNamed<Label>(stack, "SeedStamp");
		_seedStamp.Text = string.Empty;
		_seedStamp.Position = new Vector2(12, 8);
		_seedStamp.AddThemeFontSizeOverride("font_size", 17);
		_seedStamp.AddThemeColorOverride("font_color", KitTheme.Amber);
		_seedStamp.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 0.9f));
		_seedStamp.AddThemeConstantOverride("shadow_outline_size", 8);

		BuildLegend(stack);

		// The measurements, along the bottom of the mockup.
		var strip = RequireNamed<HBoxContainer>(right, "InfoStrip");
		strip.CustomMinimumSize = new Vector2(0, 200);
		strip.AddThemeConstantOverride("separation", 12);

		_stats = RequireGroupItems(strip, "StatsGroup", "WORLD INFO", 300, UiSurface.Role.Info);
		_potential = RequireGroupItems(strip, "PotentialGroup", "RESOURCE POTENTIAL", 340, UiSurface.Role.Warning);
		_climateNote = RequireGroupItems(strip, "ClimateGroup", "CLIMATE SUMMARY", 320, UiSurface.Role.Info);

		_statsRowTemplate = RequireNamed<PanelContainer>(_stats, "SummaryRowTemplate");
		StyleSummaryRow(_statsRowTemplate, UiSurface.Role.Info);
		_statsRowTemplate.Visible = Godot.Engine.IsEditorHint();

		_potentialPipsTemplate = RequireNamed<PanelContainer>(_potential, "PipsTemplate");
		StylePips(_potentialPipsTemplate, UiSurface.Role.Warning);
		_potentialPipsTemplate.Visible = Godot.Engine.IsEditorHint();

		_potentialRowTemplate = RequireNamed<PanelContainer>(_potential, "SummaryRowTemplate");
		StyleSummaryRow(_potentialRowTemplate, UiSurface.Role.Warning);
		_potentialRowTemplate.Visible = Godot.Engine.IsEditorHint();

		_potentialTextTemplate = RequireNamed<Label>(_potential, "SummaryTextTemplate");
		StyleSummaryText(_potentialTextTemplate);
		_potentialTextTemplate.Visible = Godot.Engine.IsEditorHint();

		_climateRowTemplate = RequireNamed<PanelContainer>(_climateNote, "SummaryRowTemplate");
		StyleSummaryRow(_climateRowTemplate, UiSurface.Role.Info);
		_climateRowTemplate.Visible = Godot.Engine.IsEditorHint();

		_climateTextTemplate = RequireNamed<Label>(_climateNote, "SummaryTextTemplate");
		StyleSummaryText(_climateTextTemplate);
		_climateTextTemplate.Visible = Godot.Engine.IsEditorHint();
	}

	/// <summary>
	/// The map legend of the mockup, cut to what the preview actually draws.
	/// </summary>
	/// <remarks>
	/// The mockup's legend lists prospects, rival HQs and settlements. None are
	/// drawn: no engine exists at setup, so there are no prospects to mark, and a
	/// legend advertising a symbol the map does not carry is a promise the screen
	/// cannot keep. It lists ground, and the ground is what is there.
	/// </remarks>
	private void BuildLegend(Control parent)
	{
		var panel = RequireNamed<PanelContainer>(parent, "LegendPanel");
		panel.CustomMinimumSize = new Vector2(180, 0);
		panel.AddThemeStyleboxOverride("panel", SlateChrome.FieldPlate());
		panel.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
		panel.OffsetRight = -12;
		panel.OffsetBottom = -12;
		panel.GrowHorizontal = GrowDirection.Begin;
		panel.GrowVertical = GrowDirection.Begin;

		var column = RequireNamed<VBoxContainer>(panel, "LegendItems");
		column.AddThemeConstantOverride("separation", 2);

		Key(column, "WaterKey", "Water", new Color(0.22f, 0.44f, 0.63f));
		Key(column, "ShoreKey", "Shore", new Color(0.76f, 0.66f, 0.45f));
		Key(column, "GrassKey", "Grass", new Color(0.31f, 0.55f, 0.26f));
		Key(column, "ScrubKey", "Scrub", new Color(0.52f, 0.51f, 0.28f));
		Key(column, "RockKey", "Rock", new Color(0.47f, 0.47f, 0.49f));
		Key(column, "YardKey", "Yard", new Color(0.62f, 0.45f, 0.28f));
	}

	private static void Key(Container parent, string rowName, string name, Color colour)
	{
		var row = RequireNamed<HBoxContainer>(parent, rowName);
		row.AddThemeConstantOverride("separation", 8);

		var swatch = RequireNamed<ColorRect>(row, "Swatch");
		swatch.Color = colour;
		swatch.CustomMinimumSize = new Vector2(18, 14);

		var label = RequireNamed<Label>(row, "Label");
		label.Text = name;
		label.AddThemeFontSizeOverride("font_size", 14);
		label.AddThemeColorOverride("font_color", KitTheme.Ink);
	}

	private void BuildFooter(Container parent)
	{
		var row = RequireNamed<HBoxContainer>(parent, "Footer");
		row.AddThemeConstantOverride("separation", 12);

		// BACK MEANS ONE STEP BACK, and only leaves the screen from the first
		// one. A wizard whose back button always quit would throw away four
		// pages of choices to correct one of them.
		_back = BindChunk(row, "BackButton", "BACK", UiSurface.Role.Danger, new Vector2(180, 50));
		if (!Godot.Engine.IsEditorHint())
			_back.Pressed += () => Step(-1);

		// Rolling the sheet belongs to the page that shows the sheet.
		_randomize = BindChunk(row, "RandomizeButton", "RANDOMIZE", UiSurface.Role.Info, new Vector2(210, 50));
		if (!Godot.Engine.IsEditorHint())
			_randomize.Pressed += Randomize;

		var spacer = RequireNamed<Control>(row, "FooterSpacer");
		spacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;

		_next = BindChunk(row, "NextButton", "NEXT", UiSurface.Role.Success, new Vector2(320, 50));
		if (!Godot.Engine.IsEditorHint())
			_next.Pressed += () => Step(1);

		_generate = BindChunk(row, "GenerateButton", "GENERATE WORLD", UiSurface.Role.Success, new Vector2(320, 50));
		if (!Godot.Engine.IsEditorHint())
			_generate.Pressed += Generate;
	}

	private static VBoxContainer RequireGroup(
		Container parent, string name, string title, float width, UiSurface.Role header)
	{
		var panel = RequireNamed<PanelContainer>(parent, name);
		panel.CustomMinimumSize = new Vector2(width, 0);
		panel.AddThemeStyleboxOverride("panel", SlateChrome.PanelPlate());

		var content = RequireNamed<VBoxContainer>(panel, "Content");
		content.AddThemeConstantOverride("separation", 8);

		var heading = RequireNamed<Label>(content, "Header");
		heading.Text = title;
		SlateChrome.PromoteHeader(heading, header, centered: true);

		return content;
	}

	private static VBoxContainer RequireGroupItems(
		Container parent, string name, string title, float width, UiSurface.Role header)
	{
		VBoxContainer content = RequireGroup(parent, name, title, width, header);
		var items = RequireNamed<VBoxContainer>(content, "Items");
		items.AddThemeConstantOverride("separation", 4);

		return items;
	}

	private static void AddField(Container parent, string name, string label, string icon)
	{
		var row = RequireNamed<HBoxContainer>(parent, name);
		row.CustomMinimumSize = new Vector2(0, 22);
		row.AddThemeConstantOverride("separation", 8);

		TextureRect image = RequireNamed<TextureRect>(row, "Icon");
		image.Texture = GD.Load<Texture2D>($"res://assets/icons/{icon}.png");
		image.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		image.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		image.CustomMinimumSize = new Vector2(20, 20);

		Label caption = RequireNamed<Label>(row, "Caption");
		caption.Text = label;
		caption.VerticalAlignment = VerticalAlignment.Center;
		caption.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		caption.AddThemeFontSizeOverride("font_size", 14);
		caption.AddThemeColorOverride("font_color", KitTheme.Muted);
	}

	private static Label BindCaption(Container parent, string name, string text)
	{
		Label label = RequireNamed<Label>(parent, name);
		label.Text = text;
		label.AddThemeFontSizeOverride("font_size", 14);
		label.AddThemeColorOverride("font_color", KitTheme.Muted);

		return label;
	}

	private static Label BindParagraph(Container parent, string name, string text)
	{
		Label label = RequireNamed<Label>(parent, name);
		label.Text = text;
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.CustomMinimumSize = new Vector2(180, 0);
		label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		label.AddThemeFontSizeOverride("font_size", 14);
		label.AddThemeColorOverride("font_color", KitTheme.Muted);

		return label;
	}

	private static LineEdit BindEntry(Container parent, string name, string value, float width)
	{
		var entry = RequireNamed<LineEdit>(parent, name);
		entry.Text = value;
		entry.CustomMinimumSize = new Vector2(width, 46);
		entry.AddThemeFontSizeOverride("font_size", 17);
		entry.AddThemeColorOverride("font_color", KitTheme.Ink);
		entry.AddThemeStyleboxOverride("normal", SlateChrome.FieldPlate());
		entry.AddThemeStyleboxOverride("focus", SlateChrome.FieldPlate());

		return entry;
	}

	private static OptionButton BindChoice(
		Container parent, string name, string[] items, int selected, float width)
	{
		var choice = RequireNamed<OptionButton>(parent, name);
		choice.Clear();

		foreach (string item in items)
			choice.AddItem(item);

		choice.Selected = selected;
		choice.CustomMinimumSize = new Vector2(width, 46);
		choice.AddThemeFontSizeOverride("font_size", 17);
		choice.AddThemeColorOverride("font_color", KitTheme.Ink);
		choice.AddThemeColorOverride("font_hover_color", KitTheme.Amber);
		choice.AddThemeStyleboxOverride("normal", SlateChrome.FieldPlate());
		choice.AddThemeStyleboxOverride("hover", SlateChrome.FieldPlate());
		choice.AddThemeStyleboxOverride("pressed", SlateChrome.FieldPlate());
		choice.AddThemeStyleboxOverride("focus", SlateChrome.Nothing);

		return choice;
	}

	private static KitSlider BindSlider(
		Container parent, string name, double min, double max, double step, double value, float width)
	{
		var slider = RequireNamed<KitSlider>(parent, name);
		slider.MinValue = min;
		slider.MaxValue = max;
		slider.Step = step;
		slider.Value = value;
		slider.Fill = UiSurface.Role.Accent;
		slider.CustomMinimumSize = new Vector2(width, 26);

		return slider;
	}

	private static KitStarRating BindRating(
		Container parent, string name, int total, int earned, UiSurface.Role role, Vector2 size)
	{
		var rating = RequireNamed<KitStarRating>(parent, name);
		rating.Total = total;
		rating.Earned = earned;
		rating.Role = role;
		rating.CustomMinimumSize = size;

		return rating;
	}

	private static Button BindChunk(
		Container parent, string name, string text, UiSurface.Role role, Vector2 size, int fontSize = 18)
	{
		var button = RequireNamed<Button>(parent, name);
		SlateChrome.ApplyChunk(button, text, role, size, fontSize);

		return button;
	}

	private static void AddReadout(Container parent, string name, string label, string value)
	{
		var row = RequireNamed<HBoxContainer>(parent, name);
		row.CustomMinimumSize = new Vector2(0, 26);
		row.AddThemeConstantOverride("separation", 8);

		Label left = RequireNamed<Label>(row, "Name");
		left.Text = label;
		left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		left.AddThemeFontSizeOverride("font_size", 15);
		left.AddThemeColorOverride("font_color", KitTheme.Muted);

		Label right = RequireNamed<Label>(row, "Value");
		right.Text = value;
		right.HorizontalAlignment = HorizontalAlignment.Right;
		right.AddThemeFontSizeOverride("font_size", 15);
		right.AddThemeColorOverride("font_color", KitTheme.Amber);
	}

	/// <summary>Roll the whole sheet, not only the seed — the mockup's button.</summary>
	private void Randomize()
	{
		_size.Selected = (int)(GD.Randi() % (uint)Sizes.Length);
		_climate.Selected = (int)(GD.Randi() % (uint)Climates.Length);
		_rivals.Selected = (int)(GD.Randi() % (uint)Rivals.Length);
		_era.Selected = (int)(GD.Randi() % (uint)Eras.Length);
		_richness.Earned = 1 + (int)(GD.Randi() % 5U);
		_maturity.Value = (GD.Randi() % 21U) * 0.05;
		_land.Value = Climates[_climate.Selected].Land;

		Reseed(RollSeed());
	}

	/// <summary>
	/// A climate profile carries a coastline with it, so picking one moves the
	/// land slider to match rather than leaving a desert with a temperate shore.
	/// The slider stays live afterwards: the profile is a starting point, not a
	/// lock.
	/// </summary>
	private void OnClimatePicked()
	{
		_land.Value = Climates[Mathf.Clamp(_climate.Selected, 0, Climates.Length - 1)].Land;
		RefreshPreview();
	}

	private void ShowMode()
	{
		int mode = Mathf.Clamp(_mode.Selected, 0, Modes.Length - 1);
		_modeNote.Text = Modes[mode].Backing;
		_generate.Disabled = !Modes[mode].Playable;

		// WHAT THE OTHER THREE ARE, on the page that asks you to choose one.
		// A dropdown with three disabled entries says they exist and not why
		// they are grey; the list says what each is waiting for.
		Clear(_modeItems);

		for (int i = 0; i < Modes.Length; i++)
		{
			UiSurface.Role role =
				Modes[i].Playable ? UiSurface.Role.Success : UiSurface.Role.Neutral;
			_modeItems.AddChild(SummaryRow(
				_statsRowTemplate, Modes[i].Label,
				Modes[i].Playable ? "playable" : "not yet", role,
				i == mode ? "control-room-cabin" : null));
		}
	}

	/// <summary>
	/// One step forward or back through the five of the mockup.
	/// </summary>
	/// <remarks>
	/// <para><b>BACK leaves the screen only from the first page.</b> A wizard
	/// whose back button always quit would make a player throw away four pages
	/// of choices to correct one of them.</para>
	///
	/// <para><b>FORWARD is refused, not disabled.</b> A greyed-out button says a
	/// player has done something wrong and not what, which on a page of eight
	/// controls is the whole question. Section 9.1 wants the reason named.</para>
	/// </remarks>
	private void Step(int delta)
	{
		if (delta < 0 && _stage == StageMode)
		{
			SceneRouter.Instance.Go(SceneRouter.MainMenu);

			return;
		}

		if (delta > 0 && StageProblem() is string problem)
		{
			Refuse(problem);

			return;
		}

		_problem.Text = string.Empty;
		_problem.AddThemeColorOverride("font_color", KitTheme.Muted);

		_stage = Mathf.Clamp(_stage + delta, StageMode, StageGenerate);

		ShowStage();
	}

	/// <summary>Why this page cannot be left yet, or null when it can.</summary>
	private string? StageProblem() => _stage switch
	{
		StageMode => Modes[Mathf.Clamp(_mode.Selected, 0, Modes.Length - 1)].Playable
			? null
			: "That mode is not composed yet. Campaign is the one this build plays.",

		StageWorld => ulong.TryParse(_seed.Text.Trim(), out _)
			? null
			: "A seed is a whole number. Two players comparing runs need the same one.",

		StageCompany => _company.Text.Trim().Length == 0
			? "A company needs a name. It goes on every ledger line the run writes."
			: null,

		_ => null,
	};

	/// <summary>
	/// Show the page the player is on, and only that page.
	/// </summary>
	/// <remarks>
	/// The screen holds all five and hides four rather than routing to five
	/// scenes: every page reads and writes the same sheet, and five scenes would
	/// mean five copies of it or a carrier passed between them. What the player
	/// chose on page two is still the same control on page four.
	/// </remarks>
	private void ShowStage()
	{
		_modeGroup.Visible = _stage == StageMode;
		_companyGroup.Visible = _stage == StageCompany;
		_worldGroup.Visible = _stage == StageWorld;
		_seedGroup.Visible = _stage == StageWorld;
		_reviewGroup.Visible = _stage == StageReview;
		_generateGroup.Visible = _stage == StageGenerate;

		// THE MAP STAYS UP ON EVERY PAGE. It was hidden on mode and company on
		// the grounds that neither is about the ground — which was true, and
		// left two of the five pages as a small panel against an empty half
		// screen. The mockup draws something on the right of every step, and a
		// player picking a company name is still making THIS world.
		_previewColumn.Visible = true;
		_randomize.Visible = _stage == StageWorld;

		_back.Text = _stage == StageMode ? "BACK TO MENU" : "BACK";
		_next.Visible = _stage != StageGenerate;
		_generate.Visible = _stage == StageGenerate;

		// The button says where it goes, as the mockup's does.
		if (_stage != StageGenerate)
			_next.Text = "NEXT:  " + Steps[_stage + 1][3..].ToUpperInvariant();

		for (int i = 0; i < Steps.Length; i++)
		{
			UiSurface.Role role = i < _stage
				? UiSurface.Role.Success
				: i == _stage ? UiSurface.Role.Warning : UiSurface.Role.Neutral;

			_stepPlates[i].AddThemeStyleboxOverride("panel", SlateChrome.RolePlate(role));
			_stepLabels[i].Text = i < _stage ? Steps[i] + "  ✓" : Steps[i];
		}

		if (_stage is StageReview or StageGenerate)
			FillSummary();
	}

	/// <summary>
	/// The sheet, read back on the two pages that only read it.
	/// </summary>
	/// <remarks>
	/// One reading feeding both, because review and generate summarise the same
	/// decisions and two readings would be two chances to drift apart from the
	/// controls they are describing.
	/// </remarks>
	private void FillSummary()
	{
		int mode = Mathf.Clamp(_mode.Selected, 0, Modes.Length - 1);
		int size = Mathf.Clamp(_size.Selected, 0, Sizes.Length - 1);
		int climate = Mathf.Clamp(_climate.Selected, 0, Climates.Length - 1);
		int rivals = Mathf.Clamp(_rivals.Selected, 0, Rivals.Length - 1);
		int era = Mathf.Clamp(_era.Selected, 0, Eras.Length - 1);

		int step = Mathf.Clamp(
			(int)((_maturity.Value * (MaturityWords.Length - 1)) + 0.5), 0, MaturityWords.Length - 1);

		(string Label, string Value, UiSurface.Role Role, string Icon)[] sheet =
		{
			("Game mode", Modes[mode].Label, UiSurface.Role.Warning, "control-room-cabin"),
			("Company", Company(), UiSurface.Role.Info, "main-operations-building"),
			("World size", Sizes[size].Label, UiSurface.Role.Info, "helipad-platform"),
			("Climate", Climates[climate].Label, UiSurface.Role.Info, "cooling-tower"),
			("Land / water", $"{_land.Value * 100.0:F0}% land", UiSurface.Role.Success, "produced-water-pond"),
			("Oil and gas richness", Richness[Star() - 1].Word, UiSurface.Role.Warning, "pumpjack"),
			("Basin maturity", MaturityWords[step], UiSurface.Role.Neutral, "pipe-rack-section"),
			("Third-party industry", Rivals[rivals].Label, UiSurface.Role.Neutral, "worker-accommodation-cabin"),
			("Starting era", Eras[era].Label, UiSurface.Role.Info, "communications-tower"),
			("World seed", _seed.Text.Trim(), UiSurface.Role.Warning, "gas-detector-station"),
		};

		VBoxContainer into = _stage == StageReview ? _reviewItems : _generateItems;

		Clear(into);

		for (int i = 0; i < sheet.Length; i++)
			into.AddChild(SummaryRow(
				_statsRowTemplate, sheet[i].Label, sheet[i].Value, sheet[i].Role, sheet[i].Icon));

		_reviewNote.Text =
			"Nothing is built yet. Step back to change any of it. The same seed always"
			+ " builds the same world, so this sheet is the whole of what makes this run.";

		_generateNote.Text =
			"Generating builds the world and starts the run. It takes a moment.";
	}

	/// <summary>The name on the ledger, defaulted where the player left it blank.</summary>
	private string Company() =>
		_company.Text.Trim().Length == 0 ? "Beep Energy Co." : _company.Text.Trim();

	private void ShowWords()
	{
		_landWord.Text = $"Land / water ratio        {_land.Value * 100.0:F0}% land";
		_richnessWord.Text = $"Oil & gas richness        {Richness[Star() - 1].Word}";

		int step = Mathf.Clamp(
			(int)((_maturity.Value * (MaturityWords.Length - 1)) + 0.5), 0, MaturityWords.Length - 1);

		_maturityWord.Text = $"Basin maturity        {MaturityWords[step]}";

		ShowPotential();
	}

	private int Star() => Mathf.Clamp(_richness.Earned, 1, Richness.Length);

	private void Reseed(ulong seed)
	{
		_seed.Text = seed.ToString(CultureInfo.InvariantCulture);
		RefreshPreview();
	}

	/// <summary>
	/// A seed drawn where drawing is allowed: the client, before a session
	/// exists. The engine's eight streams are never asked for it, and after this
	/// point nothing in the run is random again (§7A.2).
	/// </summary>
	private static ulong RollSeed() => ((ulong)GD.Randi() << 32) | GD.Randi();

	private void RefreshPreview()
	{
		ShowWords();

		if (!ulong.TryParse(_seed.Text.Trim(), out ulong seed))
		{
			_problem.Text = "A seed is a whole number. Two players comparing runs need the same one.";
			return;
		}

		_problem.Text = string.Empty;

		int cells = Sizes[Mathf.Clamp(_size.Selected, 0, Sizes.Length - 1)].Cells;
		(string _, double severity, double _) = Climates[Mathf.Clamp(_climate.Selected, 0, Climates.Length - 1)];

		_preview.Bind(seed, cells, _land.Value, severity);
		_seedStamp.Text = $"SEED {seed.ToString(CultureInfo.InvariantCulture)}";

		ShowStats(cells);
		ShowClimate();
	}

	/// <summary>
	/// The world's measurements — counted off the ground that was just built.
	/// </summary>
	private void ShowStats(int cells)
	{
		Clear(_stats);

		TerrainMap? terrain = _preview.Terrain;

		if (terrain is null)
			return;

		int water = 0;
		int rock = 0;
		int scrub = 0;
		int tiles = terrain.Size;

		for (int y = 0; y < tiles; y++)
		{
			for (int x = 0; x < tiles; x++)
			{
				var cell = new Vector2I(x, y);
				Ground ground = terrain.At(cell);

				if (ground == Ground.Water)
					water++;
				else if (ground == Ground.Rock)
					rock++;
				else if (terrain.IsDry(cell))
					scrub++;
			}
		}

		int total = tiles * tiles;
		double areaPerTile = (double)cells * cells / total;

		_stats.AddChild(SummaryRow(_statsRowTemplate, "Basin", $"{cells} x {cells} km", UiSurface.Role.Info, "helipad-platform"));
		_stats.AddChild(SummaryRow(_statsRowTemplate, "Land area", $"{(total - water) * areaPerTile:N0} km2", UiSurface.Role.Success, "site-lighting-tower"));
		_stats.AddChild(SummaryRow(_statsRowTemplate, "Water area", $"{water * areaPerTile:N0} km2", UiSurface.Role.Info, "produced-water-pond"));
		_stats.AddChild(SummaryRow(_statsRowTemplate, "High ground", $"{rock * areaPerTile:N0} km2", UiSurface.Role.Neutral, "pipe-rack-section"));
		_stats.AddChild(SummaryRow(_statsRowTemplate, "Dry country", $"{scrub * areaPerTile:N0} km2", UiSurface.Role.Warning, "frac-tank"));
	}

	/// <summary>
	/// Resource potential — the richness that was asked for, read back.
	/// </summary>
	/// <remarks>
	/// Not a survey. The mockup shows oil, gas and NGL potential as three
	/// separate meters, which would imply the host had looked; it has not, and
	/// the engine will not say until something is drilled. One meter, labelled as
	/// the setting it echoes, is the whole of what is known here.
	/// </remarks>
	private void ShowPotential()
	{
		if (_potential is null)
			return;

		Clear(_potential);

		_potential.AddChild(Pips(_potentialPipsTemplate, "Richness asked for", Star(), UiSurface.Role.Warning));
		_potential.AddChild(SummaryRow(_potentialRowTemplate, "World generation", Richness[Star() - 1].Word, UiSurface.Role.Warning, "pumpjack"));

		_potential.AddChild(SummaryText(_potentialTextTemplate,
			"This is the setting, not a survey. What is down there, and where, is not known to anyone " +
			"until the company measures it."));
	}

	private void ShowClimate()
	{
		Clear(_climateNote);

		(string label, double severity, double _) = Climates[Mathf.Clamp(_climate.Selected, 0, Climates.Length - 1)];

		_climateNote.AddChild(SummaryRow(_climateRowTemplate, "Profile", label, UiSurface.Role.Info, "cooling-tower"));
		_climateNote.AddChild(SummaryRow(_climateRowTemplate, "Severity", severity.ToString("F2", CultureInfo.InvariantCulture), UiSurface.Role.Info, "gas-detector-station"));

		_climateNote.AddChild(SummaryText(_climateTextTemplate, severity switch
		{
			< 0.35 => "Green country and steady weather. Most of the basin holds grass.",
			< 0.55 => "Mixed country with a wet season. Some of it burns off in summer.",
			< 0.75 => "Hard, dry country. Scrub over most of it and little standing water.",
			_ => "Desert. Almost nothing green, and the weather is against the work.",
		}));
	}

	private void Generate()
	{
		if (!ulong.TryParse(_seed.Text.Trim(), out ulong seed))
		{
			Refuse("A seed is a whole number. Two players comparing runs need the same one.");
			return;
		}

		int climate = Mathf.Clamp(_climate.Selected, 0, Climates.Length - 1);
		int era = Mathf.Clamp(_era.Selected, 0, Eras.Length - 1);

		var draft = new EngineHost.NewGameDraft(
			Seed: seed,
			Mode: DevOptions.Mode ?? GameStyles.Days.Id.Value,
			WorldTemplate: "world-template-basin",
			Cells: Sizes[Mathf.Clamp(_size.Selected, 0, Sizes.Length - 1)].Cells,
			LandFraction: _land.Value,
			ResourceRichness: Richness[Star() - 1].Value,
			BasinMaturity: _maturity.Value,
			ClimateSeverity: Climates[climate].Severity,
			RivalCount: Rivals[Mathf.Clamp(_rivals.Selected, 0, Rivals.Length - 1)].Count,
			StartEra: Eras[era].Era)
		{
			CompanyName = _company.Text.Trim().Length == 0 ? "Beep Energy Co." : _company.Text.Trim(),
			StartYear = Eras[era].Year,
		};

		if (!EngineHost.Instance.NewGame(draft))
		{
			// Every reason, not the first. §9.1: a rejection shows the whole
			// sheet, because a player told one of four problems fixes one of four.
			Refuse("The engine refused to start:\n- " + string.Join("\n- ", EngineHost.Instance.StartupProblems));
			return;
		}

		SceneRouter.Instance.Go(SceneRouter.Gameplay);
	}

	private void Refuse(string message)
	{
		_problem.Text = message;
		_problem.AddThemeColorOverride("font_color", KitTheme.Red);
	}

	private static PanelContainer SummaryRow(PanelContainer template, string label, string value, UiSurface.Role role, string? icon = null)
	{
		var row = (PanelContainer)template.Duplicate();
		row.Name = "SummaryRow";
		row.Visible = true;
		StyleSummaryRow(row, role);

		TextureRect art = RequireNamed<TextureRect>(row, "Icon");
		art.Visible = icon is not null;

		if (icon is not null)
			art.Texture = GD.Load<Texture2D>($"res://assets/icons/{icon}.png");

		RequireNamed<Label>(row, "Label").Text = label;
		RequireNamed<Label>(row, "Value").Text = value;

		return row;
	}

	private static Label SummaryText(Label template, string text)
	{
		var label = (Label)template.Duplicate();
		label.Name = "SummaryText";
		label.Visible = true;
		label.Text = text;
		StyleSummaryText(label);
		return label;
	}

	private static PanelContainer Pips(PanelContainer template, string label, int filled, UiSurface.Role role)
	{
		var row = (PanelContainer)template.Duplicate();
		row.Name = "Pips";
		row.Visible = true;
		StylePips(row, role);
		RequireNamed<Label>(row, "Label").Text = label;

		for (int i = 1; i <= 5; i++)
		{
			Label pip = RequireNamed<Label>(row, $"Pip{i}");
			pip.Text = i <= filled ? "●" : "○";
			pip.AddThemeColorOverride("font_color", i <= filled ? RoleColour(role) : KitTheme.Muted);
		}

		return row;
	}

	private static void StyleSummaryRow(PanelContainer row, UiSurface.Role role)
	{
		row.AddThemeStyleboxOverride("panel", SlateChrome.FieldPlate());

		HBoxContainer line = RequireNamed<HBoxContainer>(row, "Line");
		line.AddThemeConstantOverride("separation", 8);

		TextureRect icon = RequireNamed<TextureRect>(row, "Icon");
		icon.CustomMinimumSize = new Vector2(24, 24);
		icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;

		Label label = RequireNamed<Label>(row, "Label");
		label.AddThemeFontSizeOverride("font_size", 13);
		label.AddThemeColorOverride("font_color", KitTheme.Muted);

		Label value = RequireNamed<Label>(row, "Value");
		value.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		value.HorizontalAlignment = HorizontalAlignment.Right;
		value.AddThemeFontSizeOverride("font_size", 13);
		value.AddThemeColorOverride("font_color", RoleColour(role));
	}

	private static void StyleSummaryText(Label label)
	{
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.CustomMinimumSize = new Vector2(180, 0);
		label.AddThemeFontSizeOverride("font_size", 13);
		label.AddThemeColorOverride("font_color", KitTheme.Muted);
	}

	private static void StylePips(PanelContainer row, UiSurface.Role role)
	{
		row.AddThemeStyleboxOverride("panel", SlateChrome.FieldPlate());

		HBoxContainer line = RequireNamed<HBoxContainer>(row, "Line");
		line.AddThemeConstantOverride("separation", 8);

		Label label = RequireNamed<Label>(row, "Label");
		label.AddThemeFontSizeOverride("font_size", 13);
		label.AddThemeColorOverride("font_color", KitTheme.Muted);

		HBoxContainer pips = RequireNamed<HBoxContainer>(row, "Pips");
		pips.AddThemeConstantOverride("separation", 2);

		for (int i = 1; i <= 5; i++)
		{
			Label pip = RequireNamed<Label>(row, $"Pip{i}");
			pip.AddThemeFontSizeOverride("font_size", 13);
			pip.AddThemeColorOverride("font_color", RoleColour(role));
		}
	}

	private static Color RoleColour(UiSurface.Role role) => role switch
	{
		UiSurface.Role.Success => KitTheme.Green.Lightened(0.35f),
		UiSurface.Role.Warning => KitTheme.Amber,
		UiSurface.Role.Danger => KitTheme.Red.Lightened(0.35f),
		UiSurface.Role.Info => KitTheme.Sky,
		_ => KitTheme.Ink,
	};

	private static void Clear(Container container)
	{
		foreach (Node child in container.GetChildren())
		{
			if (child.Name.ToString().EndsWith("Template", StringComparison.Ordinal))
				continue;

			container.RemoveChild(child);
			child.QueueFree();
		}
	}

	private static string[] Labels((string Label, int Cells)[] rows)
	{
		string[] labels = new string[rows.Length];

		for (int i = 0; i < rows.Length; i++)
			labels[i] = rows[i].Label;

		return labels;
	}

	private static string[] Labels((string Label, double Severity, double Land)[] rows)
	{
		string[] labels = new string[rows.Length];

		for (int i = 0; i < rows.Length; i++)
			labels[i] = rows[i].Label;

		return labels;
	}

	private static string[] Labels((string Label, Era Era, int Year)[] rows)
	{
		string[] labels = new string[rows.Length];

		for (int i = 0; i < rows.Length; i++)
			labels[i] = rows[i].Label;

		return labels;
	}

	private T? FindNamed<T>(string name) where T : Node => FindNamed<T>(this, name);

	private T RequireNamed<T>(string name) where T : Node => RequireNamed<T>(this, name);

	private static T RequireNamed<T>(Node root, string name) where T : Node
	{
		return FindNamed<T>(root, name) ??
			throw new InvalidOperationException(
				$"{root.GetPath()} requires an authored {typeof(T).Name} named '{name}'.");
	}

	private static T? FindNamed<T>(Node root, string name) where T : Node
	{
		if (root is T self && root.Name == name)
			return self;

		foreach (Node child in root.GetChildren())
		{
			T? found = FindNamed<T>(child, name);

			if (found is not null)
				return found;
		}

		return null;
	}
}
