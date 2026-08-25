// R17.1 / R17.3 / R17.6 — the company's capabilities (SDD-005 §2, design 07).
//
// ICapabilitySet IS TWO MEMBERS WIDE, deliberately. Tiers and envelopes are not
// queried here — they are content gated BY these two answers, and keeping the
// interface this narrow is what keeps every rejection explainable: a refusal
// names a technology or a detect tier, never "requirements not met".
//
// DIFFUSION IS A DATE, NOT AN EVENT. A node auto-grants at
// (era start + its diffusion lag) — content, deterministic, no draws. The same
// mechanism rivals use with their personality lag. "Everything eventually
// becomes standard practice" is a clock the player can read and plan against.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Capabilities;

/// <summary>One node of the technology graph, from content.</summary>
/// <summary>What holding a licensed node costs, per tick, forever.</summary>
public sealed record TechnologyLicenceTerms(double FeeMillionsPerTick);

/// <summary>What an in-house programme costs and how long it runs.</summary>
public sealed record TechnologyResearchTerms(int Months, double CostMillionsPerMonth);

public sealed record TechnologyNode(
    TechnologyId Id,
    Era AvailableFrom,
    int DiffusionLagTicks,                    // 07 §3's free route
    IReadOnlyList<TechnologyId> Prerequisites,
    IReadOnlyList<Effect> Effects,
    DetectClass? GrantsDetectClass,           // observation nodes raise the tier
    IReadOnlyList<AcquisitionRoute> Routes,   // TECH_TREE's R L S D column

    /// <summary>Licence route terms where offered (finding 293) — an ongoing
    /// fee for as long as the node is held: fast, no ownership, expensive
    /// forever (design 07 §3).</summary>
    TechnologyLicenceTerms? Licence,

    /// <summary>R&amp;D route terms where offered — a sustained monthly budget
    /// for a stated number of months, then the node is owned outright.</summary>
    TechnologyResearchTerms? Research)
{
    // Finding 131.
    public bool Equals(TechnologyNode? other) =>
        other is not null && Id.Equals(other.Id) && AvailableFrom == other.AvailableFrom
        && DiffusionLagTicks == other.DiffusionLagTicks
        && GrantsDetectClass == other.GrantsDetectClass
        && Structural.Equal(Prerequisites, other.Prerequisites)
        && Structural.Equal(Effects, other.Effects)
        && Structural.Equal(Routes, other.Routes)
        && Equals(Licence, other.Licence) && Equals(Research, other.Research);

    public override int GetHashCode() =>
        HashCode.Combine(Id, AvailableFrom, DiffusionLagTicks, GrantsDetectClass,
            Structural.HashOf(Prerequisites), Structural.HashOf(Effects),
            Structural.HashOf(Routes));
}

/// <summary>
/// SDD-005 §2's real implementation: the nodes this company holds, era-filtered.
/// </summary>
public sealed class TechnologyState : ICapabilitySet
{
    private readonly Dictionary<TechnologyId, TechnologyNode> _graph = [];
    private readonly List<TechnologyId> _order = [];
    private readonly HashSet<TechnologyId> _acquired = [];
    private readonly List<TechnologyId> _acquiredOrder = [];

    public TechnologyState(IReadOnlyList<TechnologyNode> graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        for (int i = 0; i < graph.Count; i++)
        {
            TechnologyNode node = graph[i];

            if (!_graph.TryAdd(node.Id, node))
                throw new ModelFault("SDD-005 §2", null,
                    $"technology {node.Id.Value.Value} is declared twice");

            _order.Add(node.Id);
        }

        // Prerequisites must exist. A node gated behind a technology nobody ships
        // is a node nobody can ever acquire, and discovering that on the tick a
        // player tries would be a content bug found at the worst moment.
        for (int i = 0; i < _order.Count; i++)
        {
            TechnologyNode node = _graph[_order[i]];

            for (int p = 0; p < node.Prerequisites.Count; p++)
                if (!_graph.ContainsKey(node.Prerequisites[p]))
                    throw new ModelFault("SDD-005 §2", null,
                        $"technology {node.Id.Value.Value} requires " +
                        $"{node.Prerequisites[p].Value.Value}, which is not in the graph");
        }
    }

    public bool Has(TechnologyId tech) => _acquired.Contains(tech);

    /// <summary>
    /// D0..D3, derived from the observation nodes held (design 06 §2.3).
    ///
    /// <para>Derived rather than stored, so acquiring a survey technology raises
    /// the tier without a second place to update — law L5, and the reason a
    /// below-tier survey spawning nothing needs no bookkeeping of its own.</para>
    /// </summary>
    public DetectClass MaxDetectClass
    {
        get
        {
            DetectClass best = DetectClass.D0;

            for (int i = 0; i < _acquiredOrder.Count; i++)
                if (_graph[_acquiredOrder[i]].GrantsDetectClass is DetectClass granted
                    && granted > best)
                    best = granted;

            return best;
        }
    }

    /// <summary>Acquired nodes, in acquisition order — what the effect state applies.</summary>
    public IReadOnlyList<TechnologyId> Acquired => _acquiredOrder;

    /// <summary>
    /// R17.3's routes converge here: research, licence, acquisition or
    /// diffusion all end in the same grant.
    ///
    /// <para>Prerequisites are checked, and an unmet one is a REFUSAL naming it.
    /// Granting anyway would let a route bypass the graph, and the graph is the
    /// only thing that makes the technology tree a sequence rather than a menu.</para>
    /// </summary>
    /// <summary>Which route each held node arrived by (finding 293) — the
    /// licence fee bills only what was LICENSED; owned and diffused nodes
    /// carry no ongoing charge.</summary>
    private readonly Dictionary<TechnologyId, AcquisitionRoute> _routeOf = [];

    /// <summary>Programmes under way: the node and the months left. Insertion
    /// order, for the save (rule D-5).</summary>
    private readonly List<(TechnologyId Tech, int Remaining)> _research = [];

    public AcquisitionRoute RouteOf(TechnologyId tech) =>
        _routeOf.TryGetValue(tech, out AcquisitionRoute route)
            ? route
            : throw new ModelFault("SDD-005 §2", null,
                $"technology {tech.Value.Value} is not held, so it has no acquisition route");

    /// <summary>The programmes running, oldest first.</summary>
    public IReadOnlyList<(TechnologyId Tech, int Remaining)> Researching => _research;

    public void Acquire(TechnologyId tech, Era currentEra, AcquisitionRoute route)
    {
        if (!_graph.TryGetValue(tech, out TechnologyNode? node))
            throw new ModelFault("SDD-005 §2", null,
                $"technology {tech.Value.Value} is not in the graph");

        if (node.AvailableFrom > currentEra)
            throw new ModelFault("SDD-005 §2", null,
                $"technology {tech.Value.Value} is not available until era " +
                $"{node.AvailableFrom}; it is {currentEra}");

        for (int i = 0; i < node.Prerequisites.Count; i++)
            if (!_acquired.Contains(node.Prerequisites[i]))
                throw new ModelFault("SDD-005 §2", null,
                    $"technology {tech.Value.Value} requires " +
                    $"{node.Prerequisites[i].Value.Value}, which is not held");

        if (_acquired.Add(tech))
        {
            _acquiredOrder.Add(tech);
            _routeOf[tech] = route;
        }
    }

    /// <summary>
    /// Start an in-house programme (design 07 §3's R&amp;D route, finding
    /// 293): a sustained monthly budget for the node's stated months, then
    /// the node is owned. The same checks a grant makes, plus the route being
    /// offered — misuse is a fault; the COMMAND's validator asks first and
    /// refuses politely.
    /// </summary>
    public void StartResearch(TechnologyId tech, Era currentEra)
    {
        if (!_graph.TryGetValue(tech, out TechnologyNode? node))
            throw new ModelFault("SDD-005 §2", null,
                $"technology {tech.Value.Value} is not in the graph");

        if (node.Research is null || !node.Routes.Contains(AcquisitionRoute.Research))
            throw new ModelFault("SDD-005 §2", null,
                $"technology {tech.Value.Value} offers no research route");

        if (node.AvailableFrom > currentEra)
            throw new ModelFault("SDD-005 §2", null,
                $"technology {tech.Value.Value} is not available until era " +
                $"{node.AvailableFrom}; it is {currentEra}");

        for (int i = 0; i < node.Prerequisites.Count; i++)
            if (!_acquired.Contains(node.Prerequisites[i]))
                throw new ModelFault("SDD-005 §2", null,
                    $"technology {tech.Value.Value} requires " +
                    $"{node.Prerequisites[i].Value.Value}, which is not held");

        if (_acquired.Contains(tech))
            throw new ModelFault("SDD-005 §2", null,
                $"technology {tech.Value.Value} is already held");

        for (int i = 0; i < _research.Count; i++)
            if (_research[i].Tech.Equals(tech))
                throw new ModelFault("SDD-005 §2", null,
                    $"technology {tech.Value.Value} is already being researched");

        _research.Add((tech, node.Research.Months));
    }

    /// <summary>A restore's replay of an in-flight programme — the months
    /// left come from the save, not from the node's full term.</summary>
    public void RestoreResearch(TechnologyId tech, int remaining) =>
        _research.Add((tech, remaining));

    /// <summary>
    /// One month of every running programme (finding 293): bills the month,
    /// completes what finished — granted by the same door every route uses —
    /// and answers what this tick's research spend was.
    /// </summary>
    public (IReadOnlyList<TechnologyId> Completed, double SpendMillions) AdvanceResearch(
        Era currentEra)
    {
        var completed = new List<TechnologyId>();
        var spend = 0.0;

        for (int i = _research.Count - 1; i >= 0; i--)
        {
            (TechnologyId tech, int remaining) = _research[i];

            // The node arrived by another route mid-programme — diffusion
            // caught up, most likely. The budget stops: paying to develop
            // what is already standard practice is not a decision anyone
            // would defend (finding 293).
            if (_acquired.Contains(tech))
            {
                _research.RemoveAt(i);
                continue;
            }

            spend += _graph[tech].Research!.CostMillionsPerMonth;
            remaining--;

            if (remaining > 0)
            {
                _research[i] = (tech, remaining);
                continue;
            }

            _research.RemoveAt(i);
            Acquire(tech, currentEra, AcquisitionRoute.Research);
            completed.Add(tech);
        }

        return (completed, spend);
    }

    /// <summary>What holding the licensed nodes costs this tick (design 07
    /// §3: expensive forever) — owned, researched and diffused nodes bill
    /// nothing.</summary>
    public double LicenceFeeMillionsThisTick()
    {
        var fee = 0.0;

        for (int i = 0; i < _acquiredOrder.Count; i++)
            if (_routeOf.TryGetValue(_acquiredOrder[i], out AcquisitionRoute route)
                && route == AcquisitionRoute.Licence)
                fee += _graph[_acquiredOrder[i]].Licence!.FeeMillionsPerTick;

        return fee;
    }

    /// <summary>
    /// SDD-005 §2's diffusion: everything whose era has started and whose lag has
    /// elapsed, granted free.
    ///
    /// <para>Deterministic and in graph order, so prerequisites land before the
    /// nodes that need them within one call. A player who never spends a penny
    /// on research still advances — slowly, and always behind — which is what
    /// "eventually becomes standard practice" has to mean if it is to be a
    /// pressure rather than a promise.</para>
    /// </summary>
    public void ApplyDiffusion(Era currentEra, Tick eraStart, Tick now)
    {
        for (int i = 0; i < _order.Count; i++)
        {
            TechnologyNode node = _graph[_order[i]];

            if (_acquired.Contains(node.Id)) continue;
            if (node.AvailableFrom > currentEra) continue;

            // TECH_TREE lists D for only some nodes. Horizontal drilling is
            // R L S — it never becomes standard practice by waiting, and
            // granting it on a timer would erase the difference between
            // "eventually everyone has this" and "go and get this", which is
            // the whole reason there are four routes (finding 128).
            if (!node.Routes.Contains(AcquisitionRoute.Diffusion)) continue;

            if (now.Value < eraStart.Value + node.DiffusionLagTicks) continue;

            // Prerequisites first — and a node whose prerequisites have not
            // diffused yet simply waits, rather than throwing. Diffusion is a
            // background process, not a command.
            bool ready = true;
            for (int p = 0; p < node.Prerequisites.Count; p++)
                if (!_acquired.Contains(node.Prerequisites[p])) { ready = false; break; }

            if (!ready) continue;

            _acquired.Add(node.Id);
            _acquiredOrder.Add(node.Id);

            // Standard practice carries no bill (finding 293): the route is
            // recorded so the licence fee knows this node is not its business.
            _routeOf[node.Id] = AcquisitionRoute.Diffusion;
        }
    }

    /// <summary>Every effect of every held node, in acquisition order.</summary>
    public IReadOnlyList<Effect> ActiveEffects()
    {
        var effects = new List<Effect>();

        for (int i = 0; i < _acquiredOrder.Count; i++)
            effects.AddRange(_graph[_acquiredOrder[i]].Effects);

        return effects;
    }
}

/// <summary>
/// SDD-005 §2's other implementation — <b>a shipped mode, not scaffolding</b>.
///
/// <para>It is the sandbox all-tech modifier (design 18 §5) and the composition
/// every pre-R17 phase ran under, which is why those phases' test suites were
/// running a real configuration rather than a stub. Complete and tested.</para>
/// </summary>
public sealed class AllCapabilities : ICapabilitySet
{
    public bool Has(TechnologyId tech) => true;

    public DetectClass MaxDetectClass => DetectClass.D3;
}
