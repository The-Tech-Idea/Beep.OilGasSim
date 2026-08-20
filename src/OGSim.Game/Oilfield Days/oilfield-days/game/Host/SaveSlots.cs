#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;

namespace OilfieldDays.Host;

/// <summary>
/// Where saved games live, and what the host has to remember alongside them.
///
/// <para><b>The split is R19 §5's:</b> the engine owns the payload and the host
/// owns slots, paths and file I/O. <c>SaveGame.Write</c> is handed an open stream
/// and leaves it open; everything about <em>where</em> that stream points is
/// here.</para>
///
/// <para><b>The sidecar is not a second copy of engine state.</b> A save carries
/// the world seed and the tick, which is everything the engine needs — but the
/// host also drew a basin from that seed at a particular size, land fraction and
/// climate, and those are the client's own presentation choices. Reloading
/// without them would rebuild the same simulation under different ground. So the
/// draft rides beside the save in a small JSON file, and nothing in it is a fact
/// the engine also holds.</para>
/// </summary>
public static class SaveSlots
{
    private const string Folder = "user://saves";

    /// <summary>A slot, as the load screen lists it.</summary>
    public sealed record Slot(
        string Name,
        string SavePath,
        string SidecarPath,
        EngineHost.NewGameDraft Draft,
        string Yard,
        int Tick,
        string Company,
        double Cash,
        int Wells);

    /// <summary>Every saved game, newest first.</summary>
    public static IReadOnlyList<Slot> All()
    {
        var slots = new List<Slot>();

        DirAccess.MakeDirRecursiveAbsolute(Folder);

        using DirAccess? directory = DirAccess.Open(Folder);

        if (directory is null)
        {
            GD.PushError($"[saves] cannot open {Folder}: {DirAccess.GetOpenError()}");

            return slots;
        }

        string[] files = directory.GetFiles();
        Array.Sort(files, StringComparer.Ordinal);

        foreach (string file in files)
        {
            if (!file.EndsWith(".json", StringComparison.Ordinal))
                continue;

            string name = file[..^5];
            Slot? slot = Describe(name);

            if (slot is not null)
                slots.Add(slot);
        }

        // Newest first: the slot a player wants is nearly always the last one
        // they wrote, which is what Continue takes.
        slots.Sort((a, b) => b.Name.CompareTo(a.Name, StringComparison.Ordinal));

        return slots;
    }

    /// <summary>The most recent slot, or null if nothing has been saved.</summary>
    public static Slot? Newest()
    {
        IReadOnlyList<Slot> slots = All();

        return slots.Count > 0 ? slots[0] : null;
    }

    /// <summary>
    /// A slot name that sorts by when it was made.
    /// </summary>
    /// <remarks>
    /// Built from the in-game tick and a counter rather than a wall clock. The
    /// engine bans <c>DateTime.Now</c> because it makes a run irreproducible, and
    /// while a file name is not simulation state, a save list ordered by the
    /// player's clock is one that reorders itself when a machine's clock moves.
    /// </remarks>
    public static string NameFor(int tick)
    {
        string stem = $"m{tick.ToString("0000", CultureInfo.InvariantCulture)}";
        int spare = 0;

        while (FileAccess.FileExists($"{Folder}/{stem}-{spare.ToString("00", CultureInfo.InvariantCulture)}.json"))
            spare++;

        return $"{stem}-{spare.ToString("00", CultureInfo.InvariantCulture)}";
    }

    public static string SavePathOf(string name) => $"{Folder}/{name}.ogsave";

    public static string SidecarPathOf(string name) => $"{Folder}/{name}.json";

    /// <summary>Write the host's half of a slot.</summary>
    public static bool WriteSidecar(
        string name, EngineHost.NewGameDraft draft, string yard, int tick, double cash, int wells)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var record = new Godot.Collections.Dictionary
        {
            { "seed", draft.Seed.ToString(CultureInfo.InvariantCulture) },
            { "profile", draft.RealityProfile },
            { "template", draft.WorldTemplate },
            { "cells", draft.Cells },
            { "land", draft.LandFraction },
            { "richness", draft.ResourceRichness },
            { "maturity", draft.BasinMaturity },
            { "climate", draft.ClimateSeverity },
            { "rivals", draft.RivalCount },
            { "era", (int)draft.StartEra },
            { "company", draft.CompanyName },
            { "startYear", draft.StartYear },
            { "yard", yard },
            { "tick", tick },
            { "cash", cash },
            { "wells", wells },
        };

        using FileAccess? handle = FileAccess.Open(SidecarPathOf(name), FileAccess.ModeFlags.Write);

        if (handle is null)
        {
            GD.PushError($"[saves] cannot write {SidecarPathOf(name)}: {FileAccess.GetOpenError()}");

            return false;
        }

        handle.StoreString(Json.Stringify(record, indent: "  "));

        return true;
    }

    /// <summary>Read a slot's description, or null if it will not parse.</summary>
    public static Slot? Describe(string name)
    {
        using FileAccess? handle = FileAccess.Open(SidecarPathOf(name), FileAccess.ModeFlags.Read);

        if (handle is null)
            return null;

        Variant parsed = Json.ParseString(handle.GetAsText());

        if (parsed.VariantType != Variant.Type.Dictionary)
        {
            GD.PushError($"[saves] {SidecarPathOf(name)} is not a JSON object");

            return null;
        }

        var record = parsed.AsGodotDictionary();

        if (!FileAccess.FileExists(SavePathOf(name)))
        {
            // A sidecar without its payload is a half-written slot, not a game.
            // Listing it would offer a load that cannot happen.
            GD.PushWarning($"[saves] {name} has no engine payload and was skipped");

            return null;
        }

        if (!ulong.TryParse(Text(record, "seed"), out ulong seed))
            return null;

        var draft = new EngineHost.NewGameDraft(
            Seed: seed,
            RealityProfile: Text(record, "profile"),
            WorldTemplate: Text(record, "template"),
            Cells: (int)Number(record, "cells"),
            LandFraction: Number(record, "land"),
            ResourceRichness: Number(record, "richness"),
            BasinMaturity: Number(record, "maturity"),
            ClimateSeverity: Number(record, "climate"),
            RivalCount: (int)Number(record, "rivals"),
            StartEra: (OGSim.Kernel.Era)(int)Number(record, "era"))
        {
            CompanyName = Text(record, "company"),
            StartYear = (int)Number(record, "startYear"),
        };

        return new Slot(
            name,
            SavePathOf(name),
            SidecarPathOf(name),
            draft,
            Text(record, "yard"),
            (int)Number(record, "tick"),
            draft.CompanyName,
            Number(record, "cash"),
            (int)Number(record, "wells"));
    }

    /// <summary>Remove a slot, payload and sidecar together.</summary>
    public static void Delete(Slot slot)
    {
        ArgumentNullException.ThrowIfNull(slot);

        using DirAccess? directory = DirAccess.Open(Folder);

        if (directory is null)
        {
            GD.PushError($"[saves] cannot open {Folder}: {DirAccess.GetOpenError()}");

            return;
        }

        Report(directory.Remove(slot.SavePath), slot.SavePath);
        Report(directory.Remove(slot.SidecarPath), slot.SidecarPath);
    }

    private static void Report(Error error, string path)
    {
        if (error != Error.Ok)
            GD.PushError($"[saves] could not remove {path}: {error}");
    }

    private static string Text(Godot.Collections.Dictionary record, string key) =>
        record.TryGetValue(key, out Variant value) ? value.AsString() : string.Empty;

    private static double Number(Godot.Collections.Dictionary record, string key) =>
        record.TryGetValue(key, out Variant value) ? value.AsDouble() : 0.0;
}
