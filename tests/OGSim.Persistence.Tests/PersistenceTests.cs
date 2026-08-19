// R19's verification suite (SDD-013, PV1–PV6).

using OGSim.Kernel;
using OGSim.Persistence;

namespace OGSim.Persistence.Tests;

public static class Fx
{
    public static JsonObject Obj(params (string Key, JsonValue Value)[] members) =>
        new(members.ToDictionary(m => m.Key, m => m.Value, StringComparer.Ordinal));

    public static ModuleBlock Block(string module, JsonValue? state = null) =>
        new(module, state ?? Obj(("tick", new JsonInteger(12))));

    public static SaveHeader Header(
        IReadOnlyList<ModuleBlock> blocks,
        int schema = 3,
        IReadOnlyList<ModReference>? mods = null)
    {
        (IReadOnlyDictionary<string, string> perModule, string state) = SaveFile.Digest(blocks);

        return new SaveHeader(
            schema, "1.0.0", "content-1.0", mods ?? [],
            WorldSeed: 20240901UL, Epoch: new GameDate(1965, 1), Tick: new Tick(12),
            RngPositions: new Dictionary<string, ulong> { ["hazard"] = 42UL },
            ModuleDigests: perModule, StateDigest: state);
    }
}

public class CanonicalJsonTests
{
    // ------------------------------------------------------------ PV1

    [Fact] // PV1: object keys are ORDINAL-sorted, whatever order they arrive in
    public void PV1_object_keys_are_written_in_ordinal_order()
    {
        JsonObject unsorted = Fx.Obj(
            ("zulu", new JsonInteger(1)),
            ("alpha", new JsonInteger(2)),
            ("Mike", new JsonInteger(3)));

        // ORDINAL, not culture: uppercase sorts before lowercase. A
        // culture-sensitive sort orders differently under a Turkish locale,
        // which would make a save machine-specific and every cross-platform
        // digest comparison meaningless.
        Assert.Equal("{\"Mike\":3,\"alpha\":2,\"zulu\":1}", CanonicalJson.Write(unsorted));
    }

    [Fact] // PV1: two objects with the same members write identical bytes
    public void PV1_the_same_state_produces_the_same_bytes()
    {
        JsonObject a = Fx.Obj(("b", new JsonInteger(2)), ("a", new JsonInteger(1)));
        JsonObject b = Fx.Obj(("a", new JsonInteger(1)), ("b", new JsonInteger(2)));

        // This is what makes a per-module digest a divergence LOCATOR rather
        // than a checksum that changes for no reason.
        Assert.Equal(CanonicalJson.Write(a), CanonicalJson.Write(b));
    }

    [Fact] // PV1: doubles are shortest round-trip, and invariant
    public void PV1_doubles_round_trip_exactly()
    {
        double[] awkward =
        [
            0.1, 1.0 / 3.0, 1e-300, 1e300, Math.PI,
            -0.0, 5e-324, double.MaxValue, 0.5,
        ];

        foreach (double value in awkward)
        {
            string written = CanonicalJson.Write(new JsonDouble(value));
            JsonDouble read = Assert.IsType<JsonDouble>(CanonicalJson.Read(written));

            Assert.Equal(value, read.Value);

            // Never localised: a decimal comma would make the save unreadable
            // on a machine with a different locale.
            Assert.DoesNotContain(',', written);
        }
    }

    [Fact] // PV1: NaN and Infinity are UNREPRESENTABLE
    public void PV1_a_non_finite_double_cannot_be_persisted()
    {
        foreach (double bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var fault = Assert.Throws<SaveDataFault>(
                () => CanonicalJson.Write(new JsonDouble(bad)));

            // They were faults upstream. A save that could carry one would let a
            // broken tick be reloaded as a valid game.
            Assert.Contains("non-finite", fault.Fault.Detail);
        }
    }

    [Fact] // Money is integer cents, and stays exact above 2^53
    public void PV1_large_integers_survive_the_round_trip()
    {
        // A balance of ten trillion cents is a hundred billion currency units —
        // reachable, and beyond a double's exact integer range. Parsing it as a
        // double would silently lose the last digits.
        long[] big = [10_000_000_000_000_001L, long.MaxValue, long.MinValue, 0L, -1L];

        foreach (long value in big)
        {
            JsonValue read = CanonicalJson.Read(CanonicalJson.Write(new JsonInteger(value)));

            Assert.Equal(value, Assert.IsType<JsonInteger>(read).Value);
        }
    }

    [Fact] // PV1: Write(Read(s)) == s — one representation per value
    public void PV1_the_format_has_exactly_one_representation_per_value()
    {
        JsonValue nested = Fx.Obj(
            ("wells", new JsonArray(
            [
                Fx.Obj(("id", new JsonString("well-1")), ("depth", new JsonDouble(2500.5))),
                Fx.Obj(("id", new JsonString("well-2")), ("depth", new JsonDouble(3100.0))),
            ])),
            ("cash", new JsonInteger(1_234_567_890L)),
            ("live", new JsonBoolean(true)));

        string once = CanonicalJson.Write(nested);
        string twice = CanonicalJson.Write(CanonicalJson.Read(once));

        // The stronger round-trip: not merely that a value survives, but that
        // the TEXT does — which is what makes a digest meaningful.
        Assert.Equal(once, twice);
    }

    [Fact] // Strings with awkward characters survive
    public void PV1_escapes_round_trip()
    {
        string[] awkward = ["quote\"inside", "back\\slash", "line\nbreak", "tab\there", ""];

        foreach (string value in awkward)
        {
            JsonValue read = CanonicalJson.Read(CanonicalJson.Write(new JsonString(value)));
            Assert.Equal(value, Assert.IsType<JsonString>(read).Value);
        }
    }

    [Fact] // Malformed input is refused, with a position
    public void PV6_malformed_json_is_a_save_data_fault()
    {
        foreach (string bad in new[] { "{", "[1,", "{\"a\"}", "tru", "{} extra" })
            Assert.Throws<SaveDataFault>(() => CanonicalJson.Read(bad));
    }
}

public class SaveFileTests
{
    // ------------------------------------------------------------ digests

    [Fact] // Per-module digests, plus a state digest over them
    public void PV1_digests_are_computed_per_module()
    {
        IReadOnlyList<ModuleBlock> blocks =
            [Fx.Block("wells"), Fx.Block("facilities"), Fx.Block("company")];

        (IReadOnlyDictionary<string, string> perModule, string state) = SaveFile.Digest(blocks);

        Assert.Equal(3, perModule.Count);
        Assert.All(perModule.Values, d => Assert.Equal(64, d.Length));
        Assert.Equal(64, state.Length);
    }

    [Fact] // The state digest is stable under block ORDER
    public void PV1_the_state_digest_does_not_depend_on_block_order()
    {
        IReadOnlyList<ModuleBlock> one = [Fx.Block("a"), Fx.Block("b"), Fx.Block("c")];
        IReadOnlyList<ModuleBlock> other = [Fx.Block("c"), Fx.Block("a"), Fx.Block("b")];

        // Module-NAME order, not registration order: registration order is a
        // composition detail that could legitimately change between builds, and
        // a digest that moved with it would report divergence where there was
        // none.
        Assert.Equal(SaveFile.Digest(one).State, SaveFile.Digest(other).State);
    }

    [Fact] // A changed block changes ONLY its own module digest
    public void PV1_a_change_localises_to_one_module_digest()
    {
        IReadOnlyList<ModuleBlock> before = [Fx.Block("wells"), Fx.Block("company")];
        IReadOnlyList<ModuleBlock> after =
            [Fx.Block("wells", Fx.Obj(("tick", new JsonInteger(13)))), Fx.Block("company")];

        (IReadOnlyDictionary<string, string> a, string stateA) = SaveFile.Digest(before);
        (IReadOnlyDictionary<string, string> b, string stateB) = SaveFile.Digest(after);

        // This is the difference between a bug report and an investigation.
        Assert.NotEqual(a["wells"], b["wells"]);
        Assert.Equal(a["company"], b["company"]);
        Assert.NotEqual(stateA, stateB);
    }

    [Fact] // One module, one block
    public void PV1_two_blocks_for_one_module_is_a_save_data_fault()
    {
        var fault = Assert.Throws<SaveDataFault>(
            () => SaveFile.Digest([Fx.Block("wells"), Fx.Block("wells")]));

        Assert.Contains("two state blocks", fault.Fault.Detail);
    }

    // ------------------------------------------------------------ PV6

    [Fact] // A sound save loads
    public void PV6_a_consistent_save_loads()
    {
        IReadOnlyList<ModuleBlock> blocks = [Fx.Block("wells"), Fx.Block("company")];

        Loaded loaded = Assert.IsType<Loaded>(
            SaveFile.Validate(Fx.Header(blocks), blocks, supportedSchemaVersion: 3, []));

        Assert.Equal(2, loaded.Blocks.Count);
    }

    [Fact] // PV6: a digest mismatch NAMES THE MODULE
    public void PV6_a_tampered_block_names_the_module_whose_digest_diverged()
    {
        IReadOnlyList<ModuleBlock> original = [Fx.Block("wells"), Fx.Block("company")];
        SaveHeader header = Fx.Header(original);

        IReadOnlyList<ModuleBlock> tampered =
            [Fx.Block("wells", Fx.Obj(("tick", new JsonInteger(999)))), Fx.Block("company")];

        Refused refused = Assert.IsType<Refused>(
            SaveFile.Validate(header, tampered, 3, []));

        // "The save is corrupt" tells a player nothing they can act on.
        Assert.Contains(refused.Reasons, r => r.Contains("wells") && r.Contains("digest"));
        Assert.DoesNotContain(refused.Reasons, r => r.Contains("company"));
    }

    [Fact] // PV6: a save from the FUTURE is refused with both versions named
    public void PV6_a_newer_schema_is_refused_naming_both_versions()
    {
        IReadOnlyList<ModuleBlock> blocks = [Fx.Block("wells")];

        Refused refused = Assert.IsType<Refused>(
            SaveFile.Validate(Fx.Header(blocks, schema: 7), blocks, supportedSchemaVersion: 3, []));

        // Migrating forward is possible; migrating backward means guessing what
        // a later version meant, and a wrong guess corrupts a campaign silently.
        string reason = Assert.Single(refused.Reasons);
        Assert.Contains("7", reason);
        Assert.Contains("3", reason);
    }

    [Fact] // PV6: a missing mod names the mod AND the version
    public void PV6_a_missing_mod_is_named_with_its_version()
    {
        IReadOnlyList<ModuleBlock> blocks = [Fx.Block("wells")];
        IReadOnlyList<ModReference> needed = [new("deepwater-pack", "2.1", 0)];

        Refused refused = Assert.IsType<Refused>(
            SaveFile.Validate(Fx.Header(blocks, mods: needed), blocks, 3, []));

        Assert.Contains("deepwater-pack", Assert.Single(refused.Reasons));
        Assert.Contains("2.1", refused.Reasons[0]);
    }

    [Fact] // A wrong mod VERSION is a different message from a missing mod
    public void PV6_a_mod_version_mismatch_reports_both_versions()
    {
        IReadOnlyList<ModuleBlock> blocks = [Fx.Block("wells")];
        IReadOnlyList<ModReference> needed = [new("deepwater-pack", "2.1", 0)];
        IReadOnlyList<ModReference> installed = [new("deepwater-pack", "3.0", 0)];

        Refused refused = Assert.IsType<Refused>(
            SaveFile.Validate(Fx.Header(blocks, mods: needed), blocks, 3, installed));

        string reason = Assert.Single(refused.Reasons);
        Assert.Contains("3.0", reason);
        Assert.Contains("2.1", reason);
    }

    [Fact] // ALL reasons at once
    public void PV6_every_refusal_reason_is_reported_together()
    {
        IReadOnlyList<ModuleBlock> blocks = [Fx.Block("wells")];

        SaveHeader header = Fx.Header(blocks, schema: 9, mods: [new("missing-pack", "1.0", 0)]);

        IReadOnlyList<ModuleBlock> tampered =
            [Fx.Block("wells", Fx.Obj(("tick", new JsonInteger(0))))];

        Refused refused = Assert.IsType<Refused>(
            SaveFile.Validate(header, tampered, supportedSchemaVersion: 3, []));

        // A player who fixes a missing mod only to discover a version mismatch
        // has been made to pay twice for one piece of information.
        Assert.True(refused.Reasons.Count >= 3, $"only {refused.Reasons.Count} reported");
    }

    [Fact] // A block the header does not list, and a listed module with no block
    public void PV6_block_and_header_disagreements_are_both_reported()
    {
        IReadOnlyList<ModuleBlock> declared = [Fx.Block("wells"), Fx.Block("company")];
        SaveHeader header = Fx.Header(declared);

        IReadOnlyList<ModuleBlock> actual = [Fx.Block("wells"), Fx.Block("facilities")];

        Refused refused = Assert.IsType<Refused>(SaveFile.Validate(header, actual, 3, []));

        Assert.Contains(refused.Reasons, r => r.Contains("company") && r.Contains("no state block"));
        Assert.Contains(refused.Reasons, r => r.Contains("facilities") && r.Contains("does not list"));
    }
}

public class MigrationTests
{
    private sealed record Step(int From) : IMigrationStep
    {
        public JsonValue Migrate(JsonValue block, string module) =>
            block is JsonObject obj
                ? new JsonObject(new Dictionary<string, JsonValue>(obj.Members, StringComparer.Ordinal)
                {
                    [$"migrated-{From}"] = new JsonBoolean(true),
                })
                : block;
    }

    [Fact] // PV5: the chain composes v -> v+1
    public void PV5_the_chain_runs_every_step_in_order()
    {
        var chain = new MigrationChain([new Step(1), new Step(2), new Step(3)], 1, 4);

        JsonObject migrated = Assert.IsType<JsonObject>(
            chain.Migrate(Fx.Obj(("tick", new JsonInteger(1))), "wells", from: 1));

        Assert.True(migrated.Members.ContainsKey("migrated-1"));
        Assert.True(migrated.Members.ContainsKey("migrated-2"));
        Assert.True(migrated.Members.ContainsKey("migrated-3"));
    }

    [Fact] // Starting midway runs only the remaining steps
    public void PV5_migrating_from_a_later_version_skips_earlier_steps()
    {
        var chain = new MigrationChain([new Step(1), new Step(2), new Step(3)], 1, 4);

        JsonObject migrated = Assert.IsType<JsonObject>(
            chain.Migrate(Fx.Obj(), "wells", from: 3));

        Assert.False(migrated.Members.ContainsKey("migrated-1"));
        Assert.True(migrated.Members.ContainsKey("migrated-3"));
    }

    [Fact] // A GAP is a composition fault AT STARTUP
    public void PV5_a_gap_in_the_chain_is_refused_when_the_chain_is_built()
    {
        var fault = Assert.Throws<SaveDataFault>(
            () => new MigrationChain([new Step(1), new Step(3)], oldestSupported: 1, current: 4));

        // Not a load-time surprise: a chain missing v2->v3 cannot migrate a v2
        // save, and discovering that when a player opens one is the worst
        // possible moment.
        Assert.Contains("gaps", fault.Fault.Detail);
        Assert.Contains("2", fault.Fault.Detail);
    }

    [Fact] // Two steps from one version would make the chain ambiguous
    public void PV5_a_duplicate_step_is_refused()
    {
        var fault = Assert.Throws<SaveDataFault>(
            () => new MigrationChain([new Step(1), new Step(1)], 1, 2));

        Assert.Contains("ambiguous", fault.Fault.Detail);
    }

    [Fact] // Out-of-range versions are refused rather than guessed at
    public void PV5_versions_outside_the_supported_range_are_refused()
    {
        var chain = new MigrationChain([new Step(2), new Step(3)], oldestSupported: 2, current: 4);

        Assert.Throws<SaveDataFault>(() => chain.Migrate(Fx.Obj(), "wells", from: 1));
        Assert.Throws<SaveDataFault>(() => chain.Migrate(Fx.Obj(), "wells", from: 9));
    }
}
