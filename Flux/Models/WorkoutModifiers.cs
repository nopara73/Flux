namespace Flux.Models;

[Flags]
public enum WorkoutModifiers
{
    None = 0,
    Insect = 1,
    Silence = 2,
    Mirror = 4,
    // Internal qualifier for the Mirror equipment modifier. A compact mirror is
    // represented by Mirror; a tall mirror by Mirror | TallMirror. The
    // qualifier is never valid without Mirror.
    TallMirror = 8,
    HardFloor = 16,
}
