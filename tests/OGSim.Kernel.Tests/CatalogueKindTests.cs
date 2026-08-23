// Finding 284 — the relation kind's cycle report, pinned at the unit level.
//
// `CatalogueContentTests` (composition suite) proves the SHIPPED files are
// acyclic; nothing proved what the validator says about files that are not.

using OGSim.Kernel;

namespace OGSim.Kernel.Tests;

public sealed class CatalogueKindTests
{
    private static RelationDefinition Requires(params (string From, string To)[] edges) =>
        new(new ContentId("activity-requires-activity"),
            [.. edges.Select(e => new RelationEdge(new ContentId(e.From), new ContentId(e.To)))]);

    /// <summary>
    /// TWO DISJOINT CYCLES ARE TWO PROBLEMS (finding 284). The loader's own law
    /// is "EVERY problem, never just the first" — a content author told one of
    /// two cycles fixes one of two, reloads, and is told the other; the first
    /// version of this validator stopped at the first cycle it met.
    /// </summary>
    [Fact]
    public void Two_disjoint_cycles_are_both_named()
    {
        IReadOnlyList<string> problems = new RelationContentKind().ConsistencyProblems(
            Requires(("a", "b"), ("b", "a"), ("c", "d"), ("d", "c")));

        Assert.Equal(2, problems.Count(p => p.StartsWith("cycle:", StringComparison.Ordinal)));
        Assert.Contains(problems, p => p.Contains("a -> b -> a", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("c -> d -> c", StringComparison.Ordinal));
    }

    /// <summary>One tangle is ONE report, not one per node on it — the stated
    /// boundary of the walk: overlapping cycles through shared nodes surface one
    /// at a time, each next one the load after its predecessor is fixed.</summary>
    [Fact]
    public void One_cycle_is_reported_once()
    {
        IReadOnlyList<string> problems = new RelationContentKind().ConsistencyProblems(
            Requires(("a", "b"), ("b", "c"), ("c", "a")));

        Assert.Single(problems, p => p.StartsWith("cycle:", StringComparison.Ordinal));
    }

    /// <summary>An acyclic chain reports nothing — the healthy case, so the two
    /// above cannot pass by the validator complaining about everything.</summary>
    [Fact]
    public void An_acyclic_chain_reports_no_cycle()
    {
        IReadOnlyList<string> problems = new RelationContentKind().ConsistencyProblems(
            Requires(("a", "b"), ("b", "c"), ("a", "c")));

        Assert.DoesNotContain(problems, p => p.StartsWith("cycle:", StringComparison.Ordinal));
    }
}
