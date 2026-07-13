using SmartShelf.Domain.Enums;
using SmartShelf.Domain.ValueObjects;

namespace SmartShelf.Domain.Services;

public static class ShelfHealthEvaluator
{
    public static ShelfHealthAssessment Evaluate(
        int inventoryPercent,
        int daysUntilExpiration,
        bool expiredProductDetected,
        bool sensorOnline)
    {
        if (!sensorOnline)
        {
            return new(ShelfStatus.Offline, LedColor.Blue,
                "Sensor unavailable; keep last known state and request inspection.", 0.95m);
        }

        if (expiredProductDetected || inventoryPercent <= 10)
        {
            return new(ShelfStatus.Critical, LedColor.Red,
                "Immediate action required at the shelf edge.", 0.98m);
        }

        if (daysUntilExpiration < 7 || inventoryPercent < 30)
        {
            return new(ShelfStatus.Warning, LedColor.Yellow,
                "Local ARM64 controller predicts replenishment or expiry risk.", 0.86m);
        }

        return new(ShelfStatus.Healthy, LedColor.Green,
            "Shelf state is normal; no cloud round trip required.", 0.99m);
    }
}
