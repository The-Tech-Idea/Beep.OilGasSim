namespace Beep.OilGasSim.Simulation.Randomness;

public interface IGameRandom
{
    double NextDouble();
    int NextInt(int minInclusive, int maxExclusive);
}

public interface IGameRandomFactory
{
    IGameRandom CreateForTurn(int gameSeed, int turnNumber, string systemName);
}

public sealed class GameRandomFactory : IGameRandomFactory
{
    public IGameRandom CreateForTurn(int gameSeed, int turnNumber, string systemName)
    {
        var combined = HashCode.Combine(gameSeed, turnNumber, systemName);
        return new SeededRandom(combined);
    }
}

internal sealed class SeededRandom : IGameRandom
{
    private readonly Random _random;

    public SeededRandom(int seed) => _random = new Random(seed);

    public double NextDouble() => _random.NextDouble();

    public int NextInt(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
}
