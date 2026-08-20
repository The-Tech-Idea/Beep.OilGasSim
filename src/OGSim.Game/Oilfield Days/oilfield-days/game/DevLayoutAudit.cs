#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;

namespace OilfieldDays;

/// <summary>
/// A development-only tape measure: walk the screen and report every control
/// that does not fit.
///
/// <para>Layout faults are the one class of defect that looks deliberate. A
/// panel an inch too short reads as a design choice, a label clipped to an
/// ellipsis reads as intentional truncation, and a row half outside its parent
/// reads as a scroll. So they survive review after review — which is exactly why
/// they are worth measuring instead of looking at.</para>
///
/// <para>What is asked of every <c>Control</c> on screen:</para>
/// <list type="bullet">
/// <item><b>SQUEEZED</b> — a combined minimum larger than the rect, so something
/// inside is being crushed.</item>
/// <item><b>TRIMMED</b> — text wider than its rect, showing an ellipsis where a
/// word should be. A trimmed label reports a SMALL minimum — trimming is how it
/// fits — so no other check here can see one.</item>
/// <item><b>BORDER</b> — content margins shallower than the rim the plate draws,
/// measured against the rim rather than against where the piece was sliced.</item>
/// <item><b>ONRIM</b> — a child anchored across a plated parent, printed over the
/// frame. The stylebox's content margins are what a CONTAINER honours; an
/// anchored child is not laid out by one and gets the whole rect.</item>
/// <item><b>OUTSIDE</b> / <b>OFFSCREEN</b> — a rect that escapes its parent or
/// the viewport.</item>
/// <item><b>OFFGRID</b> — a rect on a half pixel, which resamples its text.</item>
/// <item><b>UNEVEN</b> — a row whose children are different heights.</item>
/// </list>
///
/// <para><b>What is deliberately not checked:</b> whether a control is tall
/// enough for the type it holds. Godot already guarantees it — a Label's minimum
/// height IS its line height and every container honours it — so the check was
/// written, verified against a deliberately squashed row, found unable to fail,
/// and removed. A check that cannot fail reads as coverage and is not.</para>
///
/// <code>Godot.exe --path &lt;project&gt; -- --screen=newgame --audit</code>
/// </summary>
public static class DevLayoutAudit
{
    /// <summary>Sub-pixel differences are rounding, not faults.</summary>
    private const float Slack = 1.5f;

    public static bool Requested()
    {
        foreach (string argument in OS.GetCmdlineUserArgs())
        {
            if (argument == "--audit")
                return true;
        }

        return false;
    }

    /// <summary>Measure everything under a node and print what does not fit.</summary>
    public static void Run(Node root, Vector2 viewport)
    {
        var faults = new List<string>();

        Walk(root, null, viewport, faults, clipped: false);

        GD.Print($"[audit] {faults.Count} layout fault{(faults.Count == 1 ? "" : "s")}");

        for (int i = 0; i < faults.Count && i < 40; i++)
            GD.Print($"[audit] {faults[i]}");

        if (faults.Count > 40)
            GD.Print($"[audit] ... and {faults.Count - 40} more");
    }

    private static void Walk(
        Node at, Control? parent, Vector2 viewport, List<string> faults, bool clipped)
    {
        // A Control hung under a Node2D is a sign in the world — a caption on a
        // building, a label on a chain element — and it lives in world space at
        // whatever coordinate the field put it. Measuring it against the viewport
        // reports the whole yard as off-screen every time the camera looks
        // elsewhere, which is the camera working.
        if (at is Node2D)
            return;

        if (at is Control control && control.IsVisibleInTree())
        {
            Check(control, parent, viewport, faults, clipped);

            // Everything below a scroll or a clipping control is cut to that
            // control's rect, so it can neither escape its parent nor leave the
            // screen — reporting it would drown the faults that are real.
            clipped = clipped || control is ScrollContainer || control.ClipContents;
            parent = control;
        }

        foreach (Node child in at.GetChildren())
            Walk(child, parent, viewport, faults, clipped);
    }

    private static void Check(
        Control control, Control? parent, Vector2 viewport, List<string> faults, bool clipped)
    {
        Vector2 wanted = control.GetCombinedMinimumSize();
        Vector2 has = control.Size;

        if (wanted.X - has.X > Slack || wanted.Y - has.Y > Slack)
        {
            faults.Add($"SQUEEZED {Name(control)}: has {Show(has)}, needs {Show(wanted)}");
        }

        // A trimmed label reports a SMALL minimum — trimming is how it fits — so
        // the squeeze check above can never see one. This is the check that
        // catches an ellipsis where there should be a word, which is the fault
        // that looks most like a decision.
        if (control is Label label
            && label.TextOverrunBehavior != TextServer.OverrunBehavior.NoTrimming
            && label.Text.Length > 0)
        {
            Font font = label.GetThemeFont("font");
            int fontSize = label.GetThemeFontSize("font_size");
            float needs = font.GetStringSize(label.Text, fontSize: fontSize).X;

            if (needs - label.Size.X > Slack)
            {
                faults.Add(
                    $"TRIMMED {Name(control)}: {needs:F0}px of text in {label.Size.X:F0}px");
            }
        }

        Border(control, faults);
        OnRim(control, parent, faults);
        Grid(control, faults);
        Even(control, faults);

        Rect2 rect = control.GetGlobalRect();

        // A scroll exists to hold more than it shows, and a clipped control has
        // said in as many words that it will cut its contents. Neither is a
        // fault, and reporting them would bury the ones that are.
        if (!clipped && parent is not null)
        {
            Rect2 room = parent.GetGlobalRect();

            if (rect.Position.X < room.Position.X - Slack
                || rect.Position.Y < room.Position.Y - Slack
                || rect.End.X > room.End.X + Slack
                || rect.End.Y > room.End.Y + Slack)
            {
                faults.Add($"OUTSIDE {Name(control)} {Show(rect)} escapes {Name(parent)} {Show(room)}");
            }
        }

        if (!clipped
            && (rect.Position.X < -Slack || rect.Position.Y < -Slack
                || rect.End.X > viewport.X + Slack || rect.End.Y > viewport.Y + Slack))
        {
            faults.Add($"OFFSCREEN {Name(control)} {Show(rect)}");
        }
    }

    /// <summary>
    /// Content printed on the frame the plate draws.
    /// </summary>
    /// <remarks>
    /// A <c>StyleBoxTexture</c> carries two sets of margins and they answer
    /// different questions: the TEXTURE margins say where the piece is sliced —
    /// which is where its rim, bevel and corner bolts live — and the CONTENT
    /// margins say how far in whatever sits on it starts. Content margins
    /// smaller than texture margins put the text on the rim. It is invisible in
    /// code, obvious on screen, and the exact fault this build shipped on every
    /// panel until it was measured.
    /// </remarks>
    private static void Border(Control control, List<string> faults)
    {
        foreach (string slot in new[] { "panel", "normal" })
        {
            if (!control.HasThemeStylebox(slot))
                continue;

            if (control.GetThemeStylebox(slot) is not StyleBoxTexture plate)
                continue;

            // Compared against the RIM the piece draws, not against where it was
            // sliced — the slice is wide enough to keep a bolt or a rounded end
            // whole and says nothing about how deep the visible frame is.
            string piece = PieceOf(plate);
            (float across, float down) = OilfieldDays.App.SlateChrome.RimOf(piece);

            // The plates are not vertically symmetric — the button pieces carry a
            // shadow along the bottom — so a face is centred ABOVE the middle of
            // its box. Equal content margins put the text below the face, which
            // reads as type that has slipped down. The bottom margin has to run
            // deeper by twice the lift, because centring splits the difference.
            float wantedGap = OilfieldDays.App.SlateChrome.LiftOf(piece) * 2.0f;
            float gap = plate.ContentMarginBottom - plate.ContentMarginTop;

            if (plate.ContentMarginTop > 0.0f && Mathf.Abs(gap - wantedGap) > Slack)
            {
                faults.Add(
                    $"UNCENTRED {Name(control)} [{slot}]: face sits {wantedGap / 2.0f:F0}px high " +
                    $"but the margins differ by {gap:F0}px, not {wantedGap:F0}px");
            }

            // Zero content margin is a frame that lays its own content out with a
            // margin container, which is how the panels do it; only a positive
            // but insufficient inset is a mistake.
            if (plate.ContentMarginLeft > 0.0f && plate.ContentMarginLeft < across)
            {
                faults.Add(
                    $"BORDER {Name(control)} [{slot}]: content starts at " +
                    $"{plate.ContentMarginLeft:F0}px inside a {across:F0}px rim");
            }

            if (plate.ContentMarginTop > 0.0f && plate.ContentMarginTop < down)
            {
                faults.Add(
                    $"BORDER {Name(control)} [{slot}]: content starts {plate.ContentMarginTop:F0}px " +
                    $"down a {down:F0}px rim");
            }
        }
    }

    /// <summary>
    /// A child laid across a plated parent, printed over the frame.
    /// </summary>
    /// <remarks>
    /// <b>The stylebox's content margins do not apply here.</b> They are what a
    /// CONTAINER honours when it fits a child; a child anchored to its parent's
    /// full rect is not laid out by a container and gets the whole rect, rim
    /// included. Every list row in this build was built that way, and every one
    /// of them ran its icon and its text over the frame — worst at the top and
    /// bottom, where the offsets were left at zero and nobody notices a missing
    /// number.
    /// </remarks>
    private static void OnRim(Control control, Control? parent, List<string> faults)
    {
        if (parent is null or Container)
            return;

        StyleBoxTexture? plate = null;

        foreach (string slot in new[] { "panel", "normal" })
        {
            if (parent.HasThemeStylebox(slot) && parent.GetThemeStylebox(slot) is StyleBoxTexture found)
            {
                plate = found;
                break;
            }
        }

        if (plate is null)
            return;

        (float across, float down) = OilfieldDays.App.SlateChrome.RimOf(PieceOf(plate));

        Rect2 rect = control.GetGlobalRect();
        Rect2 room = parent.GetGlobalRect();

        if (rect.Position.X < room.Position.X + across - Slack
            || rect.End.X > room.End.X - across + Slack
            || rect.Position.Y < room.Position.Y + down - Slack
            || rect.End.Y > room.End.Y - down + Slack)
        {
            faults.Add(
                $"ONRIM {Name(control)} {Show(rect)} runs over the frame of " +
                $"{Name(parent)} {Show(room)} (rim {across:F0}x{down:F0})");
        }
    }

    /// <summary>Which cut piece a stylebox is drawn from.</summary>
    private static string PieceOf(StyleBoxTexture plate)
    {
        string path = plate.Texture?.ResourcePath ?? string.Empty;
        int slash = path.LastIndexOf('/');
        int dot = path.LastIndexOf('.');

        return slash < 0 || dot <= slash ? string.Empty : path[(slash + 1)..dot];
    }

    /// <summary>
    /// A rect on a half pixel.
    /// </summary>
    /// <remarks>
    /// Text drawn at a fractional offset is resampled, so one label reads
    /// slightly softer or heavier than the one beside it. Nothing looks broken;
    /// the screen just looks untidy in a way that is hard to name.
    /// </remarks>
    private static void Grid(Control control, List<string> faults)
    {
        Vector2 at = control.GetGlobalRect().Position;

        if (Mathf.Abs(at.X - Mathf.Round(at.X)) > 0.01f
            || Mathf.Abs(at.Y - Mathf.Round(at.Y)) > 0.01f)
        {
            faults.Add($"OFFGRID {Name(control)} at ({at.X:F2},{at.Y:F2})");
        }
    }

    /// <summary>
    /// Siblings in a row or a column that do not line up across the axis.
    /// </summary>
    /// <remarks>
    /// A <c>BoxContainer</c> stacks along one axis and leaves the other to its
    /// children, so a row of buttons at three different heights is a row nobody
    /// asked for. Only controls that carry a plate are compared: a bare label is
    /// meant to be its own height, and a spacer has no height worth matching.
    /// </remarks>
    private static void Even(Control control, List<string> faults)
    {
        // Rows only. In a COLUMN, children of different widths are the normal
        // case — a button under a panel, a caption over a field — and flagging
        // them would report every screen as broken.
        if (control is not HBoxContainer box)
            return;

        const bool vertical = false;
        var sizes = new List<(Control Child, float Across)>();

        foreach (Node node in box.GetChildren())
        {
            if (node is not Control child || !child.Visible)
                continue;

            if (child is not (Button or PanelContainer))
                continue;

            sizes.Add((child, vertical ? child.Size.X : child.Size.Y));
        }

        if (sizes.Count < 2)
            return;

        float first = sizes[0].Across;

        for (int i = 1; i < sizes.Count; i++)
        {
            if (Mathf.Abs(sizes[i].Across - first) > Slack)
            {
                faults.Add(
                    $"UNEVEN {Name(sizes[i].Child)} is {sizes[i].Across:F0}px " +
                    $"{(vertical ? "wide" : "tall")} beside {Name(sizes[0].Child)} at {first:F0}px");
            }
        }
    }

    private static string Name(Control control)
    {
        string kind = control.GetType().Name;
        string named = control.Name.ToString();

        // A generated name says nothing; the text a control carries says which
        // one on screen it is.
        string label = control switch
        {
            Label text when text.Text.Length > 0 => $" \"{Clip(text.Text)}\"",
            Button button when button.Text.Length > 0 => $" \"{Clip(button.Text)}\"",
            _ => string.Empty,
        };

        return named.StartsWith('@') ? $"{kind}{label}" : $"{kind}:{named}{label}";
    }

    private static string Clip(string text) =>
        text.Length <= 24 ? text.Replace("\n", " ") : text[..24].Replace("\n", " ") + "...";

    private static string Show(Vector2 size) =>
        $"{size.X.ToString("F0", CultureInfo.InvariantCulture)}x{size.Y.ToString("F0", CultureInfo.InvariantCulture)}";

    private static string Show(Rect2 rect) =>
        $"({rect.Position.X:F0},{rect.Position.Y:F0} {rect.Size.X:F0}x{rect.Size.Y:F0})";
}
