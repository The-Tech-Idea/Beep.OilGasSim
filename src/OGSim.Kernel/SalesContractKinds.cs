// SDD-009 §7's R13.3 amendment (finding 250, revised) — take-or-pay as
// content. Rebalancing a committed volume or a penalty rate was a rebuild
// (Defaults.TakeOrPay, a hardcoded record) until this file existed, which is
// design 03's eleventh non-negotiable — "rebalancing is a content edit" —
// false of the one sales contract the game shipped, the same defect
// R20c.9.2's own header describes for the six facility ladders before it.
//
// UNGATED, unlike a facility unit: a sales contract has no era and no rung —
// it is a single negotiated instrument, not a purchasable progression — so
// this mirrors PropertyKindContentKind's flat shape, not FacilityContentKind's
// gated one.

using System.Text.Json;

namespace OGSim.Kernel;

/// <summary>SDD-009 §7's take-or-pay terms, as content.</summary>
public sealed record TakeOrPayDefinition(
    ContentId Id,
    SurfaceVolume CommittedVolume,    // per window, stock-tank m³
    int WindowMonths,
    Money PenaltyRate) : ContentDefinition(Id);

public sealed class TakeOrPayContentKind : IContentKind
{
    public string Name => "take-or-pay";

    public ContentDefinition Read(JsonElement element)
    {
        var id = new ContentId(PropertyKindContentKind.Required(element, "id").GetString()!);

        double committedM3 = PropertyKindContentKind.Required(
            element, "committedVolumeCubicMetres").GetDouble();

        int windowMonths = PropertyKindContentKind.Required(element, "windowMonths").GetInt32();

        // Authored in dollars per m³, the units a rebalance is actually read
        // and written in; converted once, here, to the ledger's cents — the
        // one double→Money crossing this value ever makes (SDD-009 §1).
        double penaltyRateDollarsPerM3 = PropertyKindContentKind.Required(
            element, "penaltyRateDollarsPerCubicMetre").GetDouble();

        return new TakeOrPayDefinition(
            id,
            new SurfaceVolume(committedM3),
            windowMonths,
            Money.RoundHalfEven(penaltyRateDollarsPerM3 * 100.0));
    }

    public IReadOnlyList<ContentReference> ReferencesOf(ContentDefinition definition) => [];

    public IReadOnlyList<string> ConsistencyProblems(ContentDefinition definition)
    {
        var problems = new List<string>();
        if (definition is not TakeOrPayDefinition contract) return problems;

        if (contract.CommittedVolume.CubicMetres <= 0.0)
            problems.Add(
                "committedVolumeCubicMetres must be positive; a commitment of zero is not one");

        if (contract.WindowMonths <= 0)
            problems.Add($"windowMonths {contract.WindowMonths} is not positive");

        if (contract.PenaltyRate.Cents < 0)
            problems.Add("penaltyRateDollarsPerCubicMetre cannot be negative");

        return problems;
    }

    public IReadOnlyList<PluginBinding> PluginsOf(ContentDefinition definition) => [];
}
