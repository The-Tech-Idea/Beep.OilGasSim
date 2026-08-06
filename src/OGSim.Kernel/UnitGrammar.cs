// R3.1 — the content unit-string grammar (SDD-004 §4, decision CD2).
//
// Content writes "3200 psi", not 3200 with a comment saying psi. The engine is
// SI throughout, so every quantity crosses one conversion at load and never
// again — which is what makes the unit-error class the design 05 §2 note calls
// "a classic source of unit errors" structurally absent rather than tested for.
//
// THE TABLE DOES NOT LIVE IN PhysicalConstants, though SDD-004 §4 said it did.
// PhysicalConstants is rule F-2's home: one file where every entry is a PHYSICAL
// constant carrying its SDD citation and unit. A token-to-dimension map is
// content-pipeline machinery with no physics in it, and putting it there would
// dissolve the one property that makes F-2 checkable.

using System.Globalization;

namespace OGSim.Kernel;

/// <summary>One unit token: which dimension it belongs to, and how it reaches SI.</summary>
/// <param name="Factor">SI = value · Factor + Offset.</param>
/// <param name="Offset">Non-zero only for the affine temperature scales.</param>
public readonly record struct UnitConversion(Dimension Dimension, double Factor, double Offset);

/// <summary>
/// The CLOSED unit vocabulary (SDD-004 §4). Closed deliberately: an unknown
/// token is a load error naming the nearest known one, never a silent pass-through
/// of a number whose meaning nobody established.
/// </summary>
public static class UnitGrammar
{
    // Ordinal comparison throughout (rule D-8): a unit token's meaning must not
    // depend on the machine's culture.
    private static readonly Dictionary<string, UnitConversion> Tokens =
        new(StringComparer.Ordinal)
        {
            // -------- pressure (canonical Pa)
            ["pa"] = new(Dimension.Pressure, 1.0, 0.0),
            ["kpa"] = new(Dimension.Pressure, 1e3, 0.0),
            ["mpa"] = new(Dimension.Pressure, 1e6, 0.0),
            ["bar"] = new(Dimension.Pressure, 1e5, 0.0),
            ["psi"] = new(Dimension.Pressure, 6894.757293168, 0.0),
            ["psia"] = new(Dimension.Pressure, 6894.757293168, 0.0),
            ["atm"] = new(Dimension.Pressure, 101_325.0, 0.0),

            // -------- temperature (canonical K) — the only AFFINE entries
            ["k"] = new(Dimension.Temperature, 1.0, 0.0),
            ["degc"] = new(Dimension.Temperature, 1.0, 273.15),
            ["degf"] = new(Dimension.Temperature, 5.0 / 9.0, 255.372222222222222),

            // -------- length (canonical m)
            ["m"] = new(Dimension.Length, 1.0, 0.0),
            ["km"] = new(Dimension.Length, 1e3, 0.0),
            ["cm"] = new(Dimension.Length, 1e-2, 0.0),
            ["mm"] = new(Dimension.Length, 1e-3, 0.0),
            ["ft"] = new(Dimension.Length, 0.3048, 0.0),
            ["in"] = new(Dimension.Length, 0.0254, 0.0),

            // -------- area (canonical m²)
            ["m2"] = new(Dimension.Area, 1.0, 0.0),
            ["km2"] = new(Dimension.Area, 1e6, 0.0),
            ["ha"] = new(Dimension.Area, 1e4, 0.0),
            ["acre"] = new(Dimension.Area, 4046.8564224, 0.0),

            // -------- mass (canonical kg)
            ["kg"] = new(Dimension.Mass, 1.0, 0.0),
            ["t"] = new(Dimension.Mass, 1e3, 0.0),
            ["lb"] = new(Dimension.Mass, 0.45359237, 0.0),

            // -------- duration (canonical sim DAYS, SDD-001 §3)
            ["d"] = new(Dimension.Duration, 1.0, 0.0),
            ["h"] = new(Dimension.Duration, 1.0 / 24.0, 0.0),
            ["min"] = new(Dimension.Duration, 1.0 / 1440.0, 0.0),
            ["s"] = new(Dimension.Duration, 1.0 / 86_400.0, 0.0),

            // -------- rates, power, energy
            ["kg/s"] = new(Dimension.MassRate, 1.0, 0.0),
            ["t/d"] = new(Dimension.MassRate, 1e3 / 86_400.0, 0.0),
            ["w"] = new(Dimension.Power, 1.0, 0.0),
            ["kw"] = new(Dimension.Power, 1e3, 0.0),
            ["mw"] = new(Dimension.Power, 1e6, 0.0),
            ["j"] = new(Dimension.Energy, 1.0, 0.0),
            ["kj"] = new(Dimension.Energy, 1e3, 0.0),
            ["mj"] = new(Dimension.Energy, 1e6, 0.0),

            // -------- rock and fluid properties
            ["md"] = new(Dimension.Permeability, 9.869233e-16, 0.0),
            ["darcy"] = new(Dimension.Permeability, 9.869233e-13, 0.0),
            ["pa.s"] = new(Dimension.Viscosity, 1.0, 0.0),
            ["cp"] = new(Dimension.Viscosity, 1e-3, 0.0),
            ["kg/m3"] = new(Dimension.Density, 1.0, 0.0),
            ["g/cm3"] = new(Dimension.Density, 1e3, 0.0),
            ["j/kg"] = new(Dimension.HeatingValue, 1.0, 0.0),
            ["mj/kg"] = new(Dimension.HeatingValue, 1e6, 0.0),

            // -------- volumes: the condition IS the token (SDD-001 §1.1)
            // The double-count protection reaches all the way into content —
            // "1000 rb" and "1000 stb" parse to different TYPES, so an author
            // cannot write a reservoir volume where a surface one is meant.
            ["rm3"] = new(Dimension.ReservoirVolume, 1.0, 0.0),
            ["rb"] = new(Dimension.ReservoirVolume, 0.158987294928, 0.0),
            ["sm3"] = new(Dimension.SurfaceVolume, 1.0, 0.0),
            ["stb"] = new(Dimension.SurfaceVolume, 0.158987294928, 0.0),
            ["scf"] = new(Dimension.StandardGasVolume, 0.028316846592, 0.0),
            ["sm3g"] = new(Dimension.StandardGasVolume, 1.0, 0.0),

            // -------- volumetric rates
            ["rm3/d"] = new(Dimension.ReservoirRate, 1.0 / 86_400.0, 0.0),
            ["stb/d"] = new(Dimension.SurfaceRate, 0.158987294928 / 86_400.0, 0.0),
            ["sm3/d"] = new(Dimension.SurfaceRate, 1.0 / 86_400.0, 0.0),
            ["scf/d"] = new(Dimension.StandardGasRate, 0.028316846592 / 86_400.0, 0.0),

            // -------- dimensionless
            ["frac"] = new(Dimension.Dimensionless, 1.0, 0.0),
            ["pct"] = new(Dimension.Dimensionless, 0.01, 0.0),
            ["ppm"] = new(Dimension.Dimensionless, 1e-6, 0.0),
        };

    /// <summary>
    /// Parses "3200 psi" to its canonical SI magnitude, checked against the
    /// dimension the target property declares.
    ///
    /// The EXPECTED dimension is an argument rather than an inference, and that
    /// is what resolves the genuine ambiguity in the vocabulary: `m2` is both an
    /// area and (numerically) a permeability. Binding decides which, exactly as
    /// SDD-004 §4 says — "the definition record property is a quantity type".
    /// </summary>
    public static double ParseToSi(string text, Dimension expected)
    {
        ArgumentNullException.ThrowIfNull(text);

        string trimmed = text.Trim();
        int space = trimmed.IndexOf(' ');
        if (space <= 0)
            throw new ContentUnitFault(text,
                "expected '<number> <unit>', e.g. \"3200 psi\" — the unit is never optional");

        string numberPart = trimmed[..space];
        string unitPart = trimmed[(space + 1)..].Trim().ToLowerInvariant();

        // Decimal point only. A comma is a pointed error, never a locale guess:
        // "3,5" means three-and-a-half in one country and thirty-five in another,
        // and a loader that guesses will be wrong in production, once, silently.
        if (numberPart.Contains(',', StringComparison.Ordinal))
            throw new ContentUnitFault(text,
                "',' is not a decimal point; content is InvariantCulture and uses '.'");

        if (!double.TryParse(numberPart, NumberStyles.Float, CultureInfo.InvariantCulture,
                             out double value))
            throw new ContentUnitFault(text, $"'{numberPart}' is not a number");

        if (!Tokens.TryGetValue(unitPart, out UnitConversion conversion))
            throw new ContentUnitFault(text,
                $"unknown unit '{unitPart}'{NearestHint(unitPart)}");

        if (conversion.Dimension != expected)
            throw new ContentUnitFault(text,
                $"'{unitPart}' is {conversion.Dimension}, but a {expected} was expected");

        return value * conversion.Factor + conversion.Offset;
    }

    public static bool IsKnownUnit(string token) =>
        Tokens.ContainsKey(token.Trim().ToLowerInvariant());

    /// <summary>
    /// Nearest known token by edit distance — small, and worth it for authors
    /// (SDD-004 §3 asks for the same courtesy on unknown JSON keys). Suggests
    /// nothing when nothing is close, rather than proposing a wild guess.
    /// </summary>
    private static string NearestHint(string unknown)
    {
        string? best = null;
        int bestDistance = int.MaxValue;

        // Ordered so the suggestion cannot depend on hash iteration order (D-5).
        var candidates = new List<string>(Tokens.Keys);
        candidates.Sort(StringComparer.Ordinal);

        for (int i = 0; i < candidates.Count; i++)
        {
            int distance = EditDistance(unknown, candidates[i]);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidates[i];
            }
        }

        return best is not null && bestDistance <= 2 ? $" — did you mean '{best}'?" : string.Empty;
    }

    private static int EditDistance(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++) previous[j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int substitution = previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1);
                int deletion = previous[j] + 1;
                int insertion = current[j - 1] + 1;
                current[j] = Math.Min(substitution, Math.Min(deletion, insertion));
            }
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }
}

/// <summary>
/// A unit string the loader could not accept. Content-class: the engine does not
/// start (SDD-004 §5), and the message carries the offending text so the load
/// report can name file, path and value together.
/// </summary>
public sealed class ContentUnitFault : FaultException
{
    public ContentUnitFault(string text, string reason)
        : base(new Fault(FaultClass.Content, "SDD-004 §4", null,
                         $"Cannot parse quantity \"{text}\": {reason}."))
    {
        Text = text;
        Reason = reason;
    }

    public string Text { get; }
    public string Reason { get; }
}
