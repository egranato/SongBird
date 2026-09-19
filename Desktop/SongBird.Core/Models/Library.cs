namespace SongBird.Core.Models;

public sealed class Library
{
    public required Guid Id { get; init; }
    public required string Name { get; set; }
    public required string RootPath { get; set; }
}
