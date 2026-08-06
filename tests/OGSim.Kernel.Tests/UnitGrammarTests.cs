// R3.1 — the content unit grammar (SDD-004 §4, decision CD2).
// R3-V5: a quantity whose unit is the wrong dimension fails at load, naming both.

using OGSim.Kernel;

namespace OGSim.Kernel.Tests;

public class UnitGrammarTests
{
    [Fact] // CD2's own example, and the conversions either side of it
    public void R3V5_a_quantity_parses_to_canonical_si()
    {
        Assert.Equal(3200.0 * 6894.757293168,
                     UnitGrammar.ParseToSi("3200 psi", Dimension.Pressure), 6);
        Assert.Equal(1.0e5, UnitGrammar.ParseToSi("1 bar", Dimension.Pressure), 9);
        Assert.Equal(1.0e6, UnitGrammar.ParseToSi("1 MPa", Dimension.Pressure), 9);

        Assert.Equal(0.3048, UnitGrammar.ParseToSi("1 ft", Dimension.Length), 12);
        Assert.Equal(1.0e-3, UnitGrammar.ParseToSi("1 cP", Dimension.Viscosity), 12);

        // Permeability's SI magnitude is ~1e-16, so an absolute decimal
        // tolerance would pass on any value at all — compare the ratio.
        Assert.Equal(1.0,
            UnitGrammar.ParseToSi("1 mD", Dimension.Permeability) / 9.869233e-16, 12);
        Assert.Equal(1000.0,
            UnitGrammar.ParseToSi("1 darcy", Dimension.Permeability)
            / UnitGrammar.ParseToSi("1 mD", Dimension.Permeability), 9);
    }

    [Fact] // The affine scales are the ones an offset-free table gets wrong
    public void R3V5_temperature_scales_are_affine()
    {
        Assert.Equal(273.15, UnitGrammar.ParseToSi("0 degC", Dimension.Temperature), 9);
        Assert.Equal(373.15, UnitGrammar.ParseToSi("100 degC", Dimension.Temperature), 9);

        // 60 °F is standard conditions — the value SDD-004 pins Bg against.
        Assert.Equal(288.706, UnitGrammar.ParseToSi("60 degF", Dimension.Temperature), 3);
        Assert.Equal(255.372, UnitGrammar.ParseToSi("0 degF", Dimension.Temperature), 3);
        Assert.Equal(0.0, UnitGrammar.ParseToSi("0 K", Dimension.Temperature), 9);
    }

    [Fact] // R3-V5: the wrong dimension is a load error naming BOTH
    public void R3V5_a_dimension_mismatch_names_both_dimensions()
    {
        var fault = Assert.Throws<ContentUnitFault>(
            () => UnitGrammar.ParseToSi("3200 psi", Dimension.Temperature));

        Assert.Equal(FaultClass.Content, fault.Fault.Class);
        Assert.Contains("Pressure", fault.Fault.Detail);
        Assert.Contains("Temperature", fault.Fault.Detail);
    }

    [Fact] // The m2 ambiguity is real, and binding is what resolves it
    public void R3V5_the_expected_dimension_disambiguates_a_shared_token()
    {
        // Numerically identical, semantically unrelated: an area and a
        // permeability are both "square metres".
        Assert.Equal(1.0, UnitGrammar.ParseToSi("1 m2", Dimension.Area), 12);

        // Asking for a permeability in m2 is refused — permeability is authored
        // in millidarcy, and a bare m2 there is almost certainly a mistake.
        Assert.Throws<ContentUnitFault>(
            () => UnitGrammar.ParseToSi("1 m2", Dimension.Permeability));
    }

    [Fact] // SDD-001 §1.1's volume families reach all the way into content
    public void R3V5_volume_condition_is_carried_by_the_token()
    {
        double reservoir = UnitGrammar.ParseToSi("1000 rb", Dimension.ReservoirVolume);
        double surface = UnitGrammar.ParseToSi("1000 stb", Dimension.SurfaceVolume);

        // Same number of barrels, same cubic metres — and NOT interchangeable,
        // because the dimension check refuses the swap.
        Assert.Equal(reservoir, surface, 12);
        Assert.Throws<ContentUnitFault>(
            () => UnitGrammar.ParseToSi("1000 rb", Dimension.SurfaceVolume));
        Assert.Throws<ContentUnitFault>(
            () => UnitGrammar.ParseToSi("1000 stb", Dimension.ReservoirVolume));

        // Gas has its own family (finding 77).
        Assert.Throws<ContentUnitFault>(
            () => UnitGrammar.ParseToSi("1000 scf", Dimension.SurfaceVolume));
    }

    [Fact] // A comma is an error, never a locale guess
    public void R3V5_a_decimal_comma_is_refused_with_a_pointed_message()
    {
        var fault = Assert.Throws<ContentUnitFault>(
            () => UnitGrammar.ParseToSi("3,5 bar", Dimension.Pressure));
        Assert.Contains("decimal point", fault.Reason);

        // The point of the rule: "3,5" is ambiguous across locales, and a
        // loader that guesses is wrong in production once, silently.
        Assert.Equal(3.5e5, UnitGrammar.ParseToSi("3.5 bar", Dimension.Pressure), 9);
    }

    [Fact] // The vocabulary is CLOSED, and says what it meant
    public void R3V5_an_unknown_unit_suggests_the_nearest_known_one()
    {
        var typo = Assert.Throws<ContentUnitFault>(
            () => UnitGrammar.ParseToSi("100 psii", Dimension.Pressure));
        Assert.Contains("did you mean 'psi'", typo.Reason);

        // Nothing close: no wild guess offered.
        var nonsense = Assert.Throws<ContentUnitFault>(
            () => UnitGrammar.ParseToSi("100 furlongs", Dimension.Length));
        Assert.Contains("unknown unit", nonsense.Reason);
        Assert.DoesNotContain("did you mean", nonsense.Reason);
    }

    [Fact] // The unit is never optional — a bare number has no meaning
    public void R3V5_a_bare_number_is_refused()
    {
        Assert.Throws<ContentUnitFault>(() => UnitGrammar.ParseToSi("3200", Dimension.Pressure));
        Assert.Throws<ContentUnitFault>(() => UnitGrammar.ParseToSi("", Dimension.Pressure));
        Assert.Throws<ContentUnitFault>(() => UnitGrammar.ParseToSi("psi", Dimension.Pressure));
    }

    [Fact] // Scientific notation and negatives, both legal per the EBNF
    public void R3V5_the_number_grammar_accepts_exponents_and_negatives()
    {
        Assert.Equal(1.5e-3, UnitGrammar.ParseToSi("1.5e-3 Pa.s", Dimension.Viscosity), 15);
        Assert.Equal(-40.0 + 273.15,
                     UnitGrammar.ParseToSi("-40 degC", Dimension.Temperature), 9);
    }

    [Fact] // Tokens are case-insensitive on the way in, ordinal on the way through
    public void R3V5_unit_tokens_are_case_insensitive()
    {
        double expected = UnitGrammar.ParseToSi("1 psi", Dimension.Pressure);
        Assert.Equal(expected, UnitGrammar.ParseToSi("1 PSI", Dimension.Pressure), 9);
        Assert.Equal(expected, UnitGrammar.ParseToSi("1 Psi", Dimension.Pressure), 9);

        Assert.True(UnitGrammar.IsKnownUnit("STB"));
        Assert.False(UnitGrammar.IsKnownUnit("parsec"));
    }

    [Fact] // Duration is in SIM DAYS, not seconds (SDD-001 §3's 30/360)
    public void R3V5_duration_is_canonically_days()
    {
        Assert.Equal(1.0, UnitGrammar.ParseToSi("1 d", Dimension.Duration), 12);
        Assert.Equal(1.0, UnitGrammar.ParseToSi("24 h", Dimension.Duration), 12);
        Assert.Equal(30.0, UnitGrammar.ParseToSi("30 d", Dimension.Duration), 12);
    }
}
