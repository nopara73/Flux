using System.Text.Json;
using System.Text.Json.Serialization;
using Flux.Models;

namespace Flux.Data;

internal sealed class OuraConnection
{
    public bool Enabled { get; set; }
    public OuraRecoverySnapshot? Snapshot { get; set; }
    public List<OuraDecisionAudit> Decisions { get; set; } = [];
}

internal sealed record OuraDecisionAudit(long EvaluatedAtUnixMilliseconds,
    bool CadenceDue, bool LightRequired, OuraRecoveryAssessment Assessment);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(OuraConnection))]
[JsonSerializable(typeof(OuraRecoverySnapshot))]
internal partial class OuraJsonContext : JsonSerializerContext;

internal sealed class OuraRecoveryStore
{
    private readonly string _path;
    internal OuraRecoveryStore(Android.Content.Context context) =>
        _path = Path.Combine(context.NoBackupFilesDir!.AbsolutePath, "oura-recovery.json");

    internal OuraConnection Load()
    {
        try
        {
            OuraConnection result = File.Exists(_path) && new FileInfo(_path).Length <= 1_000_000
                ? JsonSerializer.Deserialize(File.ReadAllText(_path), OuraJsonContext.Default.OuraConnection) ?? new()
                : new();
            result.Decisions ??= [];
            return result;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }

    internal void Save(OuraConnection connection)
    {
        connection.Decisions = connection.Decisions.TakeLast(120).ToList();
        string temporary = _path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(connection, OuraJsonContext.Default.OuraConnection));
        File.Move(temporary, _path, true);
    }

    internal void Disconnect()
    {
        // Both targets are this store's exact private files, never workout data.
        File.Delete(_path);
        File.Delete(_path + ".tmp");
    }
}
