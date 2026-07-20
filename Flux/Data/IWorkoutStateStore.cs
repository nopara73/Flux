using Flux.Models;

namespace Flux.Data;

public interface IWorkoutStateStore
{
    WorkoutState Load();

    void Save(WorkoutState state);
}
