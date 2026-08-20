# SegmentPlanner

Source: `src\OGSim.Kernel\SegmentPlanner.cs` · Lines: 209

## File intent

> R1.14 — sub-tick segmentation (SDD-001 §9, design 15 §6, R1 §2.7).
> 
> Availability is SEGMENTED, never averaged, and that is a correctness decision
> rather than a fidelity one: the network solve is non-linear, so a compressor
> available for 60% of a month is not the same as a compressor at 60% capacity.
> Segmenting is exact; averaging is simply wrong.
> 
> Boundaries live on the /30ths grid as whole days, so INV9 — "durations sum to

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L23` `public sealed record AvailabilityChange(`
- `L29` `public sealed class SegmentPlanner`

## Accessible members

- `L32` `public const int MaxSegments = 4;`
- `L34` `private static readonly int DaysPerTick = (int)Duration.DaysPerTick;`
- `L36` `private readonly IAuditTrail _audit;`
- `L38` `public SegmentPlanner(IAuditTrail audit)`
- `L49` `public SegmentPlan Plan(`
- `L104` `private static List<int> SelectWithinBudget(`
- `L124` `private static int NearestBoundary(int day, List<int> keptDays)`
- `L140` `private SegmentPlan Build(`
- `L183` `private static void AssertInv9(List<Segment> segments)`
- `L204` `private static string Format(int value) =>`
- `L207` `private static string Format(double value) =>`

