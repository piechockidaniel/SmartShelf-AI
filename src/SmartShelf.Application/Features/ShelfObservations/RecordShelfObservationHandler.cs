using SmartShelf.Application.Abstractions.Persistence;
using SmartShelf.Application.Abstractions.Telemetry;
using SmartShelf.Application.Contracts;
using SmartShelf.Domain.Enums;
using SmartShelf.Domain.Services;

namespace SmartShelf.Application.Features.ShelfObservations;

public sealed class RecordShelfObservationHandler(
    IShelfObservationStore observationStore,
    IAlertStore alertStore,
    ILedController ledController)
{
    public async Task<LatestShelfObservationDto> HandleAsync(
        RecordShelfObservationCommand request,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(request.Observation.InventoryPercent, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(request.Observation.InventoryPercent, 100);

        var assessment = ShelfHealthEvaluator.Evaluate(
            request.Observation.InventoryPercent,
            request.Observation.DaysUntilExpiration,
            request.Observation.ExpiredProductDetected,
            request.Observation.SensorOnline);

        var decision = new ShelfDecisionDto(
            request.ShelfId, assessment.Status.ToString(), assessment.LedColor.ToString(),
            (float)assessment.Confidence, assessment.Reason);
        var result = new LatestShelfObservationDto(request.ShelfId, request.Observation, decision);

        await observationStore.SaveAsync(result, cancellationToken);
        await ledController.SetAsync(request.ShelfId, assessment.LedColor, cancellationToken);

        if (assessment.Status == ShelfStatus.Healthy)
        {
            await alertStore.ResolveOpenAsync(
                request.ShelfId, request.Observation.CapturedAt, cancellationToken);
        }
        else
        {
            var severity = assessment.Status == ShelfStatus.Critical
                ? AlertSeverity.Critical
                : AlertSeverity.Warning;
            await alertStore.UpsertOpenAsync(
                request.ShelfId, severity, assessment.Reason,
                request.Observation.CapturedAt, cancellationToken);
        }

        return result;
    }
}
