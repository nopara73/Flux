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
    // A rigid, slippery floor. This is one modifier;
    // slipperiness is not a separate equipment state.
    HardFloor = 16,
    Wall = 32,
    // Internal qualifier for the Wall equipment modifier. Wall alone excludes
    // movements that require sole-to-wall contact; Wall | SoleWallContact
    // allows them. The qualifier is never valid without Wall.
    SoleWallContact = 64,
}
