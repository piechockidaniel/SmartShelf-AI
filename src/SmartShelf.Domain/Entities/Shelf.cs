using SmartShelf.Domain.Common;
using SmartShelf.Domain.Enums;
using SmartShelf.Domain.ValueObjects;

namespace SmartShelf.Domain.Entities;

public class Shelf : AuditableEntity
{
    private readonly List<ShelfResourceBinding> _bindings = [];

    public string Name { get; private set; } = string.Empty;
    public ShelfLocation Location { get; private set; } = new("", "", "", "");
    public bool Enabled { get; private set; }
    public int Version { get; private set; } = 1;
    public LedColor LedColor { get; private set; }
    public ShelfStatus Status { get; private set; }
    public IReadOnlyCollection<ShelfResourceBinding> Bindings => _bindings;

    private Shelf() { }

    public Shelf(
        string name, ShelfLocation location)
    {
        ApplyConfiguration(name, location);
        Enabled = true;
        Status = ShelfStatus.Healthy;
        LedColor = LedColor.Green;
    }

    public Shelf(
        string name, ShelfLocation location, string? deviceId, string? cameraDevice)
        : this(name, location)
    {
        // Compatibility constructor. Legacy strings are migrated to typed resources by persistence.
    }

    public void UpdateConfiguration(
        string name, ShelfLocation location, string? deviceId = null, string? cameraDevice = null)
    {
        ApplyConfiguration(name, location);
        MarkChanged();
    }

    public void SetEnabled(bool enabled)
    {
        if (Enabled == enabled)
        {
            return;
        }

        Enabled = enabled;
        MarkChanged();
    }

    public void ReplaceBindings(IEnumerable<ShelfResourceBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        var replacement = bindings.ToArray();
        if (replacement.GroupBy(binding => new { binding.Kind, binding.ResourceId }).Any(group => group.Count() > 1))
        {
            throw new InvalidOperationException("A shelf cannot contain duplicate resource bindings.");
        }

        foreach (var singletonKind in new[]
                 {
                     ShelfResourceKind.Controller,
                     ShelfResourceKind.Camera,
                     ShelfResourceKind.LedOutput
                 })
        {
            if (replacement.Count(binding => binding.Kind == singletonKind) > 1)
            {
                throw new InvalidOperationException($"A shelf can have only one {singletonKind} binding.");
            }
        }

        if (_bindings.Select(Key).Order().SequenceEqual(replacement.Select(Key).Order()))
        {
            return;
        }

        _bindings.Clear();
        _bindings.AddRange(replacement);
        MarkChanged();
    }

    public static Shelf Restore(
        Guid id, string name, ShelfLocation location, bool enabled, int version,
        DateTime createdAt, DateTime? updatedAt,
        IEnumerable<ShelfResourceBinding>? bindings = null)
    {
        var shelf = new Shelf
        {
            Id = id,
            Name = name,
            Location = location,
            Enabled = enabled,
            Version = version,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            Status = ShelfStatus.Healthy,
            LedColor = LedColor.Green
        };
        if (bindings is not null)
        {
            shelf._bindings.AddRange(bindings);
        }
        return shelf;
    }

    public static Shelf Restore(
        Guid id, string name, ShelfLocation location, string? deviceId, string? cameraDevice,
        bool enabled, DateTime createdAt, DateTime? updatedAt)
        => Restore(id, name, location, enabled, 1, createdAt, updatedAt);

    private void ApplyConfiguration(
        string name, ShelfLocation location)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(location);
        ArgumentException.ThrowIfNullOrWhiteSpace(location.Warehouse);
        ArgumentException.ThrowIfNullOrWhiteSpace(location.Aisle);
        ArgumentException.ThrowIfNullOrWhiteSpace(location.Shelf);
        Name = name.Trim();
        Location = location;
    }

    private void MarkChanged()
    {
        Version++;
        Touch();
    }

    private static string Key(ShelfResourceBinding binding) => $"{binding.Kind}:{binding.ResourceId:D}";
}
