using SmartShelf.Domain.Entities;
using SmartShelf.Domain.Enums;
using SmartShelf.Domain.ValueObjects;

namespace SmartShelf.Domain.Services;

public static class ShelfRuleEvaluator
{
    public static ShelfHealthAssessment Evaluate(
        ShelfSignals signals,
        IReadOnlyCollection<EvaluationRule> rules)
    {
        if (rules.Count == 0)
        {
            return ShelfHealthEvaluator.Evaluate(
                signals.InventoryPercent,
                signals.DaysUntilExpiration,
                signals.ExpiredProductDetected,
                signals.SensorOnline);
        }

        var match = rules
            .Where(rule => rule.Matches(signals))
            .OrderByDescending(rule => Severity(rule.ResultStatus))
            .ThenByDescending(rule => rule.Priority)
            .FirstOrDefault();

        return match is null
            ? new(ShelfStatus.Healthy, LedColor.Green, "No configured shelf rule matched.", 0.90m)
            : new(match.ResultStatus, match.LedColor, $"Matched rule: {match.Name}.", 0.95m);
    }

    private static int Severity(ShelfStatus status) => status switch
    {
        ShelfStatus.Critical => 4,
        ShelfStatus.Offline => 3,
        ShelfStatus.Warning => 2,
        _ => 1
    };
}
