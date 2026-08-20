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
public sealed partial class LeaseBoard : Control
{
    private static readonly Length WellDepth = new(2000.0);

    private VBoxContainer _list = null!;
    private VBoxContainer _detail = null!;
    private LeaseMap _map = null!;
    private Label _mode = null!;
    private Label _status = null!;
    private Control? _topBar;
    private ulong _selected;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(SlateChrome.Backdrop());

        BuildList();
        BuildMap();
        BuildDetail();
        BuildFooter();
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
        PanelContainer sign = SlateChrome.Sign(
            "STRUCTURES", new Vector2(430, 520), LayoutPreset.CenterLeft, new Vector2(30, -300));

        AddChild(sign);

        // Five rows and their gaps exactly. A scroll cut to an arbitrary height
        // slices the last row in half, which reads as a panel that does not fit
        // its contents rather than as a list with more below.
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(396, (5 * RowHeight) + (4 * RowGap)),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _list = new VBoxContainer { CustomMinimumSize = new Vector2(382, 0) };
        _list.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_list);
        SlateChrome.ContentOf(sign).AddChild(scroll);
    }

    private void BuildMap()
    {
        PanelContainer sign = SlateChrome.Sign(
            "THE LEASE", new Vector2(500, 520), LayoutPreset.Center, new Vector2(-250, -300));

        AddChild(sign);

        _map = new LeaseMap { CustomMinimumSize = new Vector2(466, (5 * RowHeight) + (4 * RowGap)) };
        SlateChrome.ContentOf(sign).AddChild(_map);
    }

    /// <summary>How tall one structure row is, and the gap between two.</summary>
    private const int RowHeight = 84;
    private const int RowGap = 8;

    private void BuildDetail()
    {
        PanelContainer sign = SlateChrome.Sign(
            "WHAT WE BELIEVE", new Vector2(430, 520), LayoutPreset.CenterRight, new Vector2(-30, -300));

        sign.GrowHorizontal = GrowDirection.Begin;
        AddChild(sign);

        var paper = new PanelContainer { CustomMinimumSize = new Vector2(396, 0) };
        paper.AddThemeStyleboxOverride("panel", SlateChrome.FieldPlate());

        _detail = new VBoxContainer();
        _detail.AddThemeConstantOverride("separation", 8);
        paper.AddChild(_detail);
        SlateChrome.ContentOf(sign).AddChild(paper);
    }

    private void BuildFooter()
    {
        PanelContainer bar = SlateChrome.Sign(
            string.Empty, new Vector2(1000, 0), LayoutPreset.CenterBottom, new Vector2(-500, -18));

        bar.GrowVertical = GrowDirection.Begin;
        AddChild(bar);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 14);

        _mode = SlateChrome.Text("Placement mode: a well", 19, KitTheme.Ink);
        _mode.CustomMinimumSize = new Vector2(330, 0);
        row.AddChild(_mode);

        Button survey = SlateChrome.Chunk("SHOOT SEISMIC", UiSurface.Role.Neutral, new Vector2(230, 50));
        survey.Pressed += () => Order(false);
        row.AddChild(survey);

        Button drill = SlateChrome.Chunk("CONFIRM - DRILL", UiSurface.Role.Success, new Vector2(250, 50));
        drill.Pressed += () => Order(true);
        row.AddChild(drill);

        Button cancel = SlateChrome.Chunk("CANCEL", UiSurface.Role.Danger, new Vector2(150, 50));
        cancel.Pressed += () => SceneRouter.Instance.CloseOverlay();
        row.AddChild(cancel);

        SlateChrome.ContentOf(bar).AddChild(row);

        _status = SlateChrome.Text(string.Empty, 15, KitTheme.Amber);
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _status.CustomMinimumSize = new Vector2(960, 0);
        SlateChrome.ContentOf(bar).AddChild(_status);
    }

    private void Refresh()
    {
        FieldReadModel? snapshot = EngineHost.Instance.Snapshot;

        if (snapshot is null)
            return;

        if (_topBar is not null && IsInstanceValid(_topBar))
            _topBar.QueueFree();

        _topBar = DispatchBoard.Header(snapshot);
        AddChild(_topBar);

        foreach (Node child in _list.GetChildren())
            child.QueueFree();

        if (_selected == 0 && snapshot.Prospects.Count > 0)
            _selected = snapshot.Prospects[0].Prospect.Value;

        for (int i = 0; i < snapshot.Prospects.Count; i++)
        {
            ProspectView prospect = snapshot.Prospects[i];
            ulong id = prospect.Prospect.Value;

            // A structure whose source has been disproved is one a hole has
            // already answered — the mockup's padlock, earned rather than set.
            bool spent = prospect.Source < 0.10;

            Button card = SlateChrome.IconCard(
                spent ? "blowout-preventer" : "wellhead-tree",
                $"{prospect.Play}",
                [
                    $"({prospect.At.X / 1000.0:F0} km, {prospect.At.Y / 1000.0:F0} km)",
                    $"{prospect.ToMarket.Metres / 1000.0:F0} km to market",
                ],
                spent ? "DRILLED" : $"POS {prospect.ProbabilityOfSuccess * 100:F0}%",
                spent ? KitTheme.Muted : Odds(prospect.ProbabilityOfSuccess),
                null,
                KitTheme.Muted,
                id == _selected,
                spent,
                new Vector2(382, 84));

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

    private void ShowDetail(ProspectView? prospect)
    {
        foreach (Node child in _detail.GetChildren())
            child.QueueFree();

        if (prospect is null)
        {
            _detail.AddChild(SlateChrome.Body("Nothing left in this basin to put a hole in."));
            _mode.Text = "Placement mode: nothing selected";
            return;
        }

        _mode.Text = $"Placement mode: a well on {prospect.Play}";

        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 10);
        head.AddChild(SlateChrome.Icon("wellhead-tree", 52.0f));

        var title = new VBoxContainer();
        title.AddChild(SlateChrome.Text(prospect.Play.ToString(), 22, KitTheme.Ink));
        title.AddChild(SlateChrome.Text(
            $"Probability of success {prospect.ProbabilityOfSuccess * 100:F0}%",
            16,
            Odds(prospect.ProbabilityOfSuccess).Darkened(0.2f)));

        head.AddChild(title);
        _detail.AddChild(head);

        _detail.AddChild(Line("Cost", "Months of rig time, and the hole is paid for whether or not it finds anything."));
        _detail.AddChild(Line("Required area", "A cleared pad, five tiles square. The ground is levelled when the rig arrives."));
        _detail.AddChild(Line("Placement", "The structure is where world generation put it — confirm sends the rig there."));

        _detail.AddChild(SlateChrome.Text("The five factors", 14, new Color(0.45f, 0.40f, 0.34f)));
        _detail.AddChild(Factor("Source", prospect.Source));
        _detail.AddChild(Factor("Reservoir", prospect.Reservoir));
        _detail.AddChild(Factor("Seal", prospect.Seal));
        _detail.AddChild(Factor("Trap", prospect.Trap));
        _detail.AddChild(Factor("Timing", prospect.Timing));
    }

    private static Control Factor(string name, double value)
    {
        // Weakest reads red: the point of showing the five apart is that the low
        // one is where a measurement is worth spending.
        Color colour = value < 0.4 ? KitTheme.Red.Lightened(0.25f)
            : value < 0.7 ? KitTheme.Amber
            : KitTheme.Green.Lightened(0.25f);

        var row = new HBoxContainer { CustomMinimumSize = new Vector2(370, 0) };
        row.AddThemeConstantOverride("separation", 8);

        Label label = SlateChrome.Text(name, 15, KitTheme.Ink);
        label.CustomMinimumSize = new Vector2(100, 0);
        row.AddChild(label);

        var bar = new ProgressBar
        {
            MinValue = 0.0,
            MaxValue = 1.0,
            Value = value,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(200, 16),
        };

        bar.AddThemeStyleboxOverride("background", SlateChrome.Track());
        bar.AddThemeStyleboxOverride("fill", SlateChrome.Fill(colour));
        row.AddChild(bar);

        row.AddChild(SlateChrome.Text($"{value * 100:F0}%", 14, new Color(0.42f, 0.36f, 0.28f)));

        return row;
    }

    private static Control Line(string heading, string body)
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 1);
        column.AddChild(SlateChrome.Text(heading, 14, new Color(0.45f, 0.40f, 0.34f)));

        Label text = SlateChrome.Body(body, 15);
        text.CustomMinimumSize = new Vector2(366, 0);
        column.AddChild(text);

        return column;
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
}

/// <summary>
/// The basin drawn small, with the chosen structure lit — the mockup's placement
/// preview, over the ground the world is actually tiled from.
/// </summary>
public sealed partial class LeaseMap : Control
{
    private FieldReadModel? _snapshot;
    private ulong _selected;

    public void Bind(FieldReadModel snapshot, ulong selected)
    {
        _snapshot = snapshot;
        _selected = selected;
        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 size = Size;
        BasinWorld? world = Gameplay.Current?.World;

        DrawRect(new Rect2(Vector2.Zero, size), KitTheme.Void);

        if (world is null || _snapshot is null)
            return;

        int tiles = world.Tiles;
        int step = Mathf.Max(1, tiles / 80);
        float scale = size.X / tiles;

        for (int y = 0; y < tiles; y += step)
        {
            for (int x = 0; x < tiles; x += step)
            {
                Color colour = world.Terrain.At(new Vector2I(x, y)) switch
                {
                    Ground.Water => new Color(0.30f, 0.55f, 0.75f),
                    Ground.Sand => new Color(0.72f, 0.60f, 0.42f),
                    Ground.Rock => new Color(0.42f, 0.42f, 0.44f),
                    _ => world.Terrain.IsDry(new Vector2I(x, y))
                        ? new Color(0.44f, 0.50f, 0.28f)
                        : new Color(0.40f, 0.62f, 0.30f),
                };

                DrawRect(new Rect2(x * scale, y * scale, step * scale + 1.0f, step * scale + 1.0f), colour);
            }
        }

        // The grid the mockup draws over the lease, so a placement reads as a
        // square of ground rather than a dot on a picture.
        for (int i = 0; i <= 12; i++)
        {
            float at = size.X / 12.0f * i;
            DrawLine(new Vector2(at, 0), new Vector2(at, size.Y), new Color(1, 1, 1, 0.07f));
            DrawLine(new Vector2(0, at), new Vector2(size.X, at), new Color(1, 1, 1, 0.07f));
        }

        float perMetre = size.X / (tiles / (float)BasinWorld.TilesPerKilometre * (float)BasinWorld.MetresPerCell);

        for (int i = 0; i < _snapshot.Prospects.Count; i++)
        {
            ProspectView prospect = _snapshot.Prospects[i];
            var at = new Vector2((float)prospect.At.X * perMetre, (float)prospect.At.Y * perMetre);
            bool chosen = prospect.Prospect.Value == _selected;

            Color colour = prospect.ProbabilityOfSuccess switch
            {
                < 0.20 => KitTheme.Red,
                < 0.35 => KitTheme.Amber,
                _ => KitTheme.Green,
            };

            if (chosen)
            {
                // The green square the mockup lights under the thing being placed.
                var pad = new Rect2(at - new Vector2(16, 16), new Vector2(32, 32));
                DrawRect(pad, new Color(colour, 0.35f));
                DrawRect(pad, colour, filled: false, width: 2.0f);
            }

            DrawCircle(at, chosen ? 7.0f : 5.0f, colour);
        }
    }
}
