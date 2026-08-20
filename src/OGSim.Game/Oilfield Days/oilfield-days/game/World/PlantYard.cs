#nullable enable

using System;
using System.Collections.Generic;
using Godot;

namespace OilfieldDays.World;

/// <summary>
/// Where the plant's structures stand, and how much room each is given.
///
/// <para>The chain the engine publishes is an ordered list with no geometry, so
/// somebody has to decide where a separator goes. That is this — a shelf packer
/// that reads each structure's own footprint and clearance instead of dealing
/// every element the same slot.</para>
///
/// <para><b>Order is kept, because order is the one geometric fact the engine
/// does publish.</b> The chain runs wells → gathering → manifold → flowline →
/// separation → treating → metering → tank, and a player reading the yard left to
/// right is reading the actual chain. Packing tightest-first would waste less
/// ground and destroy that.</para>
/// </summary>
public sealed class PlantYard
{
    /// <summary>One structure, placed.</summary>
    /// <param name="Centre">Where the sprite stands, in world pixels.</param>
    /// <param name="Plot">The ground it holds, in tiles, clearance included.</param>
    public readonly record struct Placed(StructureKind Kind, Vector2 Centre, Rect2I Plot);

    private const string Folder = "res://data/structures";

    private readonly List<StructureKind> _kinds = new();

    /// <summary>Load every structure kind, longest match first.</summary>
    public PlantYard()
    {
        using DirAccess? directory = DirAccess.Open(Folder);

        if (directory is null)
        {
            GD.PushError($"[plant] cannot open {Folder}: {DirAccess.GetOpenError()}");

            return;
        }

        string[] files = directory.GetFiles();
        Array.Sort(files, StringComparer.Ordinal);

        foreach (string file in files)
        {
            // An exported project keeps .tres as .tres; the editor may hand back
            // a .remap for anything it converted, so both are accepted and the
            // suffix trimmed.
            string name = file.EndsWith(".remap", StringComparison.Ordinal) ? file[..^6] : file;

            if (!name.EndsWith(".tres", StringComparison.Ordinal))
                continue;

            if (GD.Load<StructureKind>($"{Folder}/{name}") is StructureKind kind)
                _kinds.Add(kind);
        }

        // Longest fragment first, so "water-disposal" is tested before "water"
        // and a specific kind is never shadowed by a general one.
        _kinds.Sort((a, b) => b.Match.Length.CompareTo(a.Match.Length));

        if (_kinds.Count == 0)
            GD.PushError($"[plant] no structure kinds found under {Folder}");
    }

    /// <summary>The kind a chain element is drawn as, or null if none claims it.</summary>
    public StructureKind? KindFor(string displayId)
    {
        for (int i = 0; i < _kinds.Count; i++)
        {
            if (displayId.Contains(_kinds[i].Match, StringComparison.Ordinal))
                return _kinds[i];
        }

        return null;
    }

    /// <summary>
    /// Lay a run of structures out in rows, each on its own plot.
    /// </summary>
    /// <param name="displayIds">The chain, in the order the engine publishes it.</param>
    /// <param name="centre">Where the yard is centred, in world pixels.</param>
    /// <param name="tilesAcross">How wide the yard may run before it wraps.</param>
    /// <remarks>
    /// A shelf packer: fill a row left to right until the next plot will not fit,
    /// then drop by the tallest plot in that row and start again. Simple, stable,
    /// and it never overlaps — which is the whole requirement, since two
    /// structures sharing ground is the thing that made the plant read as a shelf
    /// of icons.
    /// </remarks>
    public IReadOnlyList<Placed> Lay(IReadOnlyList<string> displayIds, Vector2 centre, int tilesAcross)
    {
        ArgumentNullException.ThrowIfNull(displayIds);

        var rows = new List<List<(StructureKind Kind, int X)>>();
        var row = new List<(StructureKind Kind, int X)>();
        var widths = new List<int>();
        var heights = new List<int>();

        int x = 0;
        int tallest = 0;

        for (int i = 0; i < displayIds.Count; i++)
        {
            StructureKind? kind = KindFor(displayIds[i]);

            if (kind is null)
                continue;

            Vector2I plot = kind.Plot;

            if (row.Count > 0 && x + plot.X > tilesAcross)
            {
                rows.Add(row);
                widths.Add(x);
                heights.Add(tallest);
                row = new List<(StructureKind, int)>();
                x = 0;
                tallest = 0;
            }

            row.Add((kind, x));
            x += plot.X;
            tallest = Mathf.Max(tallest, plot.Y);
        }

        if (row.Count > 0)
        {
            rows.Add(row);
            widths.Add(x);
            heights.Add(tallest);
        }

        int deep = 0;

        for (int i = 0; i < heights.Count; i++)
            deep += heights[i];

        var placed = new List<Placed>();
        int top = -(deep / 2);

        for (int r = 0; r < rows.Count; r++)
        {
            // Each row centred on its own width rather than left-aligned: a
            // short last row hanging off the left edge reads as a mistake.
            int left = -(widths[r] / 2);

            for (int c = 0; c < rows[r].Count; c++)
            {
                (StructureKind kind, int at) = rows[r][c];
                Vector2I plot = kind.Plot;

                var cell = new Rect2I(left + at, top, plot.X, plot.Y);

                // The sprite stands on the FOOTPRINT's centre, not the plot's —
                // they are the same point here because clearance is even on all
                // sides, and saying so keeps that true if it ever is not.
                var middle = new Vector2(
                    cell.Position.X + (cell.Size.X * 0.5f),
                    cell.Position.Y + (cell.Size.Y * 0.5f));

                placed.Add(new Placed(
                    kind,
                    centre + (middle * BasinWorld.TileSize),
                    cell));
            }

            top += heights[r];
        }

        return placed;
    }
}
