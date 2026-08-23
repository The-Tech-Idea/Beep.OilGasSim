// S1 — shoot reconnaissance seismic over a block (SDD-010 §4b's S1 amendment).
//
// THE MOVE THE EXPLORATION GAME DID NOT HAVE. Every structure used to be handed
// to the company as the world was generated, so the opening question was never
// "where do I look" but only "which of these known odds do I drill". This is the
// activity that buys the map instead of being given it.
//
// It pairs with seismic-3d rather than competing: 2-D reconnaissance FINDS
// closures over a block, 3-D detail FIRMS UP one that has been found. That is
// why the per-prospect survey stopped being circular the day this landed — it
// names a prospect, and now a prospect has to be found before it can be named.

using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Company;

namespace OGSim.Composition;

/// <summary>
/// Shoot reconnaissance seismic over one block of the licence.
///
/// <para>Aimed at a BLOCK, which is an entity, so the area an activity is
/// pointed at travels through the one <c>EntityRef</c> that SDD-007 §5 gives it
/// — rather than a centre and a radius smuggled through a depth, which is the
/// call-site invention F-4 forbids.</para>
/// </summary>
public sealed record SurveyBlockCommand(
    EntityId<IBlock> Block) : Command(Subject: null);

internal sealed class SurveyBlockActivity(
    ActivityTerms terms,
    WorldState world,
    OGSim.Information.ProspectRisks risks) : Activity<SurveyBlockCommand>(terms)
{
    /// <summary>
    /// Knowledge is not PP&amp;E (SDD-009 §1). A survey is bought and consumed in
    /// the same month; capitalising it would let a company inflate its balance
    /// sheet by shooting its own acreage.
    /// </summary>
    public override bool LeavesAnAsset => false;

    /// <summary>Looking for oil is exactly what the line means (finding 225).</summary>
    public override MovementCategory Spend => MovementCategory.Exploration;

    /// <summary>
    /// One at a time over a block, and the reason is not σ this time: a block
    /// yields everything it has to the first pass, so a second alongside it
    /// would buy a map the company already owns.
    /// </summary>
    public override bool OnePerTarget => true;

    public override (EntityRef Target, Length Depth) Aim(SurveyBlockCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return (new EntityRef(EntityKind.Block, command.Block.Value), NoDepth);
    }

    public override string QuantityUnit => "square-kilometre";

    /// <summary>The generated block's own ground (finding 289): a bigger basin
    /// carves bigger blocks, and shooting one costs what that acreage costs —
    /// zero on a world with no surface, whose refusals answer first.</summary>
    public override double Quantity(SurveyBlockCommand command) =>
        (world.BlockWidth * world.BlockHeight).ToSquareKilometres();

    /// <summary>
    /// A block has to be on the licence, and shooting it twice buys nothing.
    /// </summary>
    /// <remarks>
    /// Refused here rather than in the shared checks because this is the
    /// activity's own subject, which is where SDD-007 §5 puts an unresolvable
    /// target. Note what is NOT refused: an empty basin. A company is entitled
    /// to spend its money finding out that there is nothing under a block, and a
    /// validator that answered that for free would be answering the question the
    /// survey is being bought to ask.
    /// </remarks>
    public override IReadOnlyList<RejectionReason> OwnRefusals(SurveyBlockCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var reasons = new List<RejectionReason>();

        if (command.Block.Value == 0UL || command.Block.Value > (ulong)world.BlockCount)
        {
            reasons.Add(new RejectionReason(
                "$loc:reject.no-such-block",
                $"block {command.Block.Value} is not on this licence, which is cut into " +
                $"{world.BlockCount} blocks"));

            return reasons;
        }

        if (world.WasShot(command.Block))
            reasons.Add(new RejectionReason(
                "$loc:reject.block-already-shot",
                $"block {command.Block.Value} has already been shot, and a second pass would " +
                "buy the map this company already owns"));

        return reasons;
    }

    public override void Complete(CompletedActivity done, Tick tick)
    {
        ArgumentNullException.ThrowIfNull(done);

        if (!done.Succeeded) return;

        // EVERY CLOSURE INSIDE THE BLOCK BECOMES KNOWN, charged or dry — which
        // is what stops a survey being a way to ask whether there is oil. A
        // block that returns nothing is marked shot just the same: ground known
        // to be barren is ground the company can stop paying to think about.
        world.Shoot(new EntityId<IBlock>(done.Target.Value), risks);
    }
}
