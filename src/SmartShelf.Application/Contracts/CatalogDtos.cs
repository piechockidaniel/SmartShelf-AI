namespace SmartShelf.Application.Contracts;

public sealed record ProductDto(
    Guid Id, string Sku, string Name, int Quantity, DateTime ExpirationDate,
    DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record DeviceDto(
    Guid Id, string Name, string SerialNumber, string Kind, string Status,
    DateTime LastSeen, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record EvaluationRuleDto(
    Guid Id, string Name, string Metric, string Operator, double Threshold,
    string ResultStatus, string LedColor, int Priority,
    DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record SaveProductRequest(string Sku, string Name, int Quantity, DateTime ExpirationDate);
public sealed record SaveDeviceRequest(string Name, string SerialNumber, string Kind);
public sealed record SaveEvaluationRuleRequest(
    string Name, string Metric, string Operator, double Threshold,
    string ResultStatus, string LedColor, int Priority);
