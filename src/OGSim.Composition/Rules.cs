// Game rules are a MODE, not an edit (plans 23).
//
// OGSim is built for realistic scenarios; Oilfield Days is a Settlers-shaped
// game. Those want different rules and neither is wrong — so a contested rule
// becomes a contract with two implementations, and a run composes the set it is
// played under.
//
// NOT A BRANCH. Design 03 §3.2 is explicit that a mode is a different set of
// registered implementations rather than `if (mode == …)` scattered through the
// activities. An `if` on the rule set anywhere below this file is the defect
// this design exists to prevent.
//
// NOT FIDELITY EITHER. `RealityProfile` decides which PHYSICS fills a slot;
// this decides what a company is ALLOWED to do. A run picks one of each and they
// are independent.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition;

/// <summary>
/// Whether the company has anything to work on at all.
/// </summary>
/// <remarks>
/// The rule GC-4 was about. An operator with no reservoir has nothing to survey,
/// test or drill into, and refusing that is correct — for an operator. A company
/// on frontier acreage begins with exactly nothing, and finding the first
/// compartment IS the game.
/// </remarks>
public interface IWorkSubjectRule
{
    /// <summary>Why there is nothing here to work on, or an empty list.</summary>
    IReadOnlyList<RejectionReason> Refusals(FieldControl field);
}

/// <summary>
/// An operator works a field it already has.
/// </summary>
/// <remarks>
/// Restores the constraint GC-4 deleted. Deleting it removed a real limit from
/// realistic scenarios in order to unblock a game, which is the trade this whole
/// design exists to stop making.
/// </remarks>
public sealed class OperatingSubjectRule : IWorkSubjectRule
{
    public IReadOnlyList<RejectionReason> Refusals(FieldControl field)
    {
        ArgumentNullException.ThrowIfNull(field);

        if (field.CompartmentCount > 0)
            return [];

        return
        [
            new RejectionReason("$loc:reject.no-target", "there is nothing here to work on"),
        ];
    }
}

/// <summary>
/// A company on frontier acreage may work ground it has not proved yet.
/// </summary>
/// <remarks>
/// <para><b>Permissive, not absent.</b> Everything else still refuses: the cash,
/// the rig, the one-at-a-time rule, the weather window, and each activity's own
/// subject. What goes is the single assumption that a company already holds a
/// reservoir — which is the assumption a game about finding one cannot make.</para>
///
/// <para>A survey over barren ground and a dry hole are both real outcomes here,
/// and both are paid for. The engine answering "there is nothing here" for free
/// would be answering the question the company is spending money to ask.</para>
/// </remarks>
public sealed class FrontierSubjectRule : IWorkSubjectRule
{
    public IReadOnlyList<RejectionReason> Refusals(FieldControl field)
    {
        ArgumentNullException.ThrowIfNull(field);

        return [];
    }
}

/// <summary>
/// Where a well may be drilled, and why not.
/// </summary>
/// <remarks>
/// A well needs somewhere to tie in (SDD-006 §1b) — for an operator. A frontier
/// company drills to find out whether there is anything worth building a
/// facility FOR, which is the opposite order, and is what an exploration well
/// has always been for.
/// </remarks>
public interface IDrillingRule
{
    /// <summary>Why this well cannot be drilled, or an empty list.</summary>
    IReadOnlyList<RejectionReason> Refusals(FieldControl field);
}

/// <summary>
/// An operator drills into a plant that can take the production.
/// </summary>
/// <remarks>
/// Money spent on a hole that cannot flow is money burned, and the refusal comes
/// before the rig is charged rather than four months later.
/// </remarks>
public sealed class OperatingDrillingRule : IDrillingRule
{
    public IReadOnlyList<RejectionReason> Refusals(FieldControl field)
    {
        ArgumentNullException.ThrowIfNull(field);

        // TWO WAYS TO HAVE NOWHERE TO TIE IN, and they have different remedies.
        // A full header is debottlenecked by installing a bigger one; NO header
        // is answered by commissioning a facility. Telling a company that owns
        // no manifold that every slot is taken sends a player looking for slots
        // that were never there.
        if (field.Slots == 0)
        {
            return
            [
                new RejectionReason(
                    "$loc:reject.no-plant",
                    "there is no header to tie a well into; an operating field is brought " +
                    "on by commissioning a facility before it is drilled"),
            ];
        }

        if (field.HasFreeSlot)
            return [];

        return
        [
            new RejectionReason(
                "$loc:reject.no-manifold-slot",
                "every slot on the manifold is taken; a well with nowhere to tie in " +
                "cannot flow, and a bigger header has to be installed first"),
        ];
    }
}

/// <summary>
/// A frontier company drills first and builds around what it finds.
/// </summary>
/// <remarks>
/// <para><b>A well with nowhere to go is SUSPENDED, not refused.</b> That is
/// what an exploration well is: drilled, logged, and left shut in for as long as
/// it takes to decide whether a facility is worth building. Refusing it would
/// force a company to commit to a plant before it knew it had anything, which is
/// the wrong order and the expensive one.</para>
///
/// <para>A FULL header still refuses, and for the original reason — there the
/// company HAS a plant and the remedy is a bigger one.</para>
/// </remarks>
public sealed class FrontierDrillingRule : IDrillingRule
{
    public IReadOnlyList<RejectionReason> Refusals(FieldControl field)
    {
        ArgumentNullException.ThrowIfNull(field);

        // No header at all is fine: the well waits for one.
        if (field.Slots == 0 || field.HasFreeSlot)
            return [];

        return
        [
            new RejectionReason(
                "$loc:reject.no-manifold-slot",
                "every slot on the manifold is taken; the next well has nowhere to tie " +
                "in until a bigger header is installed"),
        ];
    }
}

/// <summary>
/// The rules a run is played under.
/// </summary>
/// <remarks>
/// Mirrors <see cref="RealityProfile"/> deliberately, including the refusal of an
/// unknown id: a run naming a set that is not shipped is refused when the engine
/// composes rather than silently played under whatever the modules happened to
/// choose.
/// </remarks>
public sealed record RuleSet(
    ContentId Id,
    IWorkSubjectRule WorkSubject,
    IDrillingRule Drilling)
{
    public bool Equals(RuleSet? other) => other is not null && Id == other.Id;

    public override int GetHashCode() => Id.GetHashCode();
}

/// <summary>The rule sets this build ships.</summary>
public static class RuleSets
{
    /// <summary>An operator running a field it holds. The shipped default, and
    /// what every existing test is played at.</summary>
    public static RuleSet Realistic { get; } =
        new(new ContentId("realistic"), new OperatingSubjectRule(), new OperatingDrillingRule());

    /// <summary>A company on ground nobody has worked — the Settlers position
    /// (plans 22).</summary>
    public static RuleSet Frontier { get; } =
        new(new ContentId("frontier"), new FrontierSubjectRule(), new FrontierDrillingRule());

    /// <summary>Walked in declared order (D-5).</summary>
    public static IReadOnlyList<RuleSet> All { get; } = [Realistic, Frontier];

    public static RuleSet Named(ContentId id)
    {
        for (int i = 0; i < All.Count; i++)
            if (All[i].Id == id) return All[i];

        throw new ContentFault("plans 23 §4", null,
            $"'{id.Value}' is not a rule set this build ships; it declares " +
            $"'{Realistic.Id.Value}' and '{Frontier.Id.Value}'");
    }
}
