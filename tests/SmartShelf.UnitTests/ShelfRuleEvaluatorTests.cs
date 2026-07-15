using SmartShelf.Domain.Entities;
using SmartShelf.Domain.Enums;
using SmartShelf.Domain.Services;
using SmartShelf.Domain.ValueObjects;
using Xunit;

namespace SmartShelf.UnitTests;

public sealed class ShelfRuleEvaluatorTests
{
    [Fact]
    public void Highest_severity_matching_rule_wins_before_priority()
    {
        var warning = new EvaluationRule(
            "Low stock", RuleMetric.InventoryPercent, RuleOperator.LessThan, 50,
            ShelfStatus.Warning, LedColor.Yellow, 1000);
        var critical = new EvaluationRule(
            "Very low stock", RuleMetric.InventoryPercent, RuleOperator.LessThan, 10,
            ShelfStatus.Critical, LedColor.Red, 1);

        var result = ShelfRuleEvaluator.Evaluate(new ShelfSignals(5, 30, false, true), [warning, critical]);

        Assert.Equal(ShelfStatus.Critical, result.Status);
        Assert.Equal(LedColor.Red, result.LedColor);
    }

    [Fact]
    public void No_custom_rules_uses_existing_default_policy()
    {
        var result = ShelfRuleEvaluator.Evaluate(new ShelfSignals(9, 30, false, true), []);
        Assert.Equal(ShelfStatus.Critical, result.Status);
    }
}
