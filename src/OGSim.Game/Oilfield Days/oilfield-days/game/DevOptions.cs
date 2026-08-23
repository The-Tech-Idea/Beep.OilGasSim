#nullable enable

using Godot;

namespace OilfieldDays;

/// <summary>
/// Command-line switches for looking at the game without playing it by hand.
///
/// <para>Passed after <c>--</c>, so they reach the game rather than the editor.
/// Each one only decides <em>when</em> something ordinary happens — a seed, a
/// number of months, a command the player could have issued — so what a
/// screenshot shows is a state the player could have reached.</para>
///
/// <code>Godot.exe --path &lt;project&gt; -- --seed=7 --months=18 --drill-best=3 --shot=out.png</code>
/// </summary>
public static class DevOptions
{
    /// <summary>The world seed, or null for the game's own.</summary>
    public static ulong? Seed =>
        Value("--seed=") is string raw && ulong.TryParse(raw, out ulong seed) ? seed : null;

    /// <summary>Months to advance before the game is handed over or shot.</summary>
    public static int Months =>
        Value("--months=") is string raw && int.TryParse(raw, out int months) ? months : 0;

    /// <summary>How many of the best prospects to drill on the way, through the real command path.</summary>
    public static int DrillBest =>
        Value("--drill-best=") is string raw && int.TryParse(raw, out int wells) ? wells : 0;

    /// <summary>Basin size in kilometres, or null for the game's own.</summary>
    public static int? Basin =>
        Value("--basin=") is string raw && int.TryParse(raw, out int km) ? km : null;

    /// <summary>Where to park the truck, in basin cells, or null for the middle.</summary>
    public static Vector2? At
    {
        get
        {
            if (Value("--at=") is not string raw)
                return null;

            string[] parts = raw.Split(',');

            if (parts.Length != 2
                || !float.TryParse(parts[0], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float x)
                || !float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float y))
            {
                GD.PushWarning($"[dev] --at expects two numbers in cells, got '{raw}'");
                return null;
            }

            return new Vector2(x, y);
        }
    }

    /// <summary>A screen to open once the game is up: a board, or a whole scene.</summary>
    public static string? Screen => Value("--screen=");

    /// <summary>
    /// Which page of a staged screen to open on, counted from 1.
    /// </summary>
    /// <remarks>
    /// The same reason <c>--screen=</c> exists: New Game is five pages, and
    /// looking at the fourth should not mean clicking through the first three.
    /// Out of range is clamped by the screen rather than refused here, because
    /// only the screen knows how many pages it has.
    /// </remarks>
    public static int Stage =>
        Value("--stage=") is string raw && int.TryParse(raw, out int stage) ? stage : 0;

    /// <summary>Play the run for this many months with the development policy.</summary>
    public static int Play =>
        Value("--play=") is string raw && int.TryParse(raw, out int months) ? months : 0;

    /// <summary>Save the run once the fast-forward has finished.</summary>
    public static bool Save => Has("--save");

    /// <summary>Open the newest save instead of creating a world.</summary>
    public static bool LoadNewest => Has("--load");

    private static bool Has(string flag)
    {
        foreach (string argument in OS.GetCmdlineUserArgs())
        {
            if (argument == flag)
                return true;
        }

        return false;
    }

    /// <summary>Camera zoom, for looking at the whole basin at once.</summary>
    public static float? Zoom =>
        Value("--zoom=") is string raw
        && float.TryParse(raw, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float z)
            ? z
            : null;

    /// <summary>Reality profile override, for comparing composed model sets.</summary>
    /// <summary>
    /// Which game mode to compose at, for looking at the other one.
    /// </summary>
    /// <remarks>
    /// The engine ships two modes and this client is one of them; running it at
    /// the other is how a change to a shared rule is checked against both
    /// without building two clients to look at it.
    /// </remarks>
    public static string? Mode => Value("--mode=");

    private static string? Value(string flag)
    {
        foreach (string argument in OS.GetCmdlineUserArgs())
        {
            if (argument.StartsWith(flag, System.StringComparison.Ordinal))
                return argument[flag.Length..];
        }

        return null;
    }
}
