namespace Flux.Models;

public sealed class Exercise
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public required string Gif { get; init; }

    public required DominantRegion DominantRegion { get; init; }

    public int Score { get; set; } = 0;

    public required bool OnlyFeetTouchGround { get; init; }

    public required bool ShoeAgnostic { get; init; }

    public required int MaxSpaceMeters { get; init; }

    public required string Equipment { get; init; }

    public required bool Silent { get; init; }
}
