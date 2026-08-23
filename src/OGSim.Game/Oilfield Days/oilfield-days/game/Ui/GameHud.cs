#nullable enable

using Beep.ECS.UI;
using Godot;
using OGSim.Composition;
using OilfieldDays.App;
using OilfieldDays.Host;
using System;

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
[Tool]
public sealed partial class GameHud : CanvasLayer
{
    private static readonly string[] Seasons = { "Spring", "Summer", "Autumn", "Winter" };

    private Label _prompt = null!;
    private PanelContainer _promptPanel = null!;
    /// <summary>How many toasts may be on screen at once.</summary>
    private const int MostToasts = 4;

    private VBoxContainer _toasts = null!;
    private PanelContainer _toastTemplate = null!;

    public override void _Ready()
    {
        Layer = 10;

        Control root = RequireNamed<Control>(this, "HudRoot");
        root.MouseFilter = Control.MouseFilterEnum.Ignore;
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        BindPrompt(root);
        BindToastColumn(root);
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
        var panel = (PanelContainer)_toastTemplate.Duplicate();
        panel.Name = "Toast";
        panel.Visible = true;
        panel.Modulate = Colors.White;

        Label label = RequireNamed<Label>(panel, "ToastText");
        label.Text = message;
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(460, 0);
        label.AddThemeFontSizeOverride("font_size", 17);
        label.AddThemeColorOverride("font_color", bad ? KitTheme.Red.Lightened(0.35f) : KitTheme.Ink);

        _toasts.AddChild(panel);

        // A column that can grow without limit is a column that covers the game.
        // Months can arrive faster than a toast fades — a fast-forward runs
        // thirty of them before a single fade completes — so the oldest go
        // immediately rather than waiting their turn.
        while (ToastCount() > MostToasts)
        {
            RemoveOldestToast();
        }

        Tween tween = CreateTween();
        tween.TweenInterval(bad ? 4.5f : 2.8f);
        tween.TweenProperty(panel, "modulate:a", 0.0f, 0.5f);
        tween.TweenCallback(Callable.From(panel.QueueFree));
    }

    private void BindPrompt(Control root)
    {
        _promptPanel = RequireNamed<PanelContainer>(root, "PromptPanel");
        _prompt = RequireNamed<Label>(_promptPanel, "Prompt");
        _prompt.Name = "Prompt";

        StylePrompt();
    }

    private void BindToastColumn(Control root)
    {
        Control holder = RequireNamed<Control>(root, "ToastHolder");
        holder.MouseFilter = Control.MouseFilterEnum.Ignore;
        holder.AnchorLeft = 0.5f;
        holder.AnchorTop = 0.0f;
        holder.AnchorRight = 0.5f;
        holder.AnchorBottom = 0.0f;
        holder.OffsetLeft = -250.0f;
        holder.OffsetTop = 88.0f;
        holder.OffsetRight = 250.0f;
        holder.OffsetBottom = 88.0f;
        holder.GrowHorizontal = Control.GrowDirection.Both;
        holder.GrowVertical = Control.GrowDirection.End;
        holder.CustomMinimumSize = new Vector2(500, 0);
        _toasts = RequireNamed<VBoxContainer>(holder, "Toasts");
        _toastTemplate = RequireNamed<PanelContainer>(_toasts, "ToastTemplate");
        StyleToasts();
    }

    private void StylePrompt()
    {
        _promptPanel.Visible = false;
        _promptPanel.CustomMinimumSize = new Vector2(460, 0);
        _promptPanel.AddThemeStyleboxOverride("panel", SlateChrome.FieldPlate());
        _prompt.AddThemeFontSizeOverride("font_size", 22);
        _prompt.AddThemeColorOverride("font_color", KitTheme.Ink);
        _prompt.HorizontalAlignment = HorizontalAlignment.Center;
    }

    private void StyleToasts()
    {
        _toasts.CustomMinimumSize = new Vector2(500, 0);
        _toasts.AddThemeConstantOverride("separation", 8);
        _toastTemplate.Visible = Godot.Engine.IsEditorHint();
        _toastTemplate.AddThemeStyleboxOverride("panel", SlateChrome.FieldPlate());

        Label sample = RequireNamed<Label>(_toastTemplate, "ToastText");
        sample.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        sample.CustomMinimumSize = new Vector2(460, 0);
        sample.AddThemeFontSizeOverride("font_size", 17);
        sample.AddThemeColorOverride("font_color", KitTheme.Ink);
    }

    private int ToastCount()
    {
        int count = 0;

        foreach (Node child in _toasts.GetChildren())
        {
            if (child != _toastTemplate)
                count++;
        }

        return count;
    }

    private void RemoveOldestToast()
    {
        foreach (Node child in _toasts.GetChildren())
        {
            if (child == _toastTemplate)
                continue;

            _toasts.RemoveChild(child);
            child.QueueFree();
            return;
        }
    }

    private static T? FindNamed<T>(Node at, string name) where T : Node
    {
        if (at is T typed && at.Name == name)
            return typed;

        foreach (Node child in at.GetChildren())
        {
            T? found = FindNamed<T>(child, name);

            if (found is not null)
                return found;
        }

        return null;
    }

    private static T RequireNamed<T>(Node at, string name) where T : Node =>
        FindNamed<T>(at, name) ?? throw new InvalidOperationException(
            $"{nameof(GameHud)} requires a design-time {typeof(T).Name} named '{name}' under {at.GetPath()}.");
}
