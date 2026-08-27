using System.Text.Json.Serialization;
using Flux.Models;

namespace Flux.Data;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(WorkoutState))]
[JsonSerializable(typeof(WorkoutSessionLog))]
[JsonSerializable(typeof(LegacyWorkoutState))]
internal partial class WorkoutJsonContext : JsonSerializerContext;
