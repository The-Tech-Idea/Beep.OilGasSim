// R20d.12 — the market, and what it does to costs (SDD-009 §6).
//
// THE SECOND HALF OF A MOVING PRICE. When oil is high every company in the
// business wants a rig, a crew and a boat at the same time, and the price of all
// three goes up with it. That is why developing at the top of a cycle is a
// mistake a real operator makes: the revenue looks wonderful on the day the
// decision is taken, and the bill arrives at cycle prices too.
//
// Without it a high price is unambiguously good news and the only question is
// how much of it you can catch. With it, a boom is a worse time to BUILD and a
// better time to PRODUCE — which are different verbs, and telling them apart is
// the whole of counter-cyclical investment.
//
// ONE OWNER FOR THE MARKET (law L5). Two things read it — the ledger prices
// barrels, the scheduler prices work — and a month in which they disagreed about
// what the price was would be a month whose accounts do not close.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Company;

/// <summary>
/// This month's benchmark and this month's cost of doing business.
/// </summary>
public sealed class MarketState : IStateOwner
{
    private readonly Money _openingPrice;

    // A year of history, so the index can be driven by the year-on-year move
    // rather than by the last month's noise. A ring rather than a growing list:
    // a forty-year game is four hundred and eighty ticks and nothing needs the
    // other four hundred and sixty-eight.
    private readonly Money[] _lastYear = new Money[MonthsPerYear];

    private int _months;

    public MarketState(Money openingPrice, double costElasticity, double costDrift)
    {
        if (openingPrice <= Money.Zero)
            throw new ContentFault("SDD-009 §6", null,
                "a market opens at a positive price");

        if (!double.IsFinite(costElasticity) || costElasticity < 0.0)
            throw new ContentFault("SDD-009 §6", null,
                "cost elasticity is finite and non-negative; a negative one would make " +
                "service cheaper in a boom, which is the opposite of what a boom does");

        if (!double.IsFinite(costDrift))
            throw new ContentFault("SDD-009 §6", null, "cost drift is finite");

        _openingPrice = openingPrice;
        _elasticity = costElasticity;
        _drift = costDrift;

        OilPrice = openingPrice;
    }

    private readonly double _elasticity;
    private readonly double _drift;

    /// <summary>What a tonne of oil fetches this month.</summary>
    public Money OilPrice { get; private set; }

    /// <summary>
    /// What a day of work costs, relative to the opening year. Every capital
    /// item and every service is quoted through it, so a rig-month in a boom is
    /// dearer than the same rig-month in a slump (SDD-009 §6's ED4).
    /// </summary>
    public double CostIndex { get; private set; } = 1.0;

    // ------------------------------------------------------------- the save
    //
    // THE MARKET WAS NOT SAVED AT ALL, and it was invisible because nothing had
    // ever reloaded a game (finding 188). Restoring the price STREAM's position
    // without the price itself resumes a random walk from the right dice and the
    // wrong place: production came back identical to the tick and the money did
    // not, because the same barrels were sold at a different price.
    //
    // Three facts move and the rest are content: this month's price, the year of
    // history the cost index is driven from, and how many months have gone by —
    // the ring buffer means the last two are one array and one counter.

    public StateKey Key { get; } = new("company.market");

    public int SchemaVersion => 1;

    public void Capture(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteInt64("price", OilPrice.Cents);
        writer.WriteDouble("cost-index", CostIndex);
        writer.WriteInt64("months", _months);

        for (int i = 0; i < _lastYear.Length; i++)
            writer.WriteInt64(
                "m" + i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                _lastYear[i].Cents);
    }

    public void Restore(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        OilPrice = new Money(reader.ReadInt64("price"));
        CostIndex = reader.ReadDouble("cost-index");
        _months = (int)reader.ReadInt64("months");

        for (int i = 0; i < _lastYear.Length; i++)
            _lastYear[i] = new Money(reader.ReadInt64(
                "m" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    /// <summary>
    /// One month. The market moves, and then the cost of working in it follows
    /// — in that order, because this year's service prices are set against a
    /// price that has already happened.
    /// </summary>
    public void Advance(IPriceModel prices, IRandomStream price)
    {
        ArgumentNullException.ThrowIfNull(prices);
        ArgumentNullException.ThrowIfNull(price);

        Money yearAgo = _months < MonthsPerYear ? _openingPrice : _lastYear[_months % MonthsPerYear];

        _lastYear[_months % MonthsPerYear] = OilPrice;
        _months++;

        OilPrice = prices.Advance(OilPrice, price);

        // YEAR ON YEAR, not month on month. Service contracts are renegotiated
        // against where the market has BEEN, not against last month's wobble —
        // and an index that chased monthly noise would make the cost of a rig a
        // random number rather than a cycle a player can read and act on.
        double yearOnYear = (OilPrice.Cents - yearAgo.Cents) / (double)yearAgo.Cents;

        // SPREAD OVER THE YEAR IT DESCRIBES (SDD-009 §6's R20d.12 amendment).
        // A year-on-year move applied in full every month is applied twelve
        // times over: a market half again on the year would lift costs 17.5% in
        // the first month, then again on the new figure, and again — sevenfold
        // over the year rather than 17.5%. Measured at 1.78 over a decade before
        // this was corrected, which reads as a punishing market and is
        // arithmetic.
        CostIndex *= 1.0 + (_elasticity * yearOnYear / MonthsPerYear) + _drift;

        // A FLOOR, and it is not a tuning fudge. Costs are sticky downwards:
        // rigs are stacked rather than scrapped, crews are kept on, and the
        // yard still wants paying. Without one, a long enough slump would drive
        // the index towards zero and make everything free — which would turn
        // the worst market in the game into the best time to build anything.
        if (CostIndex < MinimumIndex) CostIndex = MinimumIndex;
    }

    /// <summary>What a quoted price becomes at today's index.</summary>
    public Money Quoted(Money listed) => Money.RoundHalfEven(listed.Cents * CostIndex);

    private const int MonthsPerYear = 12;

    /// <summary>Half the opening year's rates. Deep enough to be a real
    /// discount in a slump, short of free.</summary>
    private const double MinimumIndex = 0.5;
}
