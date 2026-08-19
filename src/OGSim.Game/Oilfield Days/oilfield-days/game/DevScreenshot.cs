#nullable enable

using Godot;

namespace OilfieldDays;

/// <summary>
/// A development-only shutter: run the game, let it settle, save a PNG, quit.
///
/// <para>It exists so a change to the world, the HUD or the art can be checked by
/// looking at the game rather than by reasoning about the code — the same reason
/// the engine keeps a reference client. It does nothing unless the flag is
/// passed, so a player build never reaches any of it:</para>
///
/// <code>Godot_v4.7.1-stable_mono_win64.exe --path &lt;project&gt; -- --shot=C:\path\shot.png</code>
/// </summary>
public static class DevScreenshot
{
    private const string Flag = "--shot=";

    /// <summary>Arm the shutter if the command line asked for it.</summary>
    public static void ArmIfRequested(Node host)
    {
        string? path = null;

        foreach (string argument in OS.GetCmdlineUserArgs())
        {
            if (argument.StartsWith(Flag, System.StringComparison.Ordinal))
                path = argument[Flag.Length..];
        }

        if (path is null)
            return;

        int frames = 0;

        // Several frames, not one: the first frame has no smoothing settled, no
        // tween run and no tile layer drawn, so a shot taken there shows a game
        // that never appears on screen.
        host.GetTree().ProcessFrame += Capture;

        void Capture()
        {
            if (++frames < 30)
                return;

            host.GetTree().ProcessFrame -= Capture;

            Image image = host.GetViewport().GetTexture().GetImage();
            Error error = image.SavePng(path);

            GD.Print(error == Error.Ok
                ? $"[dev] wrote {path}"
                : $"[dev] could not write {path}: {error}");

            host.GetTree().Quit();
        }
    }
}
