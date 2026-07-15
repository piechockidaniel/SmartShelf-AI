using SmartShelf.Application.Abstractions.Persistence;
using SmartShelf.Application.Abstractions.Telemetry;
using SmartShelf.Application.Contracts;
using SmartShelf.Domain.Enums;
using SmartShelf.Domain.Services;

namespace SmartShelf.Application.Features.ShelfObservations;

public sealed class RecordShelfObservationHandler(
    IShelfRepository shelves,
    IResourceCatalogStore resources,
    IObservationCommandStore observationStore,
    ILedController ledController)
{
    public async Task<LatestShelfObservationDto> HandleAsync(
        RecordShelfObservationCommand request,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(request.Observation.InventoryPercent, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(request.Observation.InventoryPercent, 100);

        var shelf = await shelves.GetByIdAsync(request.ShelfId, cancellationToken)
            ?? throw new KeyNotFoundException($"Shelf '{request.ShelfId:D}' does not exist.");
        if (!shelf.Enabled)
        {
            throw new InvalidOperationException("Observations cannot be recorded for a disabled shelf.");
        }

        var ruleIds = shelf.Bindings
            .Where(binding => binding.Kind == ShelfResourceKind.EvaluationRule)
            .Select(binding => binding.ResourceId);
        var rules = await resources.GetRulesAsync(ruleIds, cancellationToken);
        var assessment = ShelfRuleEvaluator.Evaluate(
            new SmartShelf.Domain.ValueObjects.ShelfSignals(
                request.Observation.InventoryPercent,
                request.Observation.DaysUntilExpiration,
                request.Observation.ExpiredProductDetected,
                request.Observation.SensorOnline),
            rules);

        var decision = new ShelfDecisionDto(
            request.ShelfId, assessment.Status.ToString(), assessment.LedColor.ToString(),
            (float)assessment.Confidence, assessment.Reason);
        var result = new LatestShelfObservationDto(request.ShelfId, request.Observation, decision);

        AlertSeverity? severity = assessment.Status == ShelfStatus.Healthy
            ? null
            : assessment.Status == ShelfStatus.Critical
                ? AlertSeverity.Critical
                : AlertSeverity.Warning;
        await observationStore.RecordAsync(result, severity, cancellationToken);
        await ledController.SetAsync(request.ShelfId, assessment.LedColor, cancellationToken);

        return result;
    }
}
