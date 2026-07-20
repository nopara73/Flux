using Flux.Models;

namespace Flux.Data;

public interface IExerciseDatabase
{
    IReadOnlyList<Exercise> Exercises { get; }
}
