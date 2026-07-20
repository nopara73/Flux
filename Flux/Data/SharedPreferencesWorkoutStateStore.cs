using System.Text.Json;
using Android.Content;
using Flux.Models;

namespace Flux.Data;

public sealed class SharedPreferencesWorkoutStateStore : IWorkoutStateStore
{
    private const string PreferencesName = "flux_workout_state";
    private const string StateKey = "state";

    private readonly ISharedPreferences _preferences;

    public SharedPreferencesWorkoutStateStore(Context context)
    {
        _preferences = context.GetSharedPreferences(PreferencesName, FileCreationMode.Private)
            ?? throw new InvalidOperationException("Unable to open workout preferences.");
    }

    public WorkoutState Load()
    {
        string? json = _preferences.GetString(StateKey, null);

        if (string.IsNullOrWhiteSpace(json))
        {
            return new WorkoutState();
        }

        try
        {
            return JsonSerializer.Deserialize(json, WorkoutJsonContext.Default.WorkoutState)
                ?? new WorkoutState();
        }
        catch (JsonException)
        {
            return new WorkoutState();
        }
    }

    public void Save(WorkoutState state)
    {
        string json = JsonSerializer.Serialize(state, WorkoutJsonContext.Default.WorkoutState);
        ISharedPreferencesEditor editor = _preferences.Edit()
            ?? throw new InvalidOperationException("Unable to edit workout preferences.");

        editor.PutString(StateKey, json);

        if (!editor.Commit())
        {
            throw new InvalidOperationException("Unable to save workout progress.");
        }
    }
}
