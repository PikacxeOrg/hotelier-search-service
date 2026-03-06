namespace SearchService.Api;

public class SearchResponse
{
    public Guid AccommodationId { get; set; }

    public Guid HostId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public List<string> Amenities { get; set; } = [];

    public List<string> Pictures { get; set; } = [];

    public int MinGuests { get; set; }

    public int MaxGuests { get; set; }

    public bool AutoApproval { get; set; }

    /// <summary>Cheapest available price in the queried date range.</summary>
    public decimal? UnitPrice { get; set; }

    /// <summary>Total price for the queried date range and guest count.</summary>
    public decimal? TotalPrice { get; set; }

    public double AverageRating { get; set; }

    public int TotalRatings { get; set; }
}
