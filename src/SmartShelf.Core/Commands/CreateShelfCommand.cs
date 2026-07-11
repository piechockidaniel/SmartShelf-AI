using SmartShelf.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartShelf.Core.Commands;
public sealed record CreateShelfCommand(
    string Name,
    ShelfLocation Location)
    : IRequest<Guid>;