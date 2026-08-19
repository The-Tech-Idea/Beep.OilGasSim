#nullable enable

using Godot;
using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Kernel;
using OilfieldDays.App;
using OilfieldDays.Host;

namespace OilfieldDays.Screens;

/// <summary>
/// The challenge result — the scorecard-and-leaderboard mockup, built to its
/// layout: a titled header with a rank rosette, a scorecard of icon rows on the
/// left, the local board on the right with medals, and three actions along the
/// bottom.
///
/// <para><b>The scorecard is the scenario's, not this screen's.</b> Plan 09 §7:
/// the engine judges at stage 12 and publishes at stage 13, and every line here
/// is read off <c>ScenarioProgress</c> or the final snapshot. The shipped
/// scenario scores nothing yet — SDD-014's dimensions read ledger values this
/// loop does not publish — so the rows that would be invented say so instead of
/// drawing a bar.</para>
/// </summary>
public sealed partial class ChallengeResult : Control
{
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var ground = new ColorRect { Color = Color.FromHtml("2E1F12") };
        ground.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(ground);

        FieldReadModel? snapshot = EngineHost.Instance.Snapshot;

        if (snapshot is null)
        {
            AddChild(ScreenChrome.Text("No run to report.", 30, ScreenChrome.Cream, HorizontalAlignment.Center));
            return;
        }

        Leaderboard.Entry run = new(
            EngineHost.Instance.Seed,
            snapshot.Cash.Cents / 100.0,
            snapshot.Tick.Value,
            snapshot.Wells,
            Verdict(snapshot));

        Leaderboard.Entry[] board = Leaderboard.Record(run);
        int rank = Leaderboard.RankOf(board, run);

        BuildHeader(snapshot, rank);
        BuildScorecard(snapshot);
        BuildLeaderboard(board, run);
        BuildButtons();
    }

    private void BuildHeader(FieldReadModel snapshot, int rank)
    {
        PanelContainer sign = ScreenChrome.Sign(
            string.Empty, new Vector2(880, 0), LayoutPreset.CenterTop, new Vector2(-440, 20));

        AddChild(sign);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 20);

        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddChild(ScreenChrome.Text("THE FIRST FIELD", 30, ScreenChrome.Gold, HorizontalAlignment.Center));
        titles.AddChild(ScreenChrome.Text(
            $"{Verdict(snapshot)}  -  {snapshot.Date.Year}-{snapshot.Date.Month:00}, month {snapshot.Tick.Value}",
            18,
            ScreenChrome.Cream,
            HorizontalAlignment.Center));

        row.AddChild(titles);

        if (rank > 0)
        {
            var rosette = new VBoxContainer();
            rosette.AddChild(ScreenChrome.Text("RANK", 13, ScreenChrome.Faded, HorizontalAlignment.Center));
            rosette.AddChild(ScreenChrome.Rosette(
                rank.ToString(System.Globalization.CultureInfo.InvariantCulture), ScreenChrome.Gold, 62.0f));

            row.AddChild(rosette);
        }

        ScreenChrome.ContentOf(sign).AddChild(row);
    }

    private void BuildScorecard(FieldReadModel snapshot)
    {
        PanelContainer sign = ScreenChrome.Sign(
            "SCORECARD", new Vector2(640, 520), LayoutPreset.CenterLeft, new Vector2(70, -200));

        AddChild(sign);

        var paper = new PanelContainer { CustomMinimumSize = new Vector2(606, 430) };
        paper.AddThemeStyleboxOverride("panel", ScreenChrome.PaperBox());

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 12);
        paper.AddChild(column);
        ScreenChrome.ContentOf(sign).AddChild(paper);

        double dollars = snapshot.Cash.Cents / 100.0;

        column.AddChild(ScoreRow(
            "crude-oil-storage-tank", "Field value", dollars / 600_000_000.0,
            $"${dollars / 1_000_000.0:N1}M of $600M",
            dollars >= 600_000_000.0 ? ScreenChrome.Good : ScreenChrome.Cash));

        column.AddChild(ScoreRow(
            "pumpjack", "Production, final month", Mathf.Min(1.0, snapshot.ProducedThisTick.CubicMetres / 40_000.0),
            $"{snapshot.ProducedThisTick.CubicMetres:N0} m3", ScreenChrome.Good));

        column.AddChild(ScoreRow(
            "drilling-rig-derrick", "Wells drilled", Mathf.Min(1.0, snapshot.Wells / 6.0),
            $"{snapshot.Wells}", ScreenChrome.Gold));

        column.AddChild(ScoreRow(
            "metering-station", "Ten years used", snapshot.Tick.Value / 120.0,
            $"month {snapshot.Tick.Value} of 120", ScreenChrome.Wood));

        column.AddChild(ScreenChrome.Text("Objectives", 15, new Color(0.45f, 0.40f, 0.34f)));

        ScenarioProgress progress = snapshot.Progress;

        for (int i = 0; i < progress.Objectives.Count; i++)
        {
            (ContentId objective, ObjectiveState state, double amount) = progress.Objectives[i];

            column.AddChild(ScreenChrome.Text(
                $"{objective}  -  {state}  ({amount * 100:F0}%)",
                16,
                state == ObjectiveState.Met ? ScreenChrome.Cash
                    : state == ObjectiveState.Pending ? ScreenChrome.Ink : ScreenChrome.Bad));
        }

        if (progress.Scores.Count == 0)
        {
            Label note = ScreenChrome.Body(
                "The shipped scenario scores nothing yet: the engine's score dimensions read ledger values " +
                "this loop does not publish, and it would rather show none than invent five.", 14);

            note.CustomMinimumSize = new Vector2(580, 0);
            column.AddChild(note);
        }
        else
        {
            for (int i = 0; i < progress.Scores.Count; i++)
            {
                (ScoreDimension dimension, double score) = progress.Scores[i];

                column.AddChild(ScoreRow(
                    "metering-station", dimension.ToString(), score, $"{score * 100:F0}%", ScreenChrome.Cash));
            }
        }
    }

    private static Control ScoreRow(string icon, string label, double value, string readout, Color colour)
    {
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(580, 0) };
        row.AddThemeConstantOverride("separation", 12);
        row.AddChild(ScreenChrome.Icon(icon, 44.0f));

        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 2);

        var head = new HBoxContainer();
        Label name = ScreenChrome.Text(label, 17, ScreenChrome.Ink);
        name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(name);
        head.AddChild(ScreenChrome.Text(readout, 16, new Color(0.42f, 0.36f, 0.28f)));
        column.AddChild(head);

        var bar = new ProgressBar
        {
            MinValue = 0.0,
            MaxValue = 1.0,
            Value = Mathf.Clamp(value, 0.0, 1.0),
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(480, 18),
        };

        bar.AddThemeStyleboxOverride("background", ScreenChrome.FlatBox(new Color(0.80f, 0.72f, 0.58f), radius: 9));
        bar.AddThemeStyleboxOverride("fill", ScreenChrome.FlatBox(colour, radius: 9));
        column.AddChild(bar);

        row.AddChild(column);

        return row;
    }

    private void BuildLeaderboard(Leaderboard.Entry[] board, Leaderboard.Entry run)
    {
        PanelContainer sign = ScreenChrome.Sign(
            "LOCAL LEADERBOARD", new Vector2(640, 520), LayoutPreset.CenterRight, new Vector2(-70, -200));

        sign.GrowHorizontal = GrowDirection.Begin;
        AddChild(sign);

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(606, 400) };
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(592, 0) };
        column.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(column);
        ScreenChrome.ContentOf(sign).AddChild(scroll);

        for (int i = 0; i < board.Length && i < 8; i++)
        {
            Leaderboard.Entry entry = board[i];
            bool mine = entry.Seed == run.Seed && entry.Months == run.Months && entry.Cash.Equals(run.Cash);

            Button card = ScreenChrome.Card(string.Empty, mine, dimmed: !mine, new Vector2(592, 76));

            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            row.OffsetLeft = 12;
            row.OffsetRight = -12;
            row.OffsetTop = 6;
            row.OffsetBottom = -6;
            row.AddThemeConstantOverride("separation", 12);
            card.AddChild(row);

            row.AddChild(ScreenChrome.Medal(i + 1));
            row.AddChild(ScreenChrome.Icon(entry.Wells > 2 ? "pumpjack" : "drilling-rig-derrick", 46.0f));

            var lines = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            lines.AddChild(ScreenChrome.Text(
                $"${entry.Cash / 1_000_000.0:N1}M  -  {entry.Wells} wells", 17, ScreenChrome.Ink));

            lines.AddChild(ScreenChrome.Text(
                $"seed {entry.Seed}  -  {entry.Months} months  -  {entry.Outcome}",
                14,
                new Color(0.42f, 0.36f, 0.28f)));

            row.AddChild(lines);
            column.AddChild(card);
        }

        Label note = ScreenChrome.Text(
            "Runs compare only on the same seed: the basin and its odds are generated from it.",
            14,
            ScreenChrome.Faded);

        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        note.CustomMinimumSize = new Vector2(600, 0);
        ScreenChrome.ContentOf(sign).AddChild(note);
    }

    private void BuildButtons()
    {
        var row = new HBoxContainer();
        row.SetAnchorsPreset(LayoutPreset.CenterBottom);
        row.Position = new Vector2(-500, -84);
        row.AddThemeConstantOverride("separation", 16);
        AddChild(row);

        Button again = ScreenChrome.Action("NEXT BASIN", ScreenChrome.Good, new Vector2(320, 56));
        again.Pressed += () => SceneRouter.Instance.Go(SceneRouter.NewGame);
        row.AddChild(again);

        Button replay = ScreenChrome.Action("SAME SEED AGAIN", ScreenChrome.Wood, new Vector2(320, 56));
        replay.Pressed += () => SceneRouter.Instance.Go(SceneRouter.NewGame);
        row.AddChild(replay);

        Button menu = ScreenChrome.Action("MAIN MENU", ScreenChrome.Bad, new Vector2(320, 56));
        menu.Pressed += () => SceneRouter.Instance.Go(SceneRouter.MainMenu);
        row.AddChild(menu);
    }

    private static string Verdict(FieldReadModel snapshot) => snapshot.Insolvent
        ? "Broke"
        : snapshot.Outcome switch
        {
            ObjectiveState.Met => "Target reached",
            ObjectiveState.Failed => "Failed",
            ObjectiveState.Expired => "Out of time",
            _ => "Ended",
        };
}
