using System.Globalization;
using Dapper;
using SmartShelf.Application.Abstractions.Persistence;
using SmartShelf.Domain.Entities;
using SmartShelf.Domain.Enums;

namespace SmartShelf.Infrastructure.Sqlite.Persistence;

public sealed class SqliteResourceCatalogStore : IResourceCatalogStore
{
    private readonly SqliteDatabase _database;

    public SqliteResourceCatalogStore(SqliteDatabase database) => _database = database;
    public SqliteResourceCatalogStore(string connectionString) => _database = new(connectionString);

    public async Task<IReadOnlyList<Product>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<ProductRow>(new CommandDefinition(
            "SELECT id Id, sku Sku, name Name, quantity Quantity, expiration_date ExpirationDate, created_at CreatedAt, updated_at UpdatedAt FROM products ORDER BY name;",
            cancellationToken: cancellationToken));
        return rows.Select(Map).ToArray();
    }

    public async Task<IReadOnlyList<Device>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<DeviceRow>(new CommandDefinition(
            "SELECT id Id, name Name, serial_number SerialNumber, kind Kind, status Status, last_seen LastSeen, created_at CreatedAt, updated_at UpdatedAt FROM devices ORDER BY kind, name;",
            cancellationToken: cancellationToken));
        return rows.Select(Map).ToArray();
    }

    public async Task<IReadOnlyList<EvaluationRule>> GetRulesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<RuleRow>(new CommandDefinition(
            RuleSql + " ORDER BY priority DESC, name;", cancellationToken: cancellationToken));
        return rows.Select(Map).ToArray();
    }

    public async Task<IReadOnlyList<EvaluationRule>> GetRulesAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        var values = ids.Select(id => id.ToString("D")).Distinct().ToArray();
        if (values.Length == 0)
        {
            return [];
        }
        await using var connection = await _database.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<RuleRow>(new CommandDefinition(
            RuleSql + " WHERE id IN @values ORDER BY priority DESC;", new { values }, cancellationToken: cancellationToken));
        return rows.Select(Map).ToArray();
    }

    public async Task SaveProductAsync(Product product, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO products(id, sku, name, quantity, expiration_date, created_at, updated_at)
            VALUES(@Id, @Sku, @Name, @Quantity, @ExpirationDate, @CreatedAt, @UpdatedAt)
            ON CONFLICT(id) DO UPDATE SET sku=excluded.sku, name=excluded.name,
                quantity=excluded.quantity, expiration_date=excluded.expiration_date, updated_at=excluded.updated_at;
            """,
            new
            {
                Id = product.Id.ToString("D"), Sku = product.SKU, product.Name, product.Quantity,
                ExpirationDate = product.ExpirationDate.ToUniversalTime().ToString("O"),
                CreatedAt = product.CreatedAt.ToUniversalTime().ToString("O"),
                UpdatedAt = Timestamp(product.UpdatedAt)
            }, cancellationToken: cancellationToken));
    }

    public async Task SaveDeviceAsync(Device device, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO devices(id, name, serial_number, kind, status, last_seen, created_at, updated_at)
            VALUES(@Id, @Name, @SerialNumber, @Kind, @Status, @LastSeen, @CreatedAt, @UpdatedAt)
            ON CONFLICT(id) DO UPDATE SET name=excluded.name, serial_number=excluded.serial_number,
                kind=excluded.kind, status=excluded.status, last_seen=excluded.last_seen, updated_at=excluded.updated_at;
            """,
            new
            {
                Id = device.Id.ToString("D"), device.Name, device.SerialNumber,
                Kind = device.Kind.ToString(), Status = device.Status.ToString(),
                LastSeen = device.LastSeen.ToUniversalTime().ToString("O"),
                CreatedAt = device.CreatedAt.ToUniversalTime().ToString("O"),
                UpdatedAt = Timestamp(device.UpdatedAt)
            }, cancellationToken: cancellationToken));
    }

    public async Task SaveRuleAsync(EvaluationRule rule, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO evaluation_rules(
                id, name, metric, operator, threshold, result_status, led_color, priority, created_at, updated_at)
            VALUES(@Id, @Name, @Metric, @Operator, @Threshold, @ResultStatus, @LedColor, @Priority, @CreatedAt, @UpdatedAt)
            ON CONFLICT(id) DO UPDATE SET name=excluded.name, metric=excluded.metric,
                operator=excluded.operator, threshold=excluded.threshold,
                result_status=excluded.result_status, led_color=excluded.led_color,
                priority=excluded.priority, updated_at=excluded.updated_at;
            """,
            new
            {
                Id = rule.Id.ToString("D"), rule.Name, Metric = rule.Metric.ToString(),
                Operator = rule.Operator.ToString(), rule.Threshold,
                ResultStatus = rule.ResultStatus.ToString(), LedColor = rule.LedColor.ToString(),
                rule.Priority, CreatedAt = rule.CreatedAt.ToUniversalTime().ToString("O"),
                UpdatedAt = Timestamp(rule.UpdatedAt)
            }, cancellationToken: cancellationToken));
    }

    public async Task<bool> DeleteAsync(
        ShelfResourceKind kind,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        var bound = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COUNT(*) FROM shelf_resource_bindings WHERE kind=@kind AND resource_id=@id;",
            new { kind = kind.ToString(), id = id.ToString("D") }, cancellationToken: cancellationToken));
        if (bound > 0)
        {
            return false;
        }

        var table = kind switch
        {
            ShelfResourceKind.Product => "products",
            ShelfResourceKind.EvaluationRule => "evaluation_rules",
            _ => "devices"
        };
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            $"DELETE FROM {table} WHERE id=@id;", new { id = id.ToString("D") }, cancellationToken: cancellationToken));
        return affected > 0;
    }

    public async Task<bool> ExistsAsync(
        ShelfResourceKind kind,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        var table = kind switch
        {
            ShelfResourceKind.Product => "products",
            ShelfResourceKind.EvaluationRule => "evaluation_rules",
            _ => "devices"
        };
        var count = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            $"SELECT COUNT(*) FROM {table} WHERE id=@id" +
            (table == "devices" ? " AND kind=@deviceKind" : string.Empty) + ";",
            new { id = id.ToString("D"), deviceKind = kind.ToString() }, cancellationToken: cancellationToken));
        return count > 0;
    }

    internal async Task SeedDevelopmentAsync(CancellationToken cancellationToken = default)
    {
        if ((await GetDevicesAsync(cancellationToken)).Count == 0)
        {
            await SaveDeviceAsync(new Device("Edge controller 001", "edge-controller-001", DeviceKind.Controller), cancellationToken);
            await SaveDeviceAsync(new Device("Shelf camera 001", "camera-001", DeviceKind.Camera), cancellationToken);
            await SaveDeviceAsync(new Device("Inventory sensor 001", "sensor-001", DeviceKind.Sensor), cancellationToken);
            await SaveDeviceAsync(new Device("LED strip 001", "led-001", DeviceKind.LedOutput), cancellationToken);
        }
        if ((await GetProductsAsync(cancellationToken)).Count == 0)
        {
            await SaveProductAsync(new Product("DEMO-001", "Demo product", 25, DateTime.UtcNow.AddDays(30)), cancellationToken);
        }
        if ((await GetRulesAsync(cancellationToken)).Count == 0)
        {
            await SaveRuleAsync(new EvaluationRule(
                "Critical low stock", RuleMetric.InventoryPercent, RuleOperator.LessThanOrEqual,
                10, ShelfStatus.Critical, LedColor.Red, 100), cancellationToken);
            await SaveRuleAsync(new EvaluationRule(
                "Replenishment warning", RuleMetric.InventoryPercent, RuleOperator.LessThan,
                30, ShelfStatus.Warning, LedColor.Yellow, 50), cancellationToken);
        }
    }

    private static Product Map(ProductRow row)
        => Product.Restore(Guid.Parse(row.Id), row.Sku, row.Name, checked((int)row.Quantity),
            ParseDate(row.ExpirationDate), ParseDate(row.CreatedAt), ParseNullableDate(row.UpdatedAt));

    private static Device Map(DeviceRow row)
        => Device.Restore(Guid.Parse(row.Id), row.Name, row.SerialNumber,
            Enum.Parse<DeviceKind>(row.Kind), Enum.Parse<DeviceStatus>(row.Status),
            ParseDate(row.LastSeen), ParseDate(row.CreatedAt), ParseNullableDate(row.UpdatedAt));

    private static EvaluationRule Map(RuleRow row)
        => EvaluationRule.Restore(Guid.Parse(row.Id), row.Name, Enum.Parse<RuleMetric>(row.Metric),
            Enum.Parse<RuleOperator>(row.Operator), row.Threshold,
            Enum.Parse<ShelfStatus>(row.ResultStatus), Enum.Parse<LedColor>(row.LedColor),
            checked((int)row.Priority), ParseDate(row.CreatedAt), ParseNullableDate(row.UpdatedAt));

    private static string? Timestamp(DateTime? value) => value?.ToUniversalTime().ToString("O");
    private static DateTime ParseDate(string value) => DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    private static DateTime? ParseNullableDate(string? value) => value is null ? null : ParseDate(value);

    private const string RuleSql =
        "SELECT id Id, name Name, metric Metric, operator Operator, threshold Threshold, result_status ResultStatus, led_color LedColor, priority Priority, created_at CreatedAt, updated_at UpdatedAt FROM evaluation_rules";
    private sealed record ProductRow(string Id, string Sku, string Name, long Quantity, string ExpirationDate, string CreatedAt, string? UpdatedAt);
    private sealed record DeviceRow(string Id, string Name, string SerialNumber, string Kind, string Status, string LastSeen, string CreatedAt, string? UpdatedAt);
    private sealed record RuleRow(string Id, string Name, string Metric, string Operator, double Threshold, string ResultStatus, string LedColor, long Priority, string CreatedAt, string? UpdatedAt);
}
