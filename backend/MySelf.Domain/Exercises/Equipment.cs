namespace MySelf.Domain.Exercises;

/// <summary>Equipment an exercise uses (Barbell, Dumbbell, none/bodyweight, …). Seeded from wger; Id == wger id.</summary>
public class Equipment
{
    public int Id { get; set; }
    public required string Name { get; set; }

    public ICollection<ExerciseEquipment> ExerciseEquipment { get; set; } = [];
}
