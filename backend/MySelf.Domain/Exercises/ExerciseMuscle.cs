namespace MySelf.Domain.Exercises;

public enum MuscleRole
{
    Primary,
    Secondary,
}

/// <summary>Join row: which muscles an exercise works, and in what role.</summary>
public class ExerciseMuscle
{
    public Guid ExerciseId { get; set; }
    public Exercise Exercise { get; set; } = null!;

    public int MuscleId { get; set; }
    public Muscle Muscle { get; set; } = null!;

    public MuscleRole Role { get; set; }
}
