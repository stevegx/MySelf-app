namespace MySelf.Api.Nutrition;

/// <summary>Multi-select operations on a day's logged items (docs/08 Story 7, #256–261).
/// All four return the rebuilt day named in the route so the client can re-render in one round trip.</summary>
public sealed record BulkDeleteItemsRequest(IReadOnlyList<Guid> Ids);

public sealed record BulkMoveItemsRequest(IReadOnlyList<Guid> Ids, string ToCategory);

/// <summary><paramref name="ToDate"/> null = copy within the same day (duplicate into another slot).</summary>
public sealed record BulkCopyItemsRequest(IReadOnlyList<Guid> Ids, string ToCategory, string? ToDate);

/// <summary>Re-insert exact item snapshots — the undo primitive for <c>bulk-delete</c>.
/// Every nutrient figure is taken verbatim so an undo restores the item byte-for-byte.</summary>
public sealed record RestoreMealItemInput(
    string Category,
    string Name,
    string ServingBasis,
    decimal? ServingSizeGrams,
    decimal Amount,
    string Unit,
    decimal Kcal,
    decimal ProteinG,
    decimal CarbG,
    decimal FatG,
    decimal BasisKcal,
    decimal BasisProteinG,
    decimal BasisCarbG,
    decimal BasisFatG);

public sealed record BulkAddItemsRequest(IReadOnlyList<RestoreMealItemInput> Items);
