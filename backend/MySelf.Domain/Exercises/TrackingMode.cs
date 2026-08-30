namespace MySelf.Domain.Exercises;

/// <summary>
/// How a set for an exercise is measured. MVP set (docs/08 #32).
/// Distance + Duration is a V1 addition and is intentionally absent here.
/// </summary>
public enum TrackingMode
{
    WeightAndReps,
    BodyweightReps,
    BodyweightPlusWeight,
    AssistanceReps,
    RepsOnly,
    Duration,
}
