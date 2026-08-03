using Flux.Models;

namespace Flux.Services;

public sealed record StoredExerciseSnapshot(string Name, string Video, int Score);

public static class CatalogMigrationRules
{
    public static IReadOnlySet<int> ValidatePreservedCatalog(
        IReadOnlyCollection<Exercise> bundledCatalog,
        IReadOnlyDictionary<int, StoredExerciseSnapshot> storedExercises)
    {
        ArgumentNullException.ThrowIfNull(bundledCatalog);
        ArgumentNullException.ThrowIfNull(storedExercises);

        Dictionary<int, Exercise> bundledById;
        try
        {
            bundledById = bundledCatalog.ToDictionary(exercise => exercise.Id);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                "The bundled catalog contains a duplicate stable exercise ID.",
                exception);
        }

        foreach ((int exerciseId, StoredExerciseSnapshot stored) in storedExercises)
        {
            if (!bundledById.TryGetValue(exerciseId, out Exercise? bundled))
            {
                throw new InvalidOperationException(
                    $"The bundled catalog would remove existing exercise {exerciseId}.");
            }

            if (!string.Equals(stored.Name, bundled.Name, StringComparison.Ordinal) ||
                !string.Equals(stored.Video, bundled.Video, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"The bundled catalog would change the stable identity or " +
                    $"demonstration of existing exercise {exerciseId}.");
            }
        }

        return storedExercises.Keys.ToHashSet();
    }
}
