#nullable enable

using Beep.ECS.UI;
using Godot;
using OGSim.Composition;
using OilfieldDays.App;
using OilfieldDays.Host;

namespace OilfieldDays.Ui;

/// <summary>
/// The HUD of the main-scene mockup, laid out where that image puts things.
///
/// <para>Top-left sign: the date as a player reads it, the cash, the field, a
/// bar. Top-right: the challenge timer. Bottom-left: the hotbar. Bottom-centre:
/// the context prompt, in quotes. Bottom-right: what the run is trying to do.
/// Plan 12 §3 lists exactly those, and this is that list.</para>
///
/// <para><b>Every slot carries a real number.</b> The mockup's "Reputation" and
/// "Actions-Left" have no counterpart in the engine yet, and plan 11 §11 forbids
/// inventing one — so those two slots show what the engine does publish, in the
/// same place and the same style. A made-up percentage would look right and be
/// a lie.</para>
/// </summary>
public sealed partial class GameHud : CanvasLayer
{
    /// <summary>The shipped scenario's target and deadline (EngineBuilder.FirstField).</summary>
    private const double TargetDollars = 600_000_000.0;
    private const int DeadlineMonths = 120;

    private static readonly string[] Seasons = { "Spring", "Summer", "Autumn", "Winter" };

    private Label _prompt = null!;
    private PanelContainer _promptPanel = null!;
    /// <summary>How many toasts may be on screen at once.</summary>
    private const int MostToasts = 4;

    private VBoxContainer _toasts = null!;
    private HBoxContainer _hotbar = null!;

    public override void _Ready()
    {
        Layer = 10;

        var root = new Control { Name = "HudRoot", MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        // The prompt for whatever is under the wheels, and the toasts. That is
        // all: every readout moved to the shell's panels, and the hotbar went
        // with them — it listed the same numbered actions the ACTIONS panel
        // already lists, so it was a second copy of one list crowding the bottom
        // of the screen and hiding behind the selection card.
        root.AddChild(BuildPrompt());
        root.AddChild(BuildToastColumn());
    }

    /// <summary>Offer, or stop offering, whatever is under the wheels.</summary>
    public void ShowPrompt(string? text)
    {
        _promptPanel.Visible = text is not null;

        if (text is not null)
            _prompt.Text = "“" + text + "”";
    }

    /// <summary>
    /// Say something once, in the middle of the screen, and take it away again.
    /// </summary>
    /// <remarks>
    /// Bad news is held nearly twice as long as good. A refusal a player did not
    /// read is a refusal that did not happen, and the whole point of showing one
    /// is that they can act on it.
    /// </remarks>
    public void Toast(string message, bool bad)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", SlateChrome.FieldPlate());

        Label label = SlateChrome.Text(message, 17, bad ? KitTheme.Red.Lightened(0.35f) : KitTheme.Ink);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(460, 0);
        panel.AddChild(label);
        _toasts.AddChild(panel);

        // A column that can grow without limit is a column that covers the game.
        // Months can arrive faster than a toast fades — a fast-forward runs
        // thirty of them before a single fade completes — so the oldest go
        // immediately rather than waiting their turn.
        while (_toasts.GetChildCount() > MostToasts)
        {
            Node oldest = _toasts.GetChild(0);
            _toasts.RemoveChild(oldest);
            oldest.QueueFree();
        }

        Tween tween = CreateTween();
        tween.TweenInterval(bad ? 4.5f : 2.8f);
        tween.TweenProperty(panel, "modulate:a", 0.0f, 0.5f);
        tween.TweenCallback(Callable.From(panel.QueueFree));
    }

    private Control BuildPrompt()
    {
        _promptPanel = new PanelContainer { Visible = false, CustomMinimumSize = new Vector2(460, 0) };
        _promptPanel.AddThemeStyleboxOverride("panel", SlateChrome.FieldPlate());

        _promptPanel.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        _promptPanel.Position = new Vector2(-230, -142);

        _prompt = SlateChrome.Text(string.Empty, 22, KitTheme.Ink, HorizontalAlignment.Center);
        _promptPanel.AddChild(_prompt);

        return _promptPanel;
    }

    private Control BuildToastColumn()
    {
        var holder = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        holder.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        holder.Position = new Vector2(-250, 88);
        holder.CustomMinimumSize = new Vector2(500, 0);

        _toasts = new VBoxContainer { CustomMinimumSize = new Vector2(500, 0) };
        _toasts.AddThemeConstantOverride("separation", 8);
        holder.AddChild(_toasts);

        return holder;
    }
}
