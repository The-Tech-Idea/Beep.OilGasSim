// R20c.9 — the technology tree as loadable content (SDD-004 §8, SDD-005 §2).
//
// The kind lives HERE rather than in the kernel's ContentKinds because its
// datasheet is written in this module's vocabulary — TechnologyId, Era,
// AcquisitionRoute, DetectClass. The kernel's three bootstrap kinds are the ones
// every module needs; a kind belongs with the module that consumes it (R3.7).

using System.Text;
using System.Text.Json;
using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Capabilities;

/// <summary>The four routes of design 07 §3 — TECH_TREE's `R L S D` column.</summary>
public enum AcquisitionRoute
{
    Research,
    Licence,
    ServiceRental,
    Diffusion,
}

/// <summary>
/// One `tech` entry — a node of <see cref="plans/catalog/TECH_TREE.md"/>,
/// mapping one-to-one onto the registry's columns.
/// </summary>
public sealed record TechnologyDefinition(
    ContentId Id,
    Era AvailableFrom,
    int DiffusionLagTicks,
    IReadOnlyList<ContentId> Prerequisites,
    IReadOnlyList<AcquisitionRoute> Routes,
    DetectClass? GrantsDetectClass,
    IReadOnlyList<Effect> Effects,

    /// <summary>The Licence route's terms where offered (design 07 §3:
    /// immediate, no ownership, expensive forever) — R17.3/R17.4, finding
    /// 293.</summary>
    TechnologyLicenceTerms? Licence,

    /// <summary>The R&amp;D route's terms where offered (design 07 §3: a
    /// sustained budget over months; cheapest per unit and you own it).</summary>
    TechnologyResearchTerms? Research) : ContentDefinition(Id)
{
    // Finding 131 — the fixture test compares shipped content against the
    // registry, and a mod override replaces an entry whole (SDD-004 §7).
    public bool Equals(TechnologyDefinition? other) =>
        other is not null && Id == other.Id && AvailableFrom == other.AvailableFrom
        && DiffusionLagTicks == other.DiffusionLagTicks
        && GrantsDetectClass == other.GrantsDetectClass
        && Structural.Equal(Prerequisites, other.Prerequisites)
        && Structural.Equal(Routes, other.Routes)
        && Structural.Equal(Effects, other.Effects);

    public override int GetHashCode() =>
        HashCode.Combine(Id, AvailableFrom, DiffusionLagTicks, GrantsDetectClass,
            Structural.HashOf(Prerequisites), Structural.HashOf(Routes),
            Structural.HashOf(Effects));
}

/// <summary>
/// SDD-004 §8's mapping rule: content id = deterministic slug of the registry
/// display name. ONE function, used by the content author and by the fixture
/// test that checks them against each other — two implementations would let the
/// registry and the content drift while both looked right.
/// </summary>
public static class TechnologySlug
{
    /// <summary>
    /// Lowercase; `/`, spaces and `·` become `-`; subscript digits fold to their
    /// ordinary form (`CO₂ flood` → `co2-flood`); anything else that is not a
    /// letter, digit or `-` is dropped. Runs of `-` collapse.
    /// </summary>
    public static string Of(string displayName)
    {
        ArgumentNullException.ThrowIfNull(displayName);

        var slug = new StringBuilder(displayName.Length);

        for (int i = 0; i < displayName.Length; i++)
        {
            char c = displayName[i];

            // The expansion flag is an authoring annotation, not part of a name.
            if (c is '⚑') continue;

            if (c is '/' or ' ' or '·' or '-' or '‑' or '–')
            {
                Append(slug, '-');
                continue;
            }

            char folded = Fold(c);
            if (char.IsAsciiLetterOrDigit(folded)) Append(slug, char.ToLowerInvariant(folded));
        }

        return slug.ToString().Trim('-');
    }

    private static void Append(StringBuilder slug, char c)
    {
        // Collapse runs, and never lead with a separator.
        if (c == '-' && (slug.Length == 0 || slug[^1] == '-')) return;
        slug.Append(c);
    }

    /// <summary>Subscript digits to ordinary ones — the ₂ of CO₂.</summary>
    private static char Fold(char c) => c switch
    {
        >= '₀' and <= '₉' => (char)('0' + (c - '₀')),
        _ => c,
    };
}

public sealed class TechnologyContentKind : IContentKind
{
    public string Name => "tech";

    public ContentDefinition Read(JsonElement element)
    {
        var id = new ContentId(Required(element, "id").GetString()!);
        Era era = ParseEnum<Era>(Required(element, "availableFrom"));

        // Whole months. Not a unit string: the diffusion lag is counted in ticks
        // and the tick IS the month (SDD-001 §4), so a duration with a unit
        // would invite "18 months" and "1.5 years" to disagree by rounding.
        int lag = Required(element, "diffusionLagTicks").GetInt32();

        var prerequisites = new List<ContentId>();
        if (element.TryGetProperty("prerequisites", out JsonElement prereqs))
            foreach (JsonElement prereq in prereqs.EnumerateArray())
                prerequisites.Add(new ContentId(prereq.GetString()!));

        var routes = new List<AcquisitionRoute>();
        foreach (JsonElement route in Required(element, "routes").EnumerateArray())
            routes.Add(ParseEnum<AcquisitionRoute>(route));

        DetectClass? grants = element.TryGetProperty("grantsDetectClass", out JsonElement detect)
            ? ParseEnum<DetectClass>(detect)
            : null;

        // Effects is hardcoded empty (finding 241, SDD-005 §4.2's R20d.10e
        // amendment) — no SDD specifies a JSON grammar for the four-record
        // Effect vocabulary, and no shipped node needs one yet (SDD-005's own
        // R20c.9 correction: a node carries an effect only where it changes "a
        // number nobody bought", true of at most Arctic operations among the
        // sixty-five shipped nodes, and that node's consumer — an
        // environment-side restriction and an EnvelopeCheck on a real
        // operation — does not exist either). Writing the parser before a real
        // consumer exists would be the same defect this hardcode already is,
        // moved one layer down (CLAUDE.md rule 6). Build it when a phase adds
        // the first technology that genuinely needs one.
        TechnologyLicenceTerms? licence =
            element.TryGetProperty("licence", out JsonElement licenceTerms)
                ? new TechnologyLicenceTerms(
                    Required(licenceTerms, "feeMillionsPerTick").GetDouble())
                : null;

        TechnologyResearchTerms? research =
            element.TryGetProperty("research", out JsonElement researchTerms)
                ? new TechnologyResearchTerms(
                    Required(researchTerms, "months").GetInt32(),
                    Required(researchTerms, "costMillionsPerMonth").GetDouble())
                : null;

        return new TechnologyDefinition(
            id, era, lag, prerequisites, routes, grants, [], licence, research);
    }

    /// <summary>
    /// Stage 4 resolves prerequisites against the loaded index, so a node naming
    /// a technology that no file declares fails at LOAD rather than on the tick
    /// a player tries to research it.
    /// </summary>
    public IReadOnlyList<ContentReference> ReferencesOf(ContentDefinition definition)
    {
        if (definition is not TechnologyDefinition tech) return [];

        var references = new List<ContentReference>(tech.Prerequisites.Count);
        for (int i = 0; i < tech.Prerequisites.Count; i++)
            references.Add(new ContentReference(
                "tech", tech.Prerequisites[i], $"$.prerequisites[{i}]"));

        return references;
    }

    public IReadOnlyList<string> ConsistencyProblems(ContentDefinition definition)
    {
        var problems = new List<string>();
        if (definition is not TechnologyDefinition tech) return problems;

        if (tech.DiffusionLagTicks < 0)
            problems.Add($"diffusionLagTicks {tech.DiffusionLagTicks} is negative; " +
                         "a technology cannot become standard before its era begins");

        if (tech.Routes.Count == 0)
            problems.Add("routes is empty; a technology reachable by no route can never " +
                         "be held, which is a node that should not exist");

        // A lag on a node that cannot diffuse is a contradiction rather than a
        // harmless extra: one of the two fields is wrong, and guessing which
        // would mean guessing whether the player gets it free.
        if (tech.DiffusionLagTicks > 0 && !tech.Routes.Contains(AcquisitionRoute.Diffusion))
            problems.Add($"diffusionLagTicks is {tech.DiffusionLagTicks} but 'Diffusion' is " +
                         "not among the routes; the lag would never elapse");

        for (int i = 0; i < tech.Prerequisites.Count; i++)
            if (tech.Prerequisites[i] == tech.Id)
                problems.Add($"{tech.Id.Value} lists itself as a prerequisite");

        return problems;
    }

    /// <summary>A technology names no model plugin: it gates and it modifies,
    /// and both are expressed as effects rather than as implementations.</summary>
    public IReadOnlyList<PluginBinding> PluginsOf(ContentDefinition definition) => [];

    private static JsonElement Required(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value)
            ? value
            : throw new JsonException($"missing required property '{name}'");

    private static TEnum ParseEnum<TEnum>(JsonElement element) where TEnum : struct, Enum =>
        Enum.TryParse(element.GetString(), ignoreCase: true, out TEnum parsed)
            ? parsed
            : throw new JsonException(
                $"'{element.GetString()}' is not one of " +
                $"{string.Join(", ", Enum.GetNames<TEnum>())}");
}
