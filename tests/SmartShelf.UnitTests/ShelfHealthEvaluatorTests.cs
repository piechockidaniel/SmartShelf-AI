using SmartShelf.Domain.Enums;
using SmartShelf.Domain.Services;
using Xunit;

namespace SmartShelf.UnitTests;

public sealed class ShelfHealthEvaluatorTests
{
    [Fact]
    public void Evaluate_returns_offline_when_sensor_is_unavailable()
    {
        var assessment = ShelfHealthEvaluator.Evaluate(86, 30, false, false);

        Assert.Equal(ShelfStatus.Offline, assessment.Status);
        Assert.Equal(LedColor.Blue, assessment.LedColor);
    }

    [Fact]
    public void Evaluate_returns_critical_for_an_expired_product()
    {
        var assessment = ShelfHealthEvaluator.Evaluate(86, -1, true, true);

        Assert.Equal(ShelfStatus.Critical, assessment.Status);
        Assert.Equal(LedColor.Red, assessment.LedColor);
    }

    [Fact]
    public void Evaluate_returns_warning_for_near_expiry_inventory()
    {
        var assessment = ShelfHealthEvaluator.Evaluate(86, 5, false, true);

        Assert.Equal(ShelfStatus.Warning, assessment.Status);
        Assert.Equal(LedColor.Yellow, assessment.LedColor);
    }

    [Fact]
    public void Evaluate_returns_healthy_for_normal_shelf_state()
    {
        var assessment = ShelfHealthEvaluator.Evaluate(86, 30, false, true);

        Assert.Equal(ShelfStatus.Healthy, assessment.Status);
        Assert.Equal(LedColor.Green, assessment.LedColor);
    }
}
