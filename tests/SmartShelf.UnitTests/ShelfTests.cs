using SmartShelf.Domain.Entities;
using SmartShelf.Domain.Enums;
using SmartShelf.Domain.ValueObjects;
using Xunit;

namespace SmartShelf.UnitTests;

public sealed class ShelfTests
{
    [Fact]
    public void Shelf_configuration_can_be_updated_and_disabled()
    {
        var shelf = new Shelf("Produce A", new ShelfLocation("WH-1", "A1", "S1", "P1"));
        var controllerId = Guid.NewGuid();
        shelf.ReplaceBindings([new ShelfResourceBinding(ShelfResourceKind.Controller, controllerId)]);

        shelf.UpdateConfiguration(
            "Produce B", new ShelfLocation("WH-1", "A2", "S2", "P2"));
        shelf.SetEnabled(false);

        Assert.Equal("Produce B", shelf.Name);
        Assert.Equal("A2", shelf.Location.Aisle);
        Assert.Contains(shelf.Bindings, binding => binding.ResourceId == controllerId);
        Assert.False(shelf.Enabled);
        Assert.Equal(4, shelf.Version);
        Assert.NotNull(shelf.UpdatedAt);
    }

    [Fact]
    public void Shelf_requires_a_name_and_physical_location()
    {
        Assert.Throws<ArgumentException>(() =>
            new Shelf("", new ShelfLocation("WH-1", "A1", "S1", "P1")));
        Assert.Throws<ArgumentException>(() =>
            new Shelf("Shelf", new ShelfLocation("", "A1", "S1", "P1")));
    }
}
