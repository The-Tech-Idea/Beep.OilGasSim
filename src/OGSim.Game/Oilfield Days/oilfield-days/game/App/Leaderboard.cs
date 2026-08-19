#nullable enable

using System.Collections.Generic;
using System.Globalization;
using Godot;

namespace OilfieldDays.App;

/// <summary>
/// The local leaderboard of the challenge-result mockup, and of plan 11 §5.
///
/// <para><b>Host data, deliberately.</b> A finished run is the host's own record
/// — which seed was played, what the field was worth when it ended, how long it
/// took — and the engine has no concept of a previous run. Plan 11 §5 asks for a
/// local board first and for scores that are comparable only within one profile,
/// so the seed is stored beside the result and shown with it.</para>
///
/// <para>Kept in <c>user://</c>, where the host owns file paths (plan 00 §13).</para>
/// </summary>
public static class Leaderboard
{
    private const string Path = "user://oilfield-days-runs.json";
    private const int Keep = 20;

    public readonly record struct Entry(ulong Seed, double Cash, int Months, int Wells, string Outcome);

    public static Entry[] Load()
    {
        if (!FileAccess.FileExists(Path))
            return [];

        using FileAccess file = FileAccess.Open(Path, FileAccess.ModeFlags.Read);

        if (file is null)
            return [];

        var json = Json.ParseString(file.GetAsText());

        if (json.VariantType != Variant.Type.Array)
            return [];

        var rows = new List<Entry>();

        foreach (Variant item in json.AsGodotArray())
        {
            if (item.VariantType != Variant.Type.Dictionary)
                continue;

            Godot.Collections.Dictionary row = item.AsGodotDictionary();

            rows.Add(new Entry(
                (ulong)row["seed"].AsInt64(),
                row["cash"].AsDouble(),
                row["months"].AsInt32(),
                row["wells"].AsInt32(),
                row["outcome"].AsString()));
        }

        rows.Sort((a, b) => b.Cash.CompareTo(a.Cash));

        return rows.ToArray();
    }

    /// <summary>Record a finished run and return the board it belongs to.</summary>
    public static Entry[] Record(Entry entry)
    {
        var rows = new List<Entry>(Load()) { entry };
        rows.Sort((a, b) => b.Cash.CompareTo(a.Cash));

        if (rows.Count > Keep)
            rows.RemoveRange(Keep, rows.Count - Keep);

        var array = new Godot.Collections.Array();

        foreach (Entry row in rows)
        {
            array.Add(new Godot.Collections.Dictionary
            {
                ["seed"] = (long)row.Seed,
                ["cash"] = row.Cash,
                ["months"] = row.Months,
                ["wells"] = row.Wells,
                ["outcome"] = row.Outcome,
            });
        }

        using FileAccess file = FileAccess.Open(Path, FileAccess.ModeFlags.Write);

        if (file is null)
        {
            GD.PushWarning($"[leaderboard] could not write {Path}");
            return rows.ToArray();
        }

        file.StoreString(Json.Stringify(array));

        return rows.ToArray();
    }

    /// <summary>Where a run sits on the board, 1-based, or 0 if it is not on it.</summary>
    public static int RankOf(Entry[] board, Entry run)
    {
        for (int i = 0; i < board.Length; i++)
        {
            if (board[i].Seed == run.Seed && board[i].Months == run.Months
                && board[i].Cash.ToString(CultureInfo.InvariantCulture) == run.Cash.ToString(CultureInfo.InvariantCulture))
            {
                return i + 1;
            }
        }

        return 0;
    }
}
