namespace MySelf.Domain.Exercises;

/// <summary>A muscle an exercise targets. Seeded from wger; Id == wger id.</summary>
public class Muscle
{
    public int Id { get; set; }

    /// <summary>Common name (wger <c>name_en</c>), falling back to the Latin name when wger has none.</summary>
    public required string Name { get; set; }

    /// <summary>Latin name (wger <c>name</c>), e.g. "Biceps brachii".</summary>
    public required string LatinName { get; set; }

    public bool IsFront { get; set; }

    public ICollection<ExerciseMuscle> ExerciseMuscles { get; set; } = [];
}
