#nullable enable

using Beep.ECS.UI;
using Godot;
using OilfieldDays.App;

namespace OilfieldDays.Screens;

/// <summary>
/// The title screen (plan 08 §6.1).
///
/// <para>It touches no engine. Plan 08 is explicit that the menu's only
/// engine-facing decision is <em>whether</em> to build one, and that decision is
/// taken on the next screen.</para>
/// </summary>
public sealed partial class MainMenu : Control
{
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var ground = new ColorRect { Color = new Color(0.09f, 0.12f, 0.10f) };
        ground.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(ground);

        var art = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://assets/props/pumpjack.png"),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
            Modulate = new Color(1, 1, 1, 0.30f),
        };

        art.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(art);

        var column = new VBoxContainer();
        column.SetAnchorsPreset(LayoutPreset.Center);
        column.Position = new Vector2(-220, -210);
        column.CustomMinimumSize = new Vector2(440, 0);
        column.AddThemeConstantOverride("separation", 14);
        AddChild(column);

        Label title = ScreenChrome.Text("OILFIELD DAYS", 58, ScreenChrome.Gold, HorizontalAlignment.Center);
        title.CustomMinimumSize = new Vector2(440, 0);
        column.AddChild(title);

        Label blurb = ScreenChrome.Text(
            "A basin, a rig, and ten years to make $600 million.",
            19,
            ScreenChrome.Faded,
            HorizontalAlignment.Center);

        blurb.CustomMinimumSize = new Vector2(440, 0);
        blurb.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        column.AddChild(blurb);

        column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 26) });

        Add(column, "New basin", ScreenChrome.Good, () => SceneRouter.Instance.Go(SceneRouter.NewGame));
        Add(column, "Quit", ScreenChrome.Bad, () => GetTree().Quit());

        column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 18) });

        Leaderboard.Entry[] best = Leaderboard.Load();

        if (best.Length > 0)
        {
            column.AddChild(ScreenChrome.Text("BEST RUNS", 15, ScreenChrome.Faded, HorizontalAlignment.Center));

            for (int i = 0; i < best.Length && i < 3; i++)
            {
                column.AddChild(ScreenChrome.Text(
                    $"{i + 1}.  ${best[i].Cash / 1_000_000.0:N1}M  ·  seed {best[i].Seed}  ·  {best[i].Months} months",
                    16,
                    ScreenChrome.Ink,
                    HorizontalAlignment.Center));
            }
        }
    }

    private static void Add(Container parent, string text, Color accent, System.Action action)
    {
        Button button = ScreenChrome.Action(text, accent, new Vector2(440, 54));
        button.Pressed += () => action();
        parent.AddChild(button);
    }
}
