// R13.3 — take-or-pay (SDD-009 §7's R13.3 amendment, finding 250).
//
// A CLOCK AND A PROMISE, the same shape as a licence's work commitment: the
// clock is the window, the promise is the committed offtake, and a window
// that closes short costs a penalty mechanically computed from the shortfall
// — never a judgement call, and never skipped because the field had a bad
// month.
//
// THE WINDOW BOUNDARY IS STATE, not `elapsed % WindowMonths`. It advances
// every time it is crossed, so it is a stored fact rather than something
// re-derived from fixed terms — the same reason `Licence.IsLive` is captured
// rather than recomputed.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Company;

/// <summary>SDD-009 §7's take-or-pay terms.</summary>
public sealed record TakeOrPayTerms(
    SurfaceVolume CommittedVolume,    // per window, stock-tank m³
    int WindowMonths,
    Money PenaltyRate);               // per m³ of shortfall

/// <summary>What a closed window found.</summary>
public sealed record TakeOrPayAssessment(
    SurfaceVolume Delivered,
    SurfaceVolume Shortfall,
    Money Penalty);

/// <summary>
/// SDD-009 §7. A live take-or-pay contract with its own clock, independent of
/// the licence's.
/// </summary>
public sealed class TakeOrPayContract : IStateOwner
{
    private readonly TakeOrPayTerms _terms;
    private double _deliveredThisWindowM3;
    private Tick _windowEndsAt;

    public TakeOrPayContract(TakeOrPayTerms terms, Tick start)
    {
        ArgumentNullException.ThrowIfNull(terms);
        Validate(terms);

        _terms = terms;
        _windowEndsAt = new Tick(start.Value + terms.WindowMonths);
    }

    /// <summary>Every tick's delivered volume, accumulated toward the open
    /// window — mechanical, the same as <see cref="Licence.RecordDelivery"/>.</summary>
    public void RecordDelivery(SurfaceVolume delivered)
    {
        if (delivered.CubicMetres < 0.0)
            throw new InvariantFault("SDD-009 §7", null,
                "negative delivery; a custody transfer cannot be un-done against a window");

        _deliveredThisWindowM3 += delivered.CubicMetres;
    }

    /// <summary>
    /// Null off-window: assessing every tick and returning nothing to post
    /// on the ones that are not the window's close is what lets a stage call
    /// this unconditionally, the same way <see cref="Licence.AssessAt"/> is
    /// called every tick while the licence is live.
    /// </summary>
    public TakeOrPayAssessment? AssessAt(Tick now)
    {
        if (now.Value < _windowEndsAt.Value) return null;

        double delivered = _deliveredThisWindowM3;
        double shortfall = Math.Max(0.0, _terms.CommittedVolume.CubicMetres - delivered);
        Money penalty = Money.RoundHalfEven(_terms.PenaltyRate.Cents * shortfall);

        _deliveredThisWindowM3 = 0.0;
        _windowEndsAt = new Tick(_windowEndsAt.Value + _terms.WindowMonths);

        return new TakeOrPayAssessment(
            new SurfaceVolume(delivered), new SurfaceVolume(shortfall), penalty);
    }

    // ------------------------------------------------------- SDD-013 §4

    public StateKey Key { get; } = new("company.take-or-pay");

    public int SchemaVersion => 1;

    /// <summary>Nothing has to be back before this is (SDD-013 §2b).</summary>
    public IReadOnlyList<StateKey> RestoreAfter => [];

    public void Capture(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteDouble("delivered-this-window-m3", _deliveredThisWindowM3);
        writer.WriteInt64("window-ends-at", _windowEndsAt.Value);
    }

    public void Restore(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        _deliveredThisWindowM3 = reader.ReadDouble("delivered-this-window-m3");
        _windowEndsAt = new Tick((int)reader.ReadInt64("window-ends-at"));
    }

    private static void Validate(TakeOrPayTerms terms)
    {
        if (terms.CommittedVolume.CubicMetres <= 0.0)
            throw new ModelFault("SDD-009 §7", null,
                "a take-or-pay commitment of zero or negative volume is not a commitment");

        if (terms.WindowMonths <= 0)
            throw new ModelFault("SDD-009 §7", null,
                $"take-or-pay window {terms.WindowMonths} months is not positive");

        if (terms.PenaltyRate.Cents < 0)
            throw new ModelFault("SDD-009 §7", null, "a penalty rate cannot be negative");
    }
}
