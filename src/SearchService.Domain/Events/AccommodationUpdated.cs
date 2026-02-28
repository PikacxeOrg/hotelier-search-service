namespace SearchService.Domain;

public record AccommodationUpdated(
    Guid AccommodationId,
    Guid HostId,
    string Name,
    string Location,
    List<string> Amenities,
    List<string> Pictures,
    int MinGuests,
    int MaxGuests,
    bool AutoApproval);
