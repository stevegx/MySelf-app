namespace MySelf.Domain.Exercises;

/// <summary>Join row: which equipment an exercise uses.</summary>
public class ExerciseEquipment
{
    public Guid ExerciseId { get; set; }
    public Exercise Exercise { get; set; } = null!;

    public int EquipmentId { get; set; }
    public Equipment Equipment { get; set; } = null!;
}
