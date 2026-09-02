namespace MySelf.Domain.Workouts;

/// <summary>
/// A named bucket of workout variants inside a program (docs/02). Examples: "Push", "Pull",
/// "Legs". <see cref="SortOrder"/> is the user's chosen order within the program.
/// </summary>
public class WorkoutGroup
{
    public Guid Id { get; set; }

    public Guid ProgramId { get; set; }
    public WorkoutProgram Program { get; set; } = null!;

    public required string Name { get; set; }
    public int SortOrder { get; set; }

    public List<WorkoutVariant> Variants { get; set; } = [];
}
