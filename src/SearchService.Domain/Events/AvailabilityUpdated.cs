namespace Hotelier.Events;

/// <summary>
/// Consumer-side DTO for AvailabilityUpdated.
/// </summary>
public record AvailabilityUpdated
{
    public Guid AvailabilityId { get; init; }
    public Guid AccommodationId { get; init; }
    public DateOnly FromDate { get; init; }
    public DateOnly ToDate { get; init; }
    public decimal Price { get; init; }
    public string PriceType { get; init; } = string.Empty;
    public bool IsAvailable { get; init; }
}
