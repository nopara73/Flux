using System.Text.Json.Serialization;
using Flux.Models;

namespace Flux.Data;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(WorkoutState))]
internal partial class WorkoutJsonContext : JsonSerializerContext;
