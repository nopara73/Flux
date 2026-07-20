namespace Flux.Models;

public sealed class Exercise
{
    public required string Name { get; init; }

    public required string Gif { get; init; }

    public required DominantRegion DominantRegion { get; init; }

    public int Score { get; set; } = 0;
}
