using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SearchService.Domain;

/// <summary>
/// Denormalized document combining accommodation, availability and rating data
/// for fast search queries.
/// </summary>
public class AccommodationIndex
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid AccommodationId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid HostId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public List<string> Amenities { get; set; } = [];

    public List<string> Pictures { get; set; } = [];

    public int MinGuests { get; set; }

    public int MaxGuests { get; set; }

    public bool AutoApproval { get; set; }

    // ----- Availability windows -----
    public List<AvailabilityWindow> AvailabilityWindows { get; set; } = [];

    // ----- Rating snapshot -----
    public double AverageRating { get; set; }

    public int TotalRatings { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class AvailabilityWindow
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    public decimal Price { get; set; }

    /// <summary>PerGuest or PerUnit</summary>
    public string PriceType { get; set; } = "PerUnit";

    public bool IsAvailable { get; set; } = true;
}
