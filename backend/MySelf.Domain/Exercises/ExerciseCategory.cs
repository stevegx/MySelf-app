namespace MySelf.Domain.Exercises;

/// <summary>Broad grouping of an exercise (Abs, Arms, Back, …). Seeded from wger; Id == wger id.</summary>
public class ExerciseCategory
{
    public int Id { get; set; }
    public required string Name { get; set; }

    public ICollection<Exercise> Exercises { get; set; } = [];
}
