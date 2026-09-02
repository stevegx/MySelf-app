namespace MySelf.Domain.Identity;

/// <summary>
/// Which units the user enters and reads values in. Canonical storage is always metric
/// (docs/06 §15: "metric and imperial entry, canonical storage in metric") — this flag only
/// drives display formatting and input parsing on the client.
/// </summary>
public enum UnitSystem
{
    Metric,
    Imperial,
}
