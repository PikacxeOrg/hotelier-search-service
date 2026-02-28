namespace SearchService.Domain;

public record AvailabilityUpdated(
    Guid AccommodationId,
    Guid AvailabilityId,
    DateTime FromDate,
    DateTime ToDate,
    decimal Price,
    string PriceType,
    bool IsAvailable);
