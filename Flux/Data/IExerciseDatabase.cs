using Flux.Models;

namespace Flux.Data;

public interface IExerciseDatabase : IDisposable
{
    IReadOnlyList<Exercise> Exercises { get; }

    void UpdateScore(Exercise exercise);
}
