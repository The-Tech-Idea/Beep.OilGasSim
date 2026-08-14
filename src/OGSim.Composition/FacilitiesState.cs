// R20d.12 — what the chain owns between ticks (SDD-006 §8b, finding 197).
//
// FACILITIES REGISTERED NO STATE OWNER, so nothing the surface chain holds
// reached a container — and what it holds is everything a company BUYS. Six
// sockets carry a fitted tier apiece: the separator that answers a bottleneck,
// the export line that costs more than any well, the gas plant that answers the
// flaring penalty, the treater that lets a watering-out field sell at all, the
// manifold that decides how many wells can tie in, and the tank. A reloaded
// company had the equipment it started with and the cash it had already spent.
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

internal sealed class FacilitiesState(SurfaceChain chain) : IStateOwner
{
    public StateKey Key { get; } = new("facilities.units");

    public int SchemaVersion => 1;

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
    }

    public void Restore(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        chain.Manifold.Fit(
            Rung(Defaults.ManifoldLadder, reader.ReadString("manifold-tier"), "manifold",
                 tier => tier.Id));

        chain.Separator.Fit(
            Rung(Defaults.SeparatorLadder, reader.ReadString("separator-tier"), "separator",
                 tier => tier.Id));

        chain.Tank.Fit(
            Rung(Defaults.TankLadder, reader.ReadString("tank-tier"), "tank",
                 tier => tier.Id));

        chain.GasPlant.Fit(
            Rung(Defaults.GasPlantLadder, reader.ReadString("gas-plant-tier"), "gas plant",
                 tier => tier.Id));

        chain.Treater.Fit(
            Rung(Defaults.TreaterLadder, reader.ReadString("treater-tier"), "treater",
                 tier => tier.Id));
    }

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
    internal sealed class ExportState(OGSim.Facilities.ExportTerminal terminal) : IStateOwner
    {
        public StateKey Key { get; } = new("field.export");

        public int SchemaVersion => 1;

        public void Capture(IStateWriter writer)
        {
            ArgumentNullException.ThrowIfNull(writer);

            writer.WriteString("tier", terminal.Tier.Id.Value);
        }

        public void Restore(IStateReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);

            terminal.Fit(
                Rung(Defaults.ExportLadder, reader.ReadString("tier"), "export terminal",
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
