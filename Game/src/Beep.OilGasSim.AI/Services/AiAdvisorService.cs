using Beep.OilGasSim.AI.Advisors;
using Beep.OilGasSim.AI.Context;
using Beep.OilGasSim.Application.Interfaces;
using Beep.OilGasSim.Domain.GameSessions;

namespace Beep.OilGasSim.AI.Services;

public interface IAiAdvisorService
{
    Task<AiAdvisorResponse> AskAsync(Guid sessionId, AiAdvisorRequest request, CancellationToken cancellationToken = default);
}

public sealed class AiAdvisorService : IAiAdvisorService
{
    private readonly IGameSessionStore _store;
    private readonly IAiContextBuilder _contextBuilder;
    private readonly IAiAdvisorEngine _engine;

    public AiAdvisorService(
        IGameSessionStore store,
        IAiContextBuilder contextBuilder,
        IAiAdvisorEngine engine)
    {
        _store = store;
        _contextBuilder = contextBuilder;
        _engine = engine;
    }

    public async Task<AiAdvisorResponse> AskAsync(
        Guid sessionId,
        AiAdvisorRequest request,
        CancellationToken cancellationToken = default)
    {
        var aggregate = await _store.GetAsync(sessionId, cancellationToken)
                        ?? throw new InvalidOperationException("Session not found.");

        if (!aggregate.Session.Companies.Any(c => c.Id == request.CompanyId))
        {
            throw new InvalidOperationException("Company not found in session.");
        }

        var context = _contextBuilder.Build(
            aggregate,
            request.CompanyId,
            request.SelectedBlockId,
            request.SelectedAssetId);

        return _engine.Generate(context, request);
    }
}

public interface IAiTurnReportService
{
    Task<AiTurnReport?> GetLatestReportAsync(Guid sessionId, Guid companyId, CancellationToken cancellationToken = default);
}

public sealed class AiTurnReportService : IAiTurnReportService
{
    private readonly IGameSessionStore _store;
    private readonly IAiContextBuilder _contextBuilder;
    private readonly IAiAdvisorEngine _engine;

    public AiTurnReportService(
        IGameSessionStore store,
        IAiContextBuilder contextBuilder,
        IAiAdvisorEngine engine)
    {
        _store = store;
        _contextBuilder = contextBuilder;
        _engine = engine;
    }

    public async Task<AiTurnReport?> GetLatestReportAsync(
        Guid sessionId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var aggregate = await _store.GetAsync(sessionId, cancellationToken);
        if (aggregate is null)
        {
            return null;
        }

        var lastResult = aggregate.TurnResults.OrderByDescending(r => r.TurnNumber).FirstOrDefault();
        if (lastResult is null || lastResult.Events.Count == 0)
        {
            return null;
        }

        var companyEvents = lastResult.Events
            .Where(e => e.CompanyId == companyId || e.IsPublic)
            .ToList();

        var context = _contextBuilder.Build(aggregate, companyId, null, null);
        var strategy = _engine.Generate(context, new AiAdvisorRequest
        {
            CompanyId = companyId,
            AdvisorType = AiAdvisorType.Strategy,
            Message = "Summarize turn and recommend next steps"
        });

        return new AiTurnReport
        {
            TurnNumber = lastResult.TurnNumber,
            Summary = $"Turn {lastResult.TurnNumber} complete. {strategy.Message}",
            Highlights = companyEvents.Select(e => $"{e.Category}: {e.Headline}").ToList(),
            Recommendations = strategy.SuggestedActions
        };
    }
}
