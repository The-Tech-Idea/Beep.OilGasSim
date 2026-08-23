// R20d.12 — what the chain owns between ticks (SDD-006 §8b, finding 197).
//
// FACILITIES REGISTERED NO STATE OWNER, so nothing the surface chain holds
// reached a container — and what it holds is everything a company BUYS. Eight
// sockets carry a fitted tier apiece: the separator that answers a bottleneck,
// the export line that costs more than any well, the gas plant that answers the
// flaring penalty, the treater that lets a watering-out field sell at all, the
// manifold that decides how many wells can tie in, the tank, the compressor
// that raises what reaches the plant (R9.1, finding 257), and the pump station
// that does the same for the oil leg (R11.2, finding 259). A reloaded company
// had the equipment it started with and the cash it had already spent.
//
// IT WAS INVISIBLE FOR A PARTICULAR REASON and the reason is worth keeping: the
// continuation test drills and floods and never INSTALLS, so two engines could
// be compared month after month for two years while every upgrade quietly went
// missing. A test cannot see equipment nobody bought.
//
// HERE RATHER THAN IN OGSim.Facilities, because a tier is restored by NAME
// through the ladder that fitted it and the ladders are composition's content
// (SDD-006 §8b). The same reasoning that puts `SubsurfaceState.DriveNamed`
// inside the module that owns drives puts this inside the layer that owns
// ladders.

using OGSim.Kernel;

namespace OGSim.Composition;

internal sealed class FacilitiesState(SurfaceChain chain, FacilityLadders ladders)
    : IStateOwner
{
    public StateKey Key { get; } = new("facilities.units");

    // R11.2's own composition (finding 259): the pump station joined the
    // seven sockets this block already carried.
    public int SchemaVersion => 3;

    /// <summary>Nothing has to be back before this is (SDD-013 §2b).</summary>
    public IReadOnlyList<StateKey> RestoreAfter => [];

    public void Capture(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        // THE FITTED RUNG IS THE PURCHASE (SDD-006 §0c's refit). The socket keeps
        // its identity across an upgrade and what is fitted into it changes, so
        // the tier's id is the whole of what a save has to carry about it.
        writer.WriteString("manifold-tier", chain.Manifold.Tier.Id.Value);
        writer.WriteString("separator-tier", chain.Separator.Tier.Id.Value);
        writer.WriteString("tank-tier", chain.Tank.Tier.Id.Value);
        writer.WriteString("gas-plant-tier", chain.GasPlant.Tier.Id.Value);
        writer.WriteString("treater-tier", chain.Treater.Tier.Id.Value);
        writer.WriteString("compressor-tier", chain.Compressor.Tier.Id.Value);
        writer.WriteString("pump-station-tier", chain.PumpStation.Tier.Id.Value);

        // WHAT THE CHAIN IS HOLDING, as distinct from what it IS. A line
        // restored empty delivers its first month's oil out of nowhere (§6's V7
        // term), and an intake restored at zero stops buying the water a flood
        // was ordered to put back.
        Inventory(writer, "linefill", chain.Flowline.Linefill);

        writer.WriteDouble("intake-commanded", chain.Intake.Commanded.CubicMetresPerSecond);

        // THE TANK'S CONTENTS — oil the company owns, and the ullage every
        // export decision is taken against. Its PROVENANCE travels with it,
        // because a barrel is credited to the compartment it came from and a
        // restored blend that had forgotten whose oil it was would allocate the
        // next sale to the wrong reservoir.
        Inventory(writer, "tank-held", chain.Tank.Held);

        writer.WriteInt64("tank-shares", chain.Tank.Provenance.Shares.Length);

        for (int i = 0; i < chain.Tank.Provenance.Shares.Length; i++)
        {
            (EntityRef compartment, double fraction) = chain.Tank.Provenance.Shares[i];
            string at = Ordinal("tank-share", i);

            writer.WriteInt64(at + ".kind", (long)compartment.Kind);
            writer.WriteInt64(at + ".id", (long)compartment.Value);
            writer.WriteDouble(at + ".fraction", fraction);
        }
    }

    public void Restore(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        chain.Manifold.Fit(
            Rung(ladders.Manifold, reader.ReadString("manifold-tier"), "manifold",
                 tier => tier.Id));

        chain.Separator.Fit(
            Rung(ladders.Separator, reader.ReadString("separator-tier"), "separator",
                 tier => tier.Id));

        chain.Tank.Fit(
            Rung(ladders.Tank, reader.ReadString("tank-tier"), "tank",
                 tier => tier.Id));

        chain.GasPlant.Fit(
            Rung(ladders.GasPlant, reader.ReadString("gas-plant-tier"), "gas plant",
                 tier => tier.Id));

        chain.Treater.Fit(
            Rung(ladders.Treater, reader.ReadString("treater-tier"), "treater",
                 tier => tier.Id));

        chain.Compressor.Fit(
            Rung(ladders.Compressor, reader.ReadString("compressor-tier"), "compressor",
                 tier => tier.Id));

        chain.PumpStation.Fit(
            Rung(ladders.PumpStation, reader.ReadString("pump-station-tier"), "pump station",
                 tier => tier.Id));

        chain.Flowline.CommitLinefill(Inventory(reader, "linefill"));

        chain.Intake.Command(new ReservoirRate(reader.ReadDouble("intake-commanded")));

        var shares =
            System.Collections.Immutable.ImmutableArray.CreateBuilder<(EntityRef, double)>();

        var count = (int)reader.ReadInt64("tank-shares");

        for (var i = 0; i < count; i++)
        {
            string at = Ordinal("tank-share", i);

            shares.Add((
                new EntityRef(
                    (EntityKind)reader.ReadInt64(at + ".kind"),
                    (ulong)reader.ReadInt64(at + ".id")),
                reader.ReadDouble(at + ".fraction")));
        }

        chain.Tank.RestoreTo(
            Inventory(reader, "tank-held"), Allocation.Validated(shares.ToImmutable()));
    }

    /// <summary>
    /// An inventory, ordinal by ordinal.
    ///
    /// <para>By ORDINAL POSITION and with the count written, because the
    /// catalogue assigns ordinals and a save that assumed a fixed material set
    /// would silently re-key every mass the day one is added — which is the
    /// failure SDD-004 §6 keeps ordinals out of content to avoid.</para>
    /// </summary>
    private static void Inventory(IStateWriter writer, string what, MaterialInventory held)
    {
        writer.WriteInt64(what + "-count", held.KilogramsByOrdinal.Length);

        for (int i = 0; i < held.KilogramsByOrdinal.Length; i++)
            writer.WriteDouble(Ordinal(what, i), held.KilogramsByOrdinal[i]);
    }

    private static MaterialInventory Inventory(IStateReader reader, string what)
    {
        var count = (int)reader.ReadInt64(what + "-count");
        var kilograms = new double[count];

        for (int i = 0; i < count; i++) kilograms[i] = reader.ReadDouble(Ordinal(what, i));

        return MaterialInventory.Of(kilograms);
    }

    private static string Ordinal(string what, int index) =>
        what + "." + index.ToString("D2", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// The export terminal's rung, owned separately because the terminal is
    /// composed by the FIELD module rather than carried on
    /// <see cref="SurfaceChain"/> (SDD-006 §8b).
    ///
    /// <para>A second owner rather than a second copy: law L5 gives one owner
    /// per fact, and the alternative — reaching the terminal from the facilities
    /// block — would mean the field module handing its element to another
    /// module's owner and two places believing they hold it. The key says which
    /// module composed it, which is what a state key is for.</para>
    ///
    /// <para>The honest alternative is to move the terminal onto the chain so
    /// all six rungs sit in one block. That is a composition restructure and it
    /// belongs in its own change: the export line is the most expensive purchase
    /// in the catalogue and shipping it unsaved while the restructure is designed
    /// would be the wrong order.</para>
    /// </summary>
    /// <summary>
    /// The export terminal's own owner, and it carries its OWN ladder reference
    /// rather than the outer class's: a nested type is not an inner one here, so
    /// there is no enclosing instance to read — and threading it explicitly is
    /// what makes the dependency visible at the one place that constructs it.
    /// </summary>
    internal sealed class ExportState(
        OGSim.Facilities.ExportTerminal terminal, FacilityLadders ladders) : IStateOwner
    {
        public StateKey Key { get; } = new("field.export");

        public int SchemaVersion => 1;

        /// <summary>Nothing has to be back before this is (SDD-013 §2b).</summary>
        public IReadOnlyList<StateKey> RestoreAfter => [];

        public void Capture(IStateWriter writer)
        {
            ArgumentNullException.ThrowIfNull(writer);

            writer.WriteString("tier", terminal.Tier.Id.Value);
        }

        public void Restore(IStateReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);

            terminal.Fit(
                Rung(ladders.Export, reader.ReadString("tier"), "export terminal",
                     tier => tier.Id));
        }
    }

    /// <summary>
    /// The rung a save names, found by CONTENT ID and never by index.
    ///
    /// <para>A ladder is an authored progression (SDD-006 §7b) and its order may
    /// legitimately change between builds; an index would silently refit a
    /// different vessel — a save that quietly gave a company the wrong equipment
    /// rather than refusing. An id this build's ladder does not contain is a
    /// refusal naming it, which is design 11 §2.1's rule that a reference which
    /// fails to resolve on restore is a fault and never a silent drop.</para>
    /// </summary>
    private static TTier Rung<TTier>(
        IReadOnlyList<TTier> ladder, string id, string what, Func<TTier, ContentId> idOf)
    {
        for (int i = 0; i < ladder.Count; i++)
            if (string.Equals(idOf(ladder[i]).Value, id, StringComparison.Ordinal))
                return ladder[i];

        throw new SaveDataFault("SDD-006 §8b", null,
            $"the save fits '{id}' to the {what} and this build's ladder has no such rung; " +
            "restoring the nearest one would hand a company equipment it never bought");
    }
}
