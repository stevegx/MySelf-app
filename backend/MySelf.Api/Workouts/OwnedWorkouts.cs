using Microsoft.EntityFrameworkCore;
using MySelf.Domain.Workouts;
using MySelf.Infrastructure.Persistence;

namespace MySelf.Api.Workouts;

/// <summary>
/// The one place the "this row belongs to the caller" predicate lives for each builder
/// entity. Every handler starts from one of these instead of re-writing the
/// <c>.Where(x =&gt; x....UserId == userId)</c> filter by hand — a single forgotten filter on
/// a future endpoint would be an IDOR hole (docs/05 owner-based authorization).
///
/// These return <see cref="IQueryable{T}"/>, so a caller still chains <c>Include</c> /
/// <c>AsNoTracking</c> / <c>Select</c> / <c>FirstOrDefaultAsync(x =&gt; x.Id == id)</c> as
/// needed; a miss (wrong owner or unknown id) simply yields no row and the handler returns
/// 404.
/// </summary>
internal static class OwnedWorkouts
{
    public static IQueryable<WorkoutProgram> OwnedPrograms(this MySelfDbContext db, Guid userId) =>
        db.WorkoutPrograms.Where(p => p.UserId == userId);

    public static IQueryable<WorkoutDay> OwnedDays(this MySelfDbContext db, Guid userId) =>
        db.WorkoutDays.Where(d => d.Program.UserId == userId);

    public static IQueryable<WorkoutSession> OwnedSessions(this MySelfDbContext db, Guid userId) =>
        db.WorkoutSessions.Where(s => s.UserId == userId);
}
