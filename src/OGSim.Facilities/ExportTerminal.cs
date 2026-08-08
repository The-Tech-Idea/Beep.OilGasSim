// R20d.8 — export capacity (SDD-006 §7b, §0c).
//
// THE FIELD'S LAST CEILING. Everything upstream of here can be debottlenecked —
// a bigger vessel, another well, a wider line — and none of it produces a barrel
// more if the export line will not take it. The tank fills, the ullage
// constraint reaches back down the chain, and wells shut themselves in.
//
// IT WAS A CONSTANT, and that was the reason a big field played exactly like a
// small one (finding 165). What is in the ground decides what CAN be produced;
// this decides how much of it a company ever sells.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Facilities;

/// <summary>What an export line contracts to take (SDD-006 §7b).</summary>
public sealed record ExportTier(ContentId Id, MassRate Offtake);

/// <summary>
/// The socket. Its identity is the terminal's and does not change when a bigger
/// line is laid — the berth, the metering and the sales contract stay exactly
/// where they were (SDD-006 §0c).
/// </summary>
public sealed class ExportTerminal
{
    public ExportTerminal(EntityRef id, ExportTier fitted)
    {
        ArgumentNullException.ThrowIfNull(fitted);

        if (fitted.Offtake.KgPerSecond <= 0.0)
            throw new ContentFault("SDD-006 §7b", null,
                $"export tier '{fitted.Id.Value}' takes nothing; a field with no route to " +
                "market is expressed by having no terminal, not by having one that lifts zero");

        Id = id;
        Tier = fitted;
    }

    /// <summary>What an activity aims at when it expands the field's export.
    /// The socket's identity, which a bigger line does not change.</summary>
    public EntityRef Id { get; }

    /// <summary>What is fitted now. The rate stage 6 draws from the tank
    /// against, and the one fact about export capacity there is (law L5).</summary>
    public ExportTier Tier { get; private set; }

    /// <summary>
    /// Lay a bigger line. Called only by a COMPLETED expansion — the months and
    /// the money are what make the capacity a decision rather than a setting.
    /// </summary>
    public void Fit(ExportTier tier)
    {
        ArgumentNullException.ThrowIfNull(tier);
        Tier = tier;
    }
}
