using System.Text.Json.Serialization;

namespace Flux.Models;

// Private nightly summaries, never Oura's composite scores. Stored separately
// from backed-up workout preferences; timestamps retain the original evidence age.
public sealed class OuraRecoverySnapshot
{
    public int Version { get; set; } = 1;
    public string Source { get; set; } = "com.ouraring.oura";
    public long FetchedAtUnixMilliseconds { get; set; }
    public List<OuraRecoveryNight> Nights { get; set; } = [];
}

public sealed class OuraRecoveryNight
{
    public int WakeDay { get; set; }
    public long SleepEndUnixMilliseconds { get; set; }
    public double MainSleepMinutes { get; set; }
    public double SleepMinutes { get; set; }
    public bool DurationReliable { get; set; }
    public double HeartRate { get; set; }
    public double Hrv { get; set; }
    public double HeartRateCoverage { get; set; }
    public double HrvCoverage { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter<OuraRecoveryVerdict>))]
public enum OuraRecoveryVerdict { Unknown, Light, Regular }

public sealed record OuraRecoveryAssessment(
    OuraRecoveryVerdict Verdict,
    string Reason,
    long SleepEndUnixMilliseconds = 0,
    int RecentNights = 0,
    int BaselineNights = 0,
    int Warnings = 0,
    double? RecentHeartRate = null,
    double? BaselineHeartRate = null,
    double? RecentLogHrv = null,
    double? BaselineLogHrv = null,
    double? RecentSleepMinutes = null);
