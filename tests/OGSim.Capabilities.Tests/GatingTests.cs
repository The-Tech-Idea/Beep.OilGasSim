// R17's verification suite (SDD-005).

using OGSim.Capabilities;
using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Capabilities.Tests;

public static class Fx
{
    public static TechnologyId Tech(string name) => new(new ContentId(name));

    public static readonly TechnologyId Rotary = Tech("rotary-drilling");
    public static readonly TechnologyId Directional = Tech("directional-drilling");
    public static readonly TechnologyId Horizontal = Tech("horizontal-drilling");
    public static readonly TechnologyId Seismic3d = Tech("seismic-3d");

    /// <summary>A small graph with a real chain: horizontal needs directional
    /// needs rotary.</summary>
    public static IReadOnlyList<TechnologyNode> Graph =>
    [
        new(Rotary, Era.E1, DiffusionLagTicks: 0, [], [], null),
        new(Directional, Era.E2, DiffusionLagTicks: 24, [Rotary],
            [new MoveEnvelope(EnvelopeKind.MaxDrillingDepth,
                              EnvelopeContributionKind.Extension, 4500.0)], null),
        new(Horizontal, Era.E3, DiffusionLagTicks: 60, [Directional],
            [new MoveEnvelope(EnvelopeKind.MaxDrillingDepth,
                              EnvelopeContributionKind.Extension, 6000.0),
             new UnlockOption(new ContentId("horizontal-completion"))], null),
        new(Seismic3d, Era.E2, DiffusionLagTicks: 36, [],
            [], GrantsDetectClass: DetectClass.D2),
    ];

    public static EffectState Effects(double baseDepth = 3000.0)
    {
        var state = new EffectState(new Dictionary<EnvelopeKind, double>
        {
            [EnvelopeKind.MaxDrillingDepth] = baseDepth,
        });

        return state;
    }

    public static Requirements Needs(
        IReadOnlyList<TechnologyId>? tech = null,
        DetectClass? detect = null,
        double? depth = null) =>
        new(tech ?? [],
            detect,
            depth is double d ? [new EnvelopeCheck(EnvelopeKind.MaxDrillingDepth, d)] : []);
}

public class TechnologyStateTests
{
    [Fact] // R17.1: nothing is held to begin with
    public void R17V1_a_new_company_holds_nothing()
    {
        var state = new TechnologyState(Fx.Graph);

        Assert.False(state.Has(Fx.Rotary));
        Assert.Empty(state.Acquired);
        Assert.Equal(DetectClass.D0, state.MaxDetectClass);
    }

    [Fact] // R17.3: the routes converge on one grant
    public void R17V3_acquiring_a_node_grants_it()
    {
        var state = new TechnologyState(Fx.Graph);

        state.Acquire(Fx.Rotary, Era.E1);

        Assert.True(state.Has(Fx.Rotary));
        Assert.Equal(Fx.Rotary, Assert.Single(state.Acquired));
    }

    [Fact] // Prerequisites make the tree a SEQUENCE rather than a menu
    public void R17V3_an_unmet_prerequisite_is_refused_and_named()
    {
        var state = new TechnologyState(Fx.Graph);

        var fault = Assert.Throws<ModelFault>(() => state.Acquire(Fx.Horizontal, Era.E3));

        Assert.Contains("directional-drilling", fault.Fault.Detail);
        Assert.Contains("not held", fault.Fault.Detail);
    }

    [Fact] // R17.6: era gating — you cannot buy the future
    public void R17V6_a_node_before_its_era_is_refused()
    {
        var state = new TechnologyState(Fx.Graph);
        state.Acquire(Fx.Rotary, Era.E1);

        var fault = Assert.Throws<ModelFault>(() => state.Acquire(Fx.Directional, Era.E1));

        Assert.Contains("not available until era E2", fault.Fault.Detail);
    }

    [Fact] // The detect tier is DERIVED from the observation nodes held
    public void R17V1_the_detect_class_follows_from_the_nodes_held()
    {
        var state = new TechnologyState(Fx.Graph);
        Assert.Equal(DetectClass.D0, state.MaxDetectClass);

        state.Acquire(Fx.Seismic3d, Era.E2);

        // Derived, not stored — so acquiring a survey technology raises the tier
        // with no second place to update (law L5).
        Assert.Equal(DetectClass.D2, state.MaxDetectClass);
    }

    // ------------------------------------------------------------ diffusion

    [Fact] // SDD-005 §2: diffusion is a DATE, not an event
    public void R17V6_diffusion_grants_a_node_once_its_lag_has_elapsed()
    {
        var state = new TechnologyState(Fx.Graph);
        var eraStart = new Tick(100);

        state.ApplyDiffusion(Era.E2, eraStart, new Tick(110));
        Assert.True(state.Has(Fx.Rotary), "a zero-lag node diffuses immediately");
        Assert.False(state.Has(Fx.Directional), "24 ticks have not elapsed");

        state.ApplyDiffusion(Era.E2, eraStart, new Tick(124));
        Assert.True(state.Has(Fx.Directional));

        // A player who never spends a penny still advances — slowly, and always
        // behind. That is what makes "eventually becomes standard practice" a
        // pressure rather than a promise.
    }

    [Fact] // Diffusion respects prerequisites — it waits rather than throwing
    public void R17V6_diffusion_waits_for_prerequisites_rather_than_failing()
    {
        // Horizontal has a 60-tick lag and needs directional's 24. At tick 60
        // horizontal's own lag has elapsed but its prerequisite has not been
        // granted in this call yet — the ordered walk grants rotary and
        // directional first, so it lands.
        var state = new TechnologyState(Fx.Graph);

        state.ApplyDiffusion(Era.E3, new Tick(0), new Tick(60));

        Assert.True(state.Has(Fx.Rotary));
        Assert.True(state.Has(Fx.Directional));
        Assert.True(state.Has(Fx.Horizontal));
    }

    [Fact] // Diffusion never grants a future era's node
    public void R17V6_diffusion_respects_the_era()
    {
        var state = new TechnologyState(Fx.Graph);

        state.ApplyDiffusion(Era.E1, new Tick(0), new Tick(10_000));

        Assert.True(state.Has(Fx.Rotary));
        Assert.False(state.Has(Fx.Directional));
        Assert.False(state.Has(Fx.Horizontal));
    }

    [Fact] // Content errors are refused where the graph file is in hand
    public void R17V1_a_dangling_prerequisite_is_a_model_fault()
    {
        var fault = Assert.Throws<ModelFault>(() => new TechnologyState(
        [
            new TechnologyNode(Fx.Horizontal, Era.E1, 0, [Fx.Tech("never-shipped")], [], null),
        ]));

        // A node gated behind a technology nobody ships can never be acquired,
        // and discovering that on the tick a player tries would be the worst
        // possible moment to find a content bug.
        Assert.Contains("not in the graph", fault.Fault.Detail);
    }

    // ------------------------------------------------------- AllCapabilities

    [Fact] // A SHIPPED MODE, not scaffolding
    public void R17V1_all_capabilities_holds_everything_at_the_top_tier()
    {
        ICapabilitySet sandbox = new AllCapabilities();

        Assert.True(sandbox.Has(Fx.Horizontal));
        Assert.True(sandbox.Has(Fx.Tech("anything-at-all")));
        Assert.Equal(DetectClass.D3, sandbox.MaxDetectClass);

        // This is the sandbox all-tech modifier AND the composition every
        // pre-R17 phase ran under — which is why those phases' suites were
        // running a real configuration rather than a stub.
    }
}

public class GatingTests
{
    private static readonly IGatingValidator Validator = new GatingValidator();

    private static GateResult Check(
        Requirements requirements,
        ICapabilitySet? capabilities = null,
        IReadOnlyList<ServiceRental>? rentals = null,
        IEffectState? effects = null) =>
        Validator.Check(
            requirements,
            capabilities ?? new TechnologyState(Fx.Graph),
            rentals ?? [],
            effects ?? Fx.Effects());

    [Fact] // Nothing required, nothing missing
    public void R17V7_an_ungated_thing_passes()
    {
        Assert.IsType<GatePass>(Check(Fx.Needs()));
    }

    [Fact] // R12-V11 / R17.7: a missing technology is NAMED
    public void R17V7_a_missing_technology_is_named_specifically()
    {
        var fail = Assert.IsType<GateFail>(Check(Fx.Needs(tech: [Fx.Horizontal])));

        MissingTechnology missing = Assert.IsType<MissingTechnology>(Assert.Single(fail.Missing));
        Assert.Equal(Fx.Horizontal, missing.Tech);

        // A domain reason, renderable straight to the player — not
        // "requirements not met".
    }

    [Fact] // ALL misses, never just the first
    public void R17V7_every_miss_is_reported_together()
    {
        var fail = Assert.IsType<GateFail>(Check(Fx.Needs(
            tech: [Fx.Horizontal, Fx.Seismic3d],
            detect: DetectClass.D2,
            depth: 5000.0)));

        // Two technologies, a detect tier and an envelope — four reasons, one
        // report. A player who fixes one and resubmits only to hit the next has
        // been made to pay twice for one piece of information, and acquiring a
        // technology takes YEARS.
        Assert.Equal(4, fail.Missing.Count);
        Assert.Equal(2, fail.Missing.OfType<MissingTechnology>().Count());
        Assert.Single(fail.Missing.OfType<MissingDetectTier>());
        Assert.Single(fail.Missing.OfType<EnvelopeExceeded>());
    }

    [Fact] // A held technology satisfies its requirement
    public void R17V7_a_held_technology_passes()
    {
        var state = new TechnologyState(Fx.Graph);
        state.Acquire(Fx.Rotary, Era.E1);

        Assert.IsType<GatePass>(Check(Fx.Needs(tech: [Fx.Rotary]), state));
    }

    // ------------------------------------------------------------ rentals

    [Fact] // R17.7: a RENTAL satisfies the gate for that operation only
    public void R17V7_a_rental_satisfies_a_technology_requirement()
    {
        Requirements needs = Fx.Needs(tech: [Fx.Horizontal]);

        Assert.IsType<GateFail>(Check(needs));

        Assert.IsType<GatePass>(Check(needs,
            rentals: [new ServiceRental(Fx.Horizontal, Money.FromMillions(3.0))]));

        // Scoped to the operation: the rental arrives as an ARGUMENT rather
        // than as state, so it cannot leak into the capability set or into a
        // save (SDD-005 §2).
    }

    [Fact] // Renting the wrong thing does not help
    public void R17V7_an_unrelated_rental_does_not_satisfy_the_gate()
    {
        Assert.IsType<GateFail>(Check(
            Fx.Needs(tech: [Fx.Horizontal]),
            rentals: [new ServiceRental(Fx.Seismic3d, Money.FromMillions(1.0))]));
    }

    // ------------------------------------------------------------ detect

    [Fact] // The detect tier gate reports what was needed AND what is held
    public void R17V7_a_missing_detect_tier_reports_both_values()
    {
        var fail = Assert.IsType<GateFail>(Check(Fx.Needs(detect: DetectClass.D3)));

        MissingDetectTier missing = Assert.IsType<MissingDetectTier>(Assert.Single(fail.Missing));
        Assert.Equal(DetectClass.D3, missing.Required);
        Assert.Equal(DetectClass.D0, missing.Held);
    }

    [Fact] // Holding a higher tier than required passes
    public void R17V7_an_adequate_detect_tier_passes()
    {
        Assert.IsType<GatePass>(Check(Fx.Needs(detect: DetectClass.D2), new AllCapabilities()));
    }

    // ------------------------------------------------------------ envelopes

    [Fact] // Envelopes compare against EFFECTIVE values
    public void R17V2_an_envelope_check_reads_the_effective_value()
    {
        EffectState effects = Fx.Effects(baseDepth: 3000.0);

        var fail = Assert.IsType<GateFail>(Check(Fx.Needs(depth: 5000.0), effects: effects));
        EnvelopeExceeded exceeded = Assert.IsType<EnvelopeExceeded>(Assert.Single(fail.Missing));

        Assert.Equal(5000.0, exceeded.Required, 9);
        Assert.Equal(3000.0, exceeded.Effective, 9);

        // Technology EXTENDS what is possible.
        effects.Apply([new MoveEnvelope(
            EnvelopeKind.MaxDrillingDepth, EnvelopeContributionKind.Extension, 6000.0)]);

        Assert.IsType<GatePass>(Check(Fx.Needs(depth: 5000.0), effects: effects));
    }
}

public class EffectStateTests
{
    [Fact] // SDD-005 §4.1: RESTRICTIONS WIN
    public void R17V2_a_restriction_caps_an_extension()
    {
        EffectState effects = Fx.Effects(baseDepth: 3000.0);

        effects.Apply([new MoveEnvelope(
            EnvelopeKind.MaxDrillingDepth, EnvelopeContributionKind.Extension, 6000.0)]);
        Assert.Equal(6000.0, effects.EffectiveEnvelope(EnvelopeKind.MaxDrillingDepth), 9);

        // The environment caps what is PERMITTED. A rig that can technically
        // drill to 6 000 m in conditions it cannot work in still cannot work.
        effects.Apply([new MoveEnvelope(
            EnvelopeKind.MaxDrillingDepth, EnvelopeContributionKind.Restriction, 4000.0)]);

        Assert.Equal(4000.0, effects.EffectiveEnvelope(EnvelopeKind.MaxDrillingDepth), 9);
    }

    [Fact] // Extensions take the BEST, not the sum
    public void R17V2_two_extensions_do_not_add()
    {
        EffectState effects = Fx.Effects(baseDepth: 1000.0);

        effects.Apply(
        [
            new MoveEnvelope(EnvelopeKind.MaxDrillingDepth,
                             EnvelopeContributionKind.Extension, 5000.0),
            new MoveEnvelope(EnvelopeKind.MaxDrillingDepth,
                             EnvelopeContributionKind.Extension, 4000.0),
        ]);

        // Two technologies that each raise a rig to 5 000 m do not reach 10 000.
        // An extension is a claim about a ceiling, and the highest claim is the
        // ceiling.
        Assert.Equal(5000.0, effects.EffectiveEnvelope(EnvelopeKind.MaxDrillingDepth), 9);
    }

    [Fact] // Restrictions take the TIGHTEST, for the mirror reason
    public void R17V2_two_restrictions_take_the_tightest()
    {
        EffectState effects = Fx.Effects(baseDepth: 8000.0);

        effects.Apply(
        [
            new MoveEnvelope(EnvelopeKind.MaxDrillingDepth,
                             EnvelopeContributionKind.Restriction, 5000.0),
            new MoveEnvelope(EnvelopeKind.MaxDrillingDepth,
                             EnvelopeContributionKind.Restriction, 3000.0),
        ]);

        Assert.Equal(3000.0, effects.EffectiveEnvelope(EnvelopeKind.MaxDrillingDepth), 9);
    }

    [Fact] // An extension below the base changes nothing
    public void R17V2_an_extension_never_lowers_the_base()
    {
        EffectState effects = Fx.Effects(baseDepth: 4000.0);

        effects.Apply([new MoveEnvelope(
            EnvelopeKind.MaxDrillingDepth, EnvelopeContributionKind.Extension, 2000.0)]);

        Assert.Equal(4000.0, effects.EffectiveEnvelope(EnvelopeKind.MaxDrillingDepth), 9);
    }

    [Fact] // R17.7: an unlock makes a catalogue entry available
    public void R17V7_an_unlock_makes_a_catalogue_entry_available()
    {
        EffectState effects = Fx.Effects();
        var completion = new ContentId("horizontal-completion");

        Assert.False(effects.IsUnlocked(completion));

        effects.Apply([new UnlockOption(completion)]);

        Assert.True(effects.IsUnlocked(completion));
        Assert.Equal(completion, Assert.Single(effects.Unlocked));
    }

    [Fact] // Technology and environment share ONE path
    public void R17V2_technology_and_environment_apply_through_the_same_method()
    {
        var state = new TechnologyState(Fx.Graph);
        state.Acquire(Fx.Rotary, Era.E1);
        state.Acquire(Fx.Directional, Era.E2);

        EffectState effects = Fx.Effects(baseDepth: 3000.0);

        // Technology's effects...
        effects.Apply(state.ActiveEffects());
        Assert.Equal(4500.0, effects.EffectiveEnvelope(EnvelopeKind.MaxDrillingDepth), 9);

        // ...and the environment's, through the identical call. A cold climate
        // restricting operability and a technology extending it are the same
        // kind of statement, so they combine by one rule rather than two that
        // have to be kept agreeing.
        effects.Apply([new MoveEnvelope(
            EnvelopeKind.MaxDrillingDepth, EnvelopeContributionKind.Restriction, 3500.0)]);

        Assert.Equal(3500.0, effects.EffectiveEnvelope(EnvelopeKind.MaxDrillingDepth), 9);
    }

    [Fact] // A model parameter with no declared value is content nobody wrote
    public void R17V2_an_undeclared_parameter_is_a_model_fault_not_a_zero()
    {
        EffectState effects = Fx.Effects();

        Assert.Throws<ModelFault>(
            () => effects.Parameter(new ModelSlot("inflow"), new ParameterKey("skin-multiplier")));

        // And a slot with no plugin is a composition that never finished, not a
        // default implementation.
        Assert.Throws<ModelFault>(() => effects.SelectedPlugin(new ModelSlot("inflow")));
    }
}
