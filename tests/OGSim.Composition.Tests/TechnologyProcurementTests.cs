// R17.3/R17.4 — buying technology, and what holding it costs (design 07 §3,
// finding 293). R17-V7's claim: unlocked technologies accrue their ongoing
// burden — and before any of this, forty-eight of sixty-five nodes had no
// reachable route at all.

using OGSim.Capabilities;
using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition.Tests;

public sealed class TechnologyProcurementTests
{
    /// <summary>
    /// R17-V7: A LICENSED TECHNOLOGY BILLS ITS FEE EVERY MONTH — granted the
    /// month it is signed, expensive forever, never owned (design 07 §3). The
    /// fee lands on the real ledger under its own named spend.
    /// </summary>
    [Fact]
    public void R17V7_a_licensed_technology_bills_its_fee_every_month()
    {
        Built built = Assert.IsType<Built>(EngineBuilder.Build(Fixture.Settings()));
        Engine engine = built.Engine;

        TechnologyNode node = Buyable(AcquisitionRoute.Licence);

        Assert.IsType<Accepted>(engine.Commands.Submit(
            new LicenseTechnologyCommand(node.Id.Value)));

        var capabilities = engine.Provided.Resolve<CapabilityState>();

        Assert.Contains(node.Id, capabilities.Technology.Acquired);
        Assert.Equal(AcquisitionRoute.Licence, capabilities.Technology.RouteOf(node.Id));

        engine.Pipeline.AdvanceTick();
        engine.Pipeline.AdvanceTick();

        Assert.Contains(
            engine.Audit.Query(new AuditQuery(null, AuditCategory.Financial, null, null)),
            entry => entry.Data.TryGetValue("spend", out AuditValue spend)
                && spend.Value == "technology-licence-fees");
    }

    /// <summary>
    /// R17-V7: A PROGRAMME SPENDS ITS BUDGET MONTHLY AND OWNS THE NODE AT THE
    /// END — cheapest per unit, longest to arrive, and no fee afterwards,
    /// which is the whole rent-or-develop decision (design 07 §3).
    /// </summary>
    [Fact]
    public void R17V7_research_spends_monthly_and_owns_the_node_at_completion()
    {
        Built built = Assert.IsType<Built>(EngineBuilder.Build(Fixture.Settings()));
        Engine engine = built.Engine;

        TechnologyNode node = Buyable(AcquisitionRoute.Research);

        Assert.IsType<Accepted>(engine.Commands.Submit(
            new ResearchTechnologyCommand(node.Id.Value)));

        var capabilities = engine.Provided.Resolve<CapabilityState>();

        Assert.DoesNotContain(node.Id, capabilities.Technology.Acquired);
        Assert.Single(capabilities.Technology.Researching);

        for (var month = 0; month < node.Research!.Months; month++)
            engine.Pipeline.AdvanceTick();

        Assert.Contains(node.Id, capabilities.Technology.Acquired);
        Assert.Equal(AcquisitionRoute.Research, capabilities.Technology.RouteOf(node.Id));
        Assert.Empty(capabilities.Technology.Researching);

        // Owned outright: no ongoing burden from this node.
        Assert.Equal(0.0, capabilities.Technology.LicenceFeeMillionsThisTick());

        Assert.Contains(
            engine.Audit.Query(new AuditQuery(null, AuditCategory.Financial, null, null)),
            entry => entry.Data.TryGetValue("spend", out AuditValue spend)
                && spend.Value == "technology-research");
    }

    /// <summary>
    /// THE DOORS REFUSE BY NAME: an unknown node, a node before its era, and a
    /// node already held each come back with the reason — every reason, never
    /// the first (R1 §2.5).
    /// </summary>
    [Fact]
    public void R17V7_procurement_refuses_by_name()
    {
        Built built = Assert.IsType<Built>(EngineBuilder.Build(Fixture.Settings()));
        Engine engine = built.Engine;

        var unknown = Assert.IsType<Rejected>(engine.Commands.Submit(
            new LicenseTechnologyCommand(new ContentId("warp-drive"))));
        Assert.Contains(unknown.Reasons,
            reason => reason.LocId == "$loc:reject.no-such-technology");

        TechnologyNode late = Registry().First(n =>
            n.AvailableFrom > Era.E1 && n.Routes.Contains(AcquisitionRoute.Licence));

        var early = Assert.IsType<Rejected>(engine.Commands.Submit(
            new LicenseTechnologyCommand(late.Id.Value)));
        Assert.Contains(early.Reasons,
            reason => reason.LocId == "$loc:reject.before-its-era");

        TechnologyNode node = Buyable(AcquisitionRoute.Licence);

        Assert.IsType<Accepted>(engine.Commands.Submit(
            new LicenseTechnologyCommand(node.Id.Value)));

        var twice = Assert.IsType<Rejected>(engine.Commands.Submit(
            new LicenseTechnologyCommand(node.Id.Value)));
        Assert.Contains(twice.Reasons,
            reason => reason.LocId == "$loc:reject.already-held");
    }

    /// <summary>
    /// THE ROUTE SURVIVES A RELOAD — the licence fee bills only what was
    /// LICENSED, so a save that forgot how a node was acquired would either
    /// stop billing a rented capability or start billing an owned one.
    /// </summary>
    [Fact]
    public void R17V7_the_acquisition_route_survives_a_reload()
    {
        Built built = Assert.IsType<Built>(EngineBuilder.Build(Fixture.Settings()));
        Engine engine = built.Engine;

        TechnologyNode licensed = Buyable(AcquisitionRoute.Licence);
        TechnologyNode researched = Buyable(AcquisitionRoute.Research, exclude: licensed.Id);

        Assert.IsType<Accepted>(engine.Commands.Submit(
            new LicenseTechnologyCommand(licensed.Id.Value)));
        Assert.IsType<Accepted>(engine.Commands.Submit(
            new ResearchTechnologyCommand(researched.Id.Value)));

        engine.Pipeline.AdvanceTick();

        using var container = new MemoryStream();
        SaveGame.Write(engine, Fixture.Settings().WorldSeed, container);
        container.Position = 0;

        Engine restored = Assert.IsType<Built>(
            SaveGame.Load(container, Fixture.Settings())).Engine;

        TechnologyState technology =
            restored.Provided.Resolve<CapabilityState>().Technology;

        Assert.Equal(AcquisitionRoute.Licence, technology.RouteOf(licensed.Id));
        Assert.Single(technology.Researching);
        Assert.Equal(researched.Id, technology.Researching[0].Tech);
        Assert.True(technology.LicenceFeeMillionsThisTick() > 0.0,
            "the reloaded licence stopped billing");
    }

    private static IReadOnlyList<TechnologyNode> Registry() => Fixture.Registry();

    /// <summary>An era-one node with no prerequisites offering the route —
    /// found in the shipped tree rather than named, so a content re-tiering
    /// cannot silently retire this suite's subject.</summary>
    private static TechnologyNode Buyable(AcquisitionRoute route, TechnologyId? exclude = null)
    {
        foreach (TechnologyNode node in Registry())
            if (node.AvailableFrom == Era.E1
                && node.Prerequisites.Count == 0
                && node.Routes.Contains(route)

                // The nodes worth buying are the ones waiting never delivers
                // — three-quarters of the tree (finding 293).
                && !node.Routes.Contains(AcquisitionRoute.Diffusion)
                && (exclude is null || !node.Id.Equals(exclude)))
                return node;

        throw new InvalidOperationException(
            $"the shipped tree offers no era-one prerequisite-free node by {route}");
    }
}
