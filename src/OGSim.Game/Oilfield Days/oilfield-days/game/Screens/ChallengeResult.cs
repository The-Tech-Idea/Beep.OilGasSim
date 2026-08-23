#nullable enable

using Beep.ECS.UI;
using Godot;
using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Kernel;
using OilfieldDays.App;
using OilfieldDays.Host;
using System;

namespace OilfieldDays.Screens;

/// <summary>
/// The challenge result - the scorecard-and-leaderboard mockup, built to its
/// layout: a titled header with a rank rosette, a scorecard of icon rows on the
/// left, the local board on the right with medals, and three actions along the
/// bottom.
///
/// <para><b>The scorecard is the scenario's, not this screen's.</b> Plan 09 §7:
/// the engine judges at stage 12 and publishes at stage 13, and every line here
/// is read off <c>ScenarioProgress</c> or the final snapshot. The shipped
/// scenario scores nothing yet - SDD-014's dimensions read ledger values this
/// loop does not publish - so the rows that would be invented say so instead of
/// drawing a bar.</para>
/// </summary>
[Tool]
public sealed partial class ChallengeResult : Control
{
	private VBoxContainer _scoreContent = null!;
	private VBoxContainer _leaderboardList = null!;
	private Label _leaderboardNote = null!;
	private Label _noRun = null!;
	private Label _scoreEmptyState = null!;
	private Label _leaderboardEmptyState = null!;
	private HBoxContainer _scoreRowTemplate = null!;
	private Label _scoreTextTemplate = null!;
	private Button _leaderboardRowTemplate = null!;

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		BuildShell();

		if (Godot.Engine.IsEditorHint())
			return;

		FieldReadModel? snapshot = EngineHost.Instance.Snapshot;

		if (snapshot is null)
		{
			_noRun.Visible = true;
			return;
		}

		_noRun.Visible = false;

		Leaderboard.Entry run = new(
			EngineHost.Instance.Seed,
			snapshot.Cash.Cents / 100.0,
			snapshot.Tick.Value,
			snapshot.Wells,
			Verdict(snapshot));

		Leaderboard.Entry[] board = Leaderboard.Record(run);
		int rank = Leaderboard.RankOf(board, run);

		FillHeader(snapshot, rank);
		FillScorecard(snapshot);
		FillLeaderboard(board, run);
		WireButtons();
	}

	private void BuildShell()
	{
		var ground = RequireNamed<ColorRect>("Ground");
		ground.Color = Color.FromHtml("071017");
		ground.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		var header = RequireNamed<PanelContainer>("HeaderSign");
		header.CustomMinimumSize = new Vector2(880, 0);
		header.AddThemeStyleboxOverride("panel", SlateChrome.PanelPlate(0));

		var headerRow = RequireNamed<HBoxContainer>(header, "HeaderRow");
		headerRow.AddThemeConstantOverride("separation", 20);

		var titles = RequireNamed<VBoxContainer>(headerRow, "Titles");
		titles.SizeFlagsHorizontal = SizeFlags.ExpandFill;

		Label title = RequireNamed<Label>(titles, "Title");
		title.Text = "THE FIRST FIELD";
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.AddThemeFontSizeOverride("font_size", 30);
		title.AddThemeColorOverride("font_color", KitTheme.Amber);

		Label subtitle = RequireNamed<Label>(titles, "Subtitle");
		subtitle.Text = "Ended  -  month 0";
		subtitle.HorizontalAlignment = HorizontalAlignment.Center;
		subtitle.AddThemeFontSizeOverride("font_size", 18);
		subtitle.AddThemeColorOverride("font_color", KitTheme.Ink);

		var rank = RequireNamed<VBoxContainer>(headerRow, "Rank");

		Label rankLabel = RequireNamed<Label>(rank, "RankLabel");
		rankLabel.Text = "RANK";
		rankLabel.HorizontalAlignment = HorizontalAlignment.Center;
		rankLabel.AddThemeFontSizeOverride("font_size", 13);
		rankLabel.AddThemeColorOverride("font_color", KitTheme.Muted);

		var score = RequireNamed<PanelContainer>("ScorecardSign");
		score.CustomMinimumSize = new Vector2(640, 520);
		score.AddThemeStyleboxOverride("panel", SlateChrome.PanelPlate(0));

		var scoreColumn = RequireNamed<VBoxContainer>(score, "Content");
		scoreColumn.AddThemeConstantOverride("separation", 8);
		AddHeader(scoreColumn, "Header", "SCORECARD");

		var paper = RequireNamed<PanelContainer>(scoreColumn, "ScorePaper");
		paper.CustomMinimumSize = new Vector2(606, 430);
		paper.AddThemeStyleboxOverride("panel", SlateChrome.FieldPlate());

		_scoreContent = RequireNamed<VBoxContainer>(paper, "ScoreContent");
		_scoreContent.AddThemeConstantOverride("separation", 12);
		_scoreEmptyState = RequireNamed<Label>(_scoreContent, "ScoreEmptyState");
		StyleScoreText(_scoreEmptyState, KitTheme.Muted);
		_scoreEmptyState.Visible = Godot.Engine.IsEditorHint();

		_scoreRowTemplate = RequireNamed<HBoxContainer>(_scoreContent, "ScoreRowTemplate");
		_scoreTextTemplate = RequireNamed<Label>(_scoreContent, "ScoreTextTemplate");
		StyleScoreRow(_scoreRowTemplate);
		_scoreRowTemplate.Visible = Godot.Engine.IsEditorHint();
		StyleScoreText(_scoreTextTemplate, KitTheme.Ink);
		_scoreTextTemplate.Visible = Godot.Engine.IsEditorHint();

		var board = RequireNamed<PanelContainer>("LeaderboardSign");
		board.GrowHorizontal = GrowDirection.Begin;
		board.CustomMinimumSize = new Vector2(640, 520);
		board.AddThemeStyleboxOverride("panel", SlateChrome.PanelPlate(0));

		var boardColumn = RequireNamed<VBoxContainer>(board, "Content");
		boardColumn.AddThemeConstantOverride("separation", 8);
		AddHeader(boardColumn, "Header", "LOCAL LEADERBOARD");

		var scroll = RequireNamed<ScrollContainer>(boardColumn, "LeaderboardScroll");
		scroll.CustomMinimumSize = new Vector2(606, 400);

		_leaderboardList = RequireNamed<VBoxContainer>(scroll, "LeaderboardList");
		_leaderboardList.CustomMinimumSize = new Vector2(592, 0);
		_leaderboardList.AddThemeConstantOverride("separation", 8);
		_leaderboardEmptyState = RequireNamed<Label>(_leaderboardList, "LeaderboardEmptyState");
		StyleScoreText(_leaderboardEmptyState, KitTheme.Muted);
		_leaderboardEmptyState.Visible = Godot.Engine.IsEditorHint();

		_leaderboardRowTemplate = RequireNamed<Button>(_leaderboardList, "LeaderboardRowTemplate");
		StyleLeaderboardRow(_leaderboardRowTemplate, selected: false, dimmed: false);
		_leaderboardRowTemplate.Visible = Godot.Engine.IsEditorHint();

		_leaderboardNote = RequireNamed<Label>(boardColumn, "LeaderboardNote");
		_leaderboardNote.Text = "Runs compare only on the same seed: the basin and its odds are generated from it.";
		_leaderboardNote.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_leaderboardNote.CustomMinimumSize = new Vector2(600, 0);
		_leaderboardNote.AddThemeFontSizeOverride("font_size", 14);
		_leaderboardNote.AddThemeColorOverride("font_color", KitTheme.Muted);

		var actions = RequireNamed<HBoxContainer>("Actions");
		actions.AddThemeConstantOverride("separation", 16);

		Button again = RequireNamed<Button>(actions, "NextButton");
		SlateChrome.ApplyChunk(again, "NEXT BASIN", UiSurface.Role.Success, new Vector2(320, 56));

		Button replay = RequireNamed<Button>(actions, "ReplayButton");
		SlateChrome.ApplyChunk(replay, "SAME SEED AGAIN", UiSurface.Role.Neutral, new Vector2(320, 56));

		Button menu = RequireNamed<Button>(actions, "MenuButton");
		SlateChrome.ApplyChunk(menu, "MAIN MENU", UiSurface.Role.Danger, new Vector2(320, 56));

		_noRun = RequireNamed<Label>("NoRun");
		_noRun.Text = "No run to report.";
		_noRun.CustomMinimumSize = new Vector2(360, 40);
		_noRun.HorizontalAlignment = HorizontalAlignment.Center;
		_noRun.AddThemeFontSizeOverride("font_size", 30);
		_noRun.AddThemeColorOverride("font_color", KitTheme.Ink);
		_noRun.Visible = false;
	}

	private static void AddHeader(Container parent, string name, string text)
	{
		Label header = RequireNamed<Label>(parent, name);
		header.Text = text;
		SlateChrome.PromoteHeader(header, UiSurface.Role.Warning, centered: true);
	}

	private void FillHeader(FieldReadModel snapshot, int rank)
	{
		if (FindNamed<Label>("Subtitle") is { } subtitle)
		{
			subtitle.Text =
				$"{Verdict(snapshot)}  -  {snapshot.Date.Year}-{snapshot.Date.Month:00}, month {snapshot.Tick.Value}";
		}

		if (FindNamed<VBoxContainer>("Rank") is not { } rankHost)
			return;

		for (int i = rankHost.GetChildCount() - 1; i >= 0; i--)
		{
			Node child = rankHost.GetChild(i);

			string childName = child.Name.ToString();

			if (childName != "RankLabel" && childName != "RankBadge")
			{
				rankHost.RemoveChild(child);
				child.QueueFree();
			}
		}

		if (rank > 0)
		{
			rankHost.Visible = true;
			Label badge = RequireNamed<Label>(rankHost, "RankBadge");
			badge.Text = rank.ToString(System.Globalization.CultureInfo.InvariantCulture);
			badge.Visible = true;
			badge.CustomMinimumSize = new Vector2(62, 62);
			badge.HorizontalAlignment = HorizontalAlignment.Center;
			badge.VerticalAlignment = VerticalAlignment.Center;
			badge.AddThemeFontSizeOverride("font_size", 24);
			badge.AddThemeColorOverride("font_color", Color.FromHtml("2A1C06"));
			badge.AddThemeStyleboxOverride("normal", SlateChrome.RolePlate(UiSurface.Role.Warning));
		}
		else
		{
			if (FindNamed<Label>(rankHost, "RankBadge") is { } badge)
				badge.Visible = false;

			rankHost.Visible = false;
		}
	}

	private void FillScorecard(FieldReadModel snapshot)
	{
		Clear(_scoreContent);

		double dollars = snapshot.Cash.Cents / 100.0;

		// THE TARGET COMES OFF THE READ MODEL. This row used to state a figure
		// of its own and went on stating it for months after the scenario moved.
		_scoreContent.AddChild(ScoreRow(
			"crude-oil-storage-tank", "Field value", Goal.Fraction(snapshot),
			Goal.Line(snapshot), KitTheme.Green));

		_scoreContent.AddChild(ScoreRow(
			"pumpjack", "Production, final month", Mathf.Min(1.0, snapshot.ProducedThisTick.CubicMetres / 40_000.0),
			$"{snapshot.ProducedThisTick.CubicMetres:N0} m3", KitTheme.Green));

		_scoreContent.AddChild(ScoreRow(
			"drilling-rig-derrick", "Wells drilled", Mathf.Min(1.0, snapshot.Wells / 6.0),
			$"{snapshot.Wells}", KitTheme.Amber));

		_scoreContent.AddChild(ScoreRow(
			"metering-station", "Ten years used", snapshot.Tick.Value / 120.0,
			$"month {snapshot.Tick.Value} of 120", KitTheme.Muted));

		_scoreContent.AddChild(ScoreText("Objectives", new Color(0.45f, 0.40f, 0.34f)));

		ScenarioProgress progress = snapshot.Progress;

		for (int i = 0; i < progress.Objectives.Count; i++)
		{
			(ContentId objective, ObjectiveState state, double amount) = progress.Objectives[i];

			_scoreContent.AddChild(ScoreText(
				$"{objective}  -  {state}  ({amount * 100:F0}%)",
				state == ObjectiveState.Met ? KitTheme.Green
					: state == ObjectiveState.Pending ? KitTheme.Ink : KitTheme.Red));
		}

		if (progress.Scores.Count == 0)
		{
			Label note = ScoreText(
				"The shipped scenario scores nothing yet: the engine's score dimensions read ledger values " +
				"this loop does not publish, and it would rather show none than invent five.",
				KitTheme.Muted);

			note.CustomMinimumSize = new Vector2(580, 0);
			_scoreContent.AddChild(note);
		}
		else
		{
			for (int i = 0; i < progress.Scores.Count; i++)
			{
				(ScoreDimension dimension, double score) = progress.Scores[i];

				_scoreContent.AddChild(ScoreRow(
					"metering-station", dimension.ToString(), score, $"{score * 100:F0}%", KitTheme.Green));
			}
		}
	}

	private Control ScoreRow(string icon, string label, double value, string readout, Color colour)
	{
		var row = (HBoxContainer)_scoreRowTemplate.Duplicate();
		row.Name = "ScoreRow";
		row.Visible = true;
		StyleScoreRow(row);

		TextureRect art = RequireNamed<TextureRect>(row, "Icon");
		art.Texture = GD.Load<Texture2D>($"res://assets/icons/{icon}.png");

		Label name = RequireNamed<Label>(row, "Name");
		name.Text = label;
		name.AddThemeFontSizeOverride("font_size", 17);
		name.AddThemeColorOverride("font_color", KitTheme.Ink);

		Label reading = RequireNamed<Label>(row, "Readout");
		reading.Text = readout;
		reading.AddThemeFontSizeOverride("font_size", 16);
		reading.AddThemeColorOverride("font_color", new Color(0.42f, 0.36f, 0.28f));

		ProgressBar bar = RequireNamed<ProgressBar>(row, "Bar");
		bar.Value = Mathf.Clamp(value, 0.0, 1.0);
		bar.AddThemeStyleboxOverride("background", SlateChrome.Track());
		bar.AddThemeStyleboxOverride("fill", SlateChrome.Fill(colour));

		return row;
	}

	private Label ScoreText(string text, Color colour)
	{
		var label = (Label)_scoreTextTemplate.Duplicate();
		label.Name = "ScoreText";
		label.Visible = true;
		label.Text = text;
		StyleScoreText(label, colour);
		return label;
	}

	private void FillLeaderboard(Leaderboard.Entry[] board, Leaderboard.Entry run)
	{
		Clear(_leaderboardList);

		for (int i = 0; i < board.Length && i < 8; i++)
		{
			Leaderboard.Entry entry = board[i];
			bool mine = entry.Seed == run.Seed && entry.Months == run.Months && entry.Cash.Equals(run.Cash);

			Button card = (Button)_leaderboardRowTemplate.Duplicate();
			card.Name = "LeaderboardRow";
			card.Visible = true;
			StyleLeaderboardRow(card, mine, !mine);

			Label place = RequireNamed<Label>(card, "Place");
			place.Text = (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

			TextureRect art = RequireNamed<TextureRect>(card, "Icon");
			art.Texture = GD.Load<Texture2D>(
				$"res://assets/icons/{(entry.Wells > 2 ? "pumpjack" : "drilling-rig-derrick")}.png");

			Label title = RequireNamed<Label>(card, "Title");
			title.Text = $"${entry.Cash / 1_000_000.0:N1}M  -  {entry.Wells} wells";

			Label meta = RequireNamed<Label>(card, "Meta");
			meta.Text = $"seed {entry.Seed}  -  {entry.Months} months  -  {entry.Outcome}";

			_leaderboardList.AddChild(card);
		}

		_leaderboardNote.Text =
			"Runs compare only on the same seed: the basin and its odds are generated from it.";
	}

	private void WireButtons()
	{
		RequireNamed<Button>("NextButton").Pressed += () => SceneRouter.Instance.Go(SceneRouter.NewGame);

		RequireNamed<Button>("ReplayButton").Pressed += () => SceneRouter.Instance.Go(SceneRouter.NewGame);

		RequireNamed<Button>("MenuButton").Pressed += () => SceneRouter.Instance.Go(SceneRouter.MainMenu);
	}

	private static void StyleScoreRow(HBoxContainer row)
	{
		row.CustomMinimumSize = new Vector2(580, 0);
		row.AddThemeConstantOverride("separation", 12);

		TextureRect icon = RequireNamed<TextureRect>(row, "Icon");
		icon.CustomMinimumSize = new Vector2(44, 44);
		icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;

		VBoxContainer column = RequireNamed<VBoxContainer>(row, "Column");
		column.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		column.AddThemeConstantOverride("separation", 2);

		Label name = RequireNamed<Label>(row, "Name");
		name.SizeFlagsHorizontal = SizeFlags.ExpandFill;

		ProgressBar bar = RequireNamed<ProgressBar>(row, "Bar");
		bar.MinValue = 0.0;
		bar.MaxValue = 1.0;
		bar.ShowPercentage = false;
		bar.CustomMinimumSize = new Vector2(480, 18);
	}

	private static void StyleScoreText(Label label, Color colour)
	{
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.AddThemeFontSizeOverride("font_size", 16);
		label.AddThemeColorOverride("font_color", colour);
	}

	private static void StyleLeaderboardRow(Button card, bool selected, bool dimmed)
	{
		card.Text = string.Empty;
		card.CustomMinimumSize = new Vector2(592, 76);
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
		row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		row.OffsetLeft = 16;
		row.OffsetRight = -16;
		row.OffsetTop = 10;
		row.OffsetBottom = -10;
		row.AddThemeConstantOverride("separation", 12);

		Label place = RequireNamed<Label>(card, "Place");
		place.CustomMinimumSize = new Vector2(46, 46);
		place.HorizontalAlignment = HorizontalAlignment.Center;
		place.VerticalAlignment = VerticalAlignment.Center;
		place.AddThemeFontSizeOverride("font_size", 20);
		place.AddThemeColorOverride("font_color", Color.FromHtml("2A1C06"));
		place.AddThemeStyleboxOverride("normal", SlateChrome.RolePlate(UiSurface.Role.Warning));

		TextureRect icon = RequireNamed<TextureRect>(card, "Icon");
		icon.CustomMinimumSize = new Vector2(46, 46);
		icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;

		VBoxContainer lines = RequireNamed<VBoxContainer>(card, "Lines");
		lines.SizeFlagsHorizontal = SizeFlags.ExpandFill;

		Label title = RequireNamed<Label>(card, "Title");
		title.AddThemeFontSizeOverride("font_size", 17);
		title.AddThemeColorOverride("font_color", KitTheme.Ink);

		Label meta = RequireNamed<Label>(card, "Meta");
		meta.AddThemeFontSizeOverride("font_size", 14);
		meta.AddThemeColorOverride("font_color", new Color(0.42f, 0.36f, 0.28f));
	}

	private static void Clear(Node node)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child.Name.ToString().EndsWith("Template") || child.Name.ToString().EndsWith("EmptyState"))
				continue;

			node.RemoveChild(child);
			child.QueueFree();
		}
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

	private T? FindNamed<T>(string name) where T : Node => FindNamed<T>(this, name);

	private T RequireNamed<T>(string name) where T : Node =>
		FindNamed<T>(name) ?? throw new InvalidOperationException(
			$"{nameof(ChallengeResult)} requires a design-time {typeof(T).Name} named '{name}'.");

	private static T RequireNamed<T>(Node root, string name) where T : Node =>
		FindNamed<T>(root, name) ?? throw new InvalidOperationException(
			$"{nameof(ChallengeResult)} requires a design-time {typeof(T).Name} named '{name}' under {root.GetPath()}.");

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
