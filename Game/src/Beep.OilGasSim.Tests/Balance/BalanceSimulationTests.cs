using Beep.OilGasSim.Application.GameSessions;
using Beep.OilGasSim.Tests.Simulation;

namespace Beep.OilGasSim.Tests.Balance;

public class BalanceSimulationTests
{
    [Fact]
    public async Task FunMode_BatchSimulation_ProducesValidMetrics()
    {
        var service = TestServiceFactory.CreateService(includeFunMode: true);
        var runner = new BalanceSimulationRunner(service);

        var report = await runner.RunAsync("fun", gameCount: 4);

        Assert.Equal(4, report.GamesRun);
        Assert.Equal("fun", report.ModeProfileId);
        Assert.InRange(report.DiscoveryRate, 0, 1);
        Assert.InRange(report.ProductionReachRate, 0, 1);
        Assert.InRange(report.FinancialDistressRate, 0, 1);
        Assert.InRange(report.DryHoleRate, 0, 1);
    }

    [Fact]
    public async Task BalancedMode_BatchSimulation_ProducesValidMetrics()
    {
        var service = TestServiceFactory.CreateService();
        var runner = new BalanceSimulationRunner(service);

        var report = await runner.RunAsync("balanced", gameCount: 3);

        Assert.Equal(3, report.GamesRun);
        Assert.Equal("balanced", report.ModeProfileId);
        Assert.InRange(report.DiscoveryRate, 0, 1);
        Assert.InRange(report.ProductionReachRate, 0, 1);
        Assert.InRange(report.FinancialDistressRate, 0, 1);
    }
}
