// SDD-011 — companies, licences and rivals.
//
// ILicence lives HERE as of R16, having sat in InformationContracts.cs since the
// contract layer. SDD-011 §1 records why it was there — IWell.Licence needs it
// from R6, and Information was the file that existed — and records that moving
// it is an R16 task: the type was right and only its address was wrong, and a
// public type is worth moving where the tests that cover it are being written.

using OGSim.Kernel;

namespace OGSim.Contracts;

/// <summary>
/// SDD-011 §1. Deliberately thin: everything negotiable is in
/// <see cref="LicenceTerms"/>, and everything spatial is in the read model's
/// exploration view.
/// </summary>
public interface ILicence
{
    EntityId<ILicence> Id { get; }
    ContentId FiscalRegime { get; }
    Tick Expiry { get; }
}

/// <summary>One item of the work programme a licence was won with.</summary>
public sealed record CommitmentItem(
    ContentId Kind,        // exploration-well, seismic-2d, …
    double Quantity,       // wells, or km²
    Tick Due);

/// <summary>A fraction of the acreage handed back by a date.</summary>
public sealed record RelinquishmentStep(double Fraction, Tick Due);

/// <summary>
/// The negotiated terms, from jurisdiction content.
///
/// <para><c>TermMonths</c> is a whole number of months on the calendar, not a
/// <c>Duration</c> — SDD-011 §1 records that it was typed as one, which carries
/// a double of days, the same defect SDD-007 §1 had with an operation's
/// duration.</para>
/// </summary>
public sealed record LicenceTerms(
    int TermMonths,
    IReadOnlyList<CommitmentItem> WorkCommitment,
    Money Bond,
    IReadOnlyList<RelinquishmentStep> Relinquishment,
    ContentId FiscalRegime,
    ContentId HseRegime);
