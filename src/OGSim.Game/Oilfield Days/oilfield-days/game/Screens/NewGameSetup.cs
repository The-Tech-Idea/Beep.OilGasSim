#nullable enable

using System;
using System.Globalization;
using Beep.ECS.UI;
using Beep.ECS.UI.Kit;
using Godot;
using OGSim.Kernel;
using OilfieldDays.App;
using OilfieldDays.Host;
using OilfieldDays.World;

namespace OilfieldDays.Screens;

/// <summary>
/// New Game and world setup, built to the supplied setup mockups: the five-step
/// breadcrumb across the top, the world settings down the left with named values
/// rather than bare numbers, the seeded preview filling the right under its own
/// legend, and the world's measurements along the bottom.
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
public sealed partial class NewGameSetup : Control
{
    private const float ColumnWidth = 430.0f;

    private static readonly string[] Steps =
    {
        "1  Mode", "2  World Setup", "3  Company", "4  Review", "5  Generate",
    };

    /// <summary>The modes of the flow mockup, and what backs them.</summary>
    private static readonly (string Label, bool Playable, string Backing)[] Modes =
    {
        ("Campaign", true, "first-field: reach $600M inside ten years, and stay solvent"),
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
    private WorldPreview _preview = null!;
    private VBoxContainer _stats = null!;
    private VBoxContainer _potential = null!;
    private VBoxContainer _climateNote = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var ground = new ColorRect { Color = KitTheme.Void };
        ground.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(ground);

        var page = new VBoxContainer();
        page.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        page.OffsetLeft = 20;
        page.OffsetRight = -20;
        page.OffsetTop = 14;
        page.OffsetBottom = -14;
        page.AddThemeConstantOverride("separation", 10);
        AddChild(page);

        BuildHeader(page);

        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 14);
        page.AddChild(body);

        BuildSettings(body);
        BuildPreview(body);
        BuildFooter(page);

        ShowMode();
        Reseed(20260819UL);
    }

    private void BuildHeader(Container parent)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        parent.AddChild(row);

        var mark = new TextureRect
        {
            Texture = GD.Load<Texture2D>(SlateChrome.LogoPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(54, 54),
        };

        row.AddChild(mark);
        row.AddChild(SlateChrome.Heading("NEW GAME"));
        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        // The breadcrumb of the flow mockup. This build fills in one page rather
        // than five, so the steps it does not walk through are shown as reached
        // rather than pending — every one of their settings is on this screen.
        for (int i = 0; i < Steps.Length; i++)
            row.AddChild(SlateChrome.StepChip(Steps[i], i < 2 ? -1 : i == 2 ? 1 : 0));
    }

    private void BuildSettings(Container parent)
    {
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(ColumnWidth + 22, 0),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };

        parent.AddChild(scroll);

        var column = new VBoxContainer { CustomMinimumSize = new Vector2(ColumnWidth, 0) };
        column.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(column);

        VBoxContainer company = SlateChrome.Group("COMPANY", column, ColumnWidth, UiSurface.Role.Info);

        company.AddChild(Field("Company name", "main-operations-building"));
        _company = SlateChrome.Entry("Beep Energy Co.", ColumnWidth - 60);
        company.AddChild(_company);

        company.AddChild(Field("Game mode", "control-room-cabin"));

        string[] modeLabels = new string[Modes.Length];

        for (int i = 0; i < Modes.Length; i++)
            modeLabels[i] = Modes[i].Playable ? Modes[i].Label : Modes[i].Label + "   (not yet)";

        _mode = SlateChrome.Choice(modeLabels, 0, ColumnWidth - 60);

        for (int i = 0; i < Modes.Length; i++)
            _mode.SetItemDisabled(i, !Modes[i].Playable);

        _mode.ItemSelected += _ => ShowMode();
        company.AddChild(_mode);

        _modeNote = Wrapped(company, string.Empty);

        company.AddChild(SlateChrome.Row2("Starting capital", "the opening position", UiSurface.Role.Neutral, "crude-oil-storage-tank"));
        company.AddChild(SlateChrome.Row2("Starting reputation", "no engine owner", UiSurface.Role.Neutral, "security-checkpoint"));

        VBoxContainer world = SlateChrome.Group("WORLD SETTINGS", column, ColumnWidth, UiSurface.Role.Success);

        world.AddChild(Field("World size", "helipad-platform"));
        _size = SlateChrome.Choice(Labels(Sizes), 0, ColumnWidth - 60);
        _size.ItemSelected += _ => RefreshPreview();
        world.AddChild(_size);

        world.AddChild(Field("Climate profile", "cooling-tower"));
        _climate = SlateChrome.Choice(Labels(Climates), 0, ColumnWidth - 60);
        _climate.ItemSelected += _ => OnClimatePicked();
        world.AddChild(_climate);

        _landWord = SlateChrome.Caption("Land / water ratio");
        world.AddChild(Beside(_landWord, "produced-water-pond"));
        _land = SlateChrome.Bar(0.30, 0.95, 0.01, 0.72, ColumnWidth - 64);
        _land.DragEnded += _ => RefreshPreview();
        _land.ValueChanged += _ => ShowWords();
        world.AddChild(_land);

        _richnessWord = SlateChrome.Caption("Oil & gas richness");
        world.AddChild(Beside(_richnessWord, "pumpjack"));

        _richness = new KitStarRating
        {
            Total = 5,
            Earned = 3,
            Role = UiSurface.Role.Warning,
            CustomMinimumSize = new Vector2(190, 28),
        };

        _richness.Changed += ShowWords;
        world.AddChild(_richness);

        _maturityWord = SlateChrome.Caption("Basin maturity");
        world.AddChild(Beside(_maturityWord, "drilling-rig-derrick"));
        _maturity = SlateChrome.Bar(0.0, 1.0, 0.05, 0.5, ColumnWidth - 64);
        _maturity.ValueChanged += _ => ShowWords();
        world.AddChild(_maturity);

        world.AddChild(Field("Third-party industry", "worker-accommodation-cabin"));
        _rivals = SlateChrome.Choice(Labels(Rivals), 2, ColumnWidth - 60);
        world.AddChild(_rivals);

        world.AddChild(Field("Starting era", "communications-tower"));
        _era = SlateChrome.Choice(Labels(Eras), 0, ColumnWidth - 60);
        world.AddChild(_era);

        VBoxContainer seed = SlateChrome.Group("WORLD SEED", column, ColumnWidth, UiSurface.Role.Warning);

        var seedRow = new HBoxContainer();
        seedRow.AddThemeConstantOverride("separation", 6);

        _seed = SlateChrome.Entry(string.Empty, ColumnWidth - 170);
        _seed.TextSubmitted += _ => RefreshPreview();
        seedRow.AddChild(_seed);

        Button roll = SlateChrome.Chunk("ROLL", UiSurface.Role.Info, new Vector2(100, 40), fontSize: 14);
        roll.TooltipText = "Draw a new seed";
        roll.Pressed += () => Reseed(RollSeed());
        seedRow.AddChild(roll);

        seed.AddChild(seedRow);

        var seedButtons = new HBoxContainer();
        seedButtons.AddThemeConstantOverride("separation", 6);

        Button copy = SlateChrome.Chunk("COPY", UiSurface.Role.Neutral, new Vector2(120, 38), fontSize: 14);
        copy.Pressed += () => DisplayServer.ClipboardSet(_seed.Text);
        seedButtons.AddChild(copy);

        CheckBox show = SlateChrome.Tick("Show seed on map", true);
        show.Toggled += on => _seedStamp.Visible = on;
        seedButtons.AddChild(show);

        seed.AddChild(seedButtons);

        Wrapped(seed, "The same seed always builds the same world. Share it to play the one someone else played.");

        _problem = Wrapped(column, string.Empty);
    }

    private void BuildPreview(Container parent)
    {
        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 10);
        parent.AddChild(right);

        Container inset = SlateChrome.Frame(new Vector2(0, 0), "WORLD PREVIEW", UiSurface.Role.Success);
        Control panel = SlateChrome.PanelOf(inset);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;
        right.AddChild(panel);

        var stack = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
        inset.AddChild(stack);

        _preview = new WorldPreview();
        _preview.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        stack.AddChild(_preview);

        _seedStamp = SlateChrome.Line(string.Empty, 17, KitTheme.Amber);
        _seedStamp.Position = new Vector2(12, 8);
        _seedStamp.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 0.9f));
        _seedStamp.AddThemeConstantOverride("shadow_outline_size", 8);
        stack.AddChild(_seedStamp);

        BuildLegend(stack);

        // The measurements, along the bottom of the mockup.
        var strip = new HBoxContainer { CustomMinimumSize = new Vector2(0, 200) };
        strip.AddThemeConstantOverride("separation", 12);
        right.AddChild(strip);

        _stats = SlateChrome.Group("WORLD INFO", strip, 300, UiSurface.Role.Info);
        _potential = SlateChrome.Group("RESOURCE POTENTIAL", strip, 340, UiSurface.Role.Warning);
        _climateNote = SlateChrome.Group("CLIMATE SUMMARY", strip, 320, UiSurface.Role.Info);
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
        Container inset = SlateChrome.Frame(new Vector2(180, 0), "GROUND");
        Control panel = SlateChrome.PanelOf(inset);
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
        panel.OffsetRight = -12;
        panel.OffsetBottom = -12;

        // Grow towards the corner it is pinned to. Left at the default the panel
        // expands rightwards off the edge of the map whenever its own minimum
        // width — banner included — is wider than the offsets asked for.
        panel.GrowHorizontal = GrowDirection.Begin;
        panel.GrowVertical = GrowDirection.Begin;
        parent.AddChild(panel);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 2);
        inset.AddChild(column);

        Key(column, "Water", new Color(0.22f, 0.44f, 0.63f));
        Key(column, "Shore", new Color(0.76f, 0.66f, 0.45f));
        Key(column, "Grass", new Color(0.31f, 0.55f, 0.26f));
        Key(column, "Scrub", new Color(0.52f, 0.51f, 0.28f));
        Key(column, "Rock", new Color(0.47f, 0.47f, 0.49f));
        Key(column, "Yard", new Color(0.62f, 0.45f, 0.28f));
    }

    private static void Key(Container parent, string name, Color colour)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        var swatch = new ColorRect { Color = colour, CustomMinimumSize = new Vector2(18, 14) };
        row.AddChild(swatch);
        row.AddChild(SlateChrome.Line(name, 14, KitTheme.Ink));

        parent.AddChild(row);
    }

    private void BuildFooter(Container parent)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        parent.AddChild(row);

        Button back = SlateChrome.Chunk("BACK", UiSurface.Role.Danger, new Vector2(180, 50));
        back.Pressed += () => SceneRouter.Instance.Go(SceneRouter.MainMenu);
        row.AddChild(back);

        Button randomize = SlateChrome.Chunk("RANDOMIZE", UiSurface.Role.Info, new Vector2(210, 50));
        randomize.Pressed += Randomize;
        row.AddChild(randomize);

        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        _generate = SlateChrome.Chunk("GENERATE WORLD", UiSurface.Role.Success, new Vector2(320, 50));
        _generate.Pressed += Generate;
        row.AddChild(_generate);
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
    }

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

        _stats.AddChild(SlateChrome.Row2("Basin", $"{cells} x {cells} km", icon: "helipad-platform"));
        _stats.AddChild(SlateChrome.Row2("Land area", $"{(total - water) * areaPerTile:N0} km2", icon: "site-lighting-tower"));
        _stats.AddChild(SlateChrome.Row2("Water area", $"{water * areaPerTile:N0} km2", UiSurface.Role.Info, "produced-water-pond"));
        _stats.AddChild(SlateChrome.Row2("High ground", $"{rock * areaPerTile:N0} km2", UiSurface.Role.Neutral, "pipe-rack-section"));
        _stats.AddChild(SlateChrome.Row2("Dry country", $"{scrub * areaPerTile:N0} km2", UiSurface.Role.Warning, "frac-tank"));
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

        _potential.AddChild(SlateChrome.Pips("Richness asked for", Star(), UiSurface.Role.Warning));
        _potential.AddChild(SlateChrome.Row2("World generation", Richness[Star() - 1].Word, icon: "pumpjack"));

        Wrapped(_potential,
            "This is the setting, not a survey. What is down there, and where, is not known to anyone " +
            "until the company measures it.");
    }

    private void ShowClimate()
    {
        Clear(_climateNote);

        (string label, double severity, double _) = Climates[Mathf.Clamp(_climate.Selected, 0, Climates.Length - 1)];

        _climateNote.AddChild(SlateChrome.Row2("Profile", label, icon: "cooling-tower"));
        _climateNote.AddChild(SlateChrome.Row2("Severity", severity.ToString("F2", CultureInfo.InvariantCulture), UiSurface.Role.Info, "gas-detector-station"));

        Wrapped(_climateNote, severity switch
        {
            < 0.35 => "Green country and steady weather. Most of the basin holds grass.",
            < 0.55 => "Mixed country with a wet season. Some of it burns off in summer.",
            < 0.75 => "Hard, dry country. Scrub over most of it and little standing water.",
            _ => "Desert. Almost nothing green, and the weather is against the work.",
        });
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
            RealityProfile: "arcade",
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

    /// <summary>A setting's caption, with the icon the sheets put beside it.</summary>
    private static Control Field(string label, string icon) => Beside(SlateChrome.Caption(label), icon);

    private static Control Beside(Label caption, string icon)
    {
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 22) };
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(SlateChrome.Icon(icon, 20.0f));

        caption.VerticalAlignment = VerticalAlignment.Center;
        caption.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(caption);

        return row;
    }

    private static Label Wrapped(Container parent, string text)
    {
        Label label = SlateChrome.Paragraph(text, 180.0f);
        parent.AddChild(label);

        return label;
    }

    private static void Clear(Container container)
    {
        foreach (Node child in container.GetChildren())
        {
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
}

/// <summary>
/// The seeded ground, painted — the preview panel of the setup mockups.
///
/// <para>Not a diagram of the world: <b>the world</b>. It builds a real
/// <see cref="BasinWorld"/> from the seed and knobs on screen, into an offscreen
/// viewport with the camera pulled back to hold the whole basin, so the preview
/// is drawn by the same tilesets, autotiling and scatter the game will use.</para>
///
/// <para>It is surface only because there is nothing else to draw. No engine
/// exists at setup, so <see cref="BasinWorld.PaintBareGround"/> runs with no
/// prospects and no wells — which is what §7A.4 asks for, arrived at by having no
/// subsurface rather than by hiding one.</para>
/// </summary>
public sealed partial class WorldPreview : Control
{
    private SubViewport _viewport = null!;
    private TextureRect _screen = null!;
    private Camera2D _camera = null!;
    private BasinWorld? _basin;

    private ulong _seed;
    private int _cells;
    private double _land;
    private double _climate;
    private bool _pending;

    /// <summary>The ground that was built, for the screen to measure.</summary>
    public TerrainMap? Terrain => _basin?.Terrain;

    public override void _Ready()
    {
        ClipContents = true;

        _viewport = new SubViewport
        {
            Size = new Vector2I(960, 720),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            TransparentBg = false,
        };

        AddChild(_viewport);

        // Deep water behind the basin, so the panel is not letterboxed in grey
        // where a square world does not reach the corners of a wide frame.
        var sea = new CanvasLayer { Layer = -100 };
        _viewport.AddChild(sea);

        var fill = new ColorRect { Color = new Color(0.13f, 0.30f, 0.45f) };
        fill.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        sea.AddChild(fill);

        _camera = new Camera2D { Enabled = true };
        _viewport.AddChild(_camera);

        // Scale, not cover: the viewport is resized to the panel's own shape, so
        // the picture already has the right aspect and cropping it would only
        // throw away coastline.
        _screen = new TextureRect
        {
            Texture = _viewport.GetTexture(),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
        };

        _screen.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_screen);

        Resized += Fit;
        Fit();

        if (_pending)
            Repaint();
    }

    /// <summary>Match the offscreen viewport to the panel it is shown in.</summary>
    private void Fit()
    {
        var wanted = new Vector2I(
            Mathf.Max(64, (int)Size.X),
            Mathf.Max(64, (int)Size.Y));

        if (_viewport.Size == wanted)
            return;

        _viewport.Size = wanted;
        Frame();
    }

    /// <summary>
    /// Put the whole basin in shot. The smaller of the two fits, so a square
    /// basin in a wide panel keeps its coasts rather than having them cropped.
    /// </summary>
    private void Frame()
    {
        if (_basin is null)
            return;

        _camera.Position = _basin.Extent * 0.5f;

        float fit = Mathf.Min(_viewport.Size.X / _basin.Extent.X, _viewport.Size.Y / _basin.Extent.Y);
        _camera.Zoom = Vector2.One * (fit * 0.98f);
    }

    public void Bind(ulong seed, int cells, double landFraction, double climateSeverity)
    {
        _seed = seed;
        _cells = cells;
        _land = landFraction;
        _climate = climateSeverity;

        if (!IsNodeReady())
        {
            _pending = true;
            return;
        }

        Repaint();
    }

    private void Repaint()
    {
        _pending = false;

        // The old basin goes before the new one is built: two worlds of tile
        // layers in one viewport would draw on top of each other.
        if (_basin is not null)
        {
            _viewport.RemoveChild(_basin);
            _basin.QueueFree();
        }

        _basin = new BasinWorld();
        _viewport.AddChild(_basin);
        _basin.Build(_cells, _seed, _land, _climate);
        _basin.PaintBareGround();

        Frame();
    }
}
