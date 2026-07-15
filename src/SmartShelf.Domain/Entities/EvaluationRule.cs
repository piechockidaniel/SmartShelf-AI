using SmartShelf.Domain.Common;
using SmartShelf.Domain.Enums;
using SmartShelf.Domain.ValueObjects;

namespace SmartShelf.Domain.Entities;

public sealed class EvaluationRule : AuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public RuleMetric Metric { get; private set; }
    public RuleOperator Operator { get; private set; }
    public double Threshold { get; private set; }
    public ShelfStatus ResultStatus { get; private set; }
    public LedColor LedColor { get; private set; }
    public int Priority { get; private set; }

    private EvaluationRule() { }

    public EvaluationRule(
        string name,
        RuleMetric metric,
        RuleOperator @operator,
        double threshold,
        ShelfStatus resultStatus,
        LedColor ledColor,
        int priority)
    {
        Update(name, metric, @operator, threshold, resultStatus, ledColor, priority);
    }

    public void Update(
        string name,
        RuleMetric metric,
        RuleOperator @operator,
        double threshold,
        ShelfStatus resultStatus,
        LedColor ledColor,
        int priority)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Metric = metric;
        Operator = @operator;
        Threshold = threshold;
        ResultStatus = resultStatus;
        LedColor = ledColor;
        Priority = priority;
        Touch();
    }

    public bool Matches(ShelfSignals signals)
    {
        var value = Metric switch
        {
            RuleMetric.InventoryPercent => signals.InventoryPercent,
            RuleMetric.DaysUntilExpiration => signals.DaysUntilExpiration,
            RuleMetric.ExpiredProductDetected => signals.ExpiredProductDetected ? 1d : 0d,
            RuleMetric.SensorOnline => signals.SensorOnline ? 1d : 0d,
            _ => throw new ArgumentOutOfRangeException()
        };

        return Operator switch
        {
            RuleOperator.LessThan => value < Threshold,
            RuleOperator.LessThanOrEqual => value <= Threshold,
            RuleOperator.Equal => Math.Abs(value - Threshold) < double.Epsilon,
            RuleOperator.NotEqual => Math.Abs(value - Threshold) >= double.Epsilon,
            RuleOperator.GreaterThan => value > Threshold,
            RuleOperator.GreaterThanOrEqual => value >= Threshold,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public static EvaluationRule Restore(
        Guid id,
        string name,
        RuleMetric metric,
        RuleOperator @operator,
        double threshold,
        ShelfStatus resultStatus,
        LedColor ledColor,
        int priority,
        DateTime createdAt,
        DateTime? updatedAt)
        => new()
        {
            Id = id,
            Name = name,
            Metric = metric,
            Operator = @operator,
            Threshold = threshold,
            ResultStatus = resultStatus,
            LedColor = ledColor,
            Priority = priority,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
}
