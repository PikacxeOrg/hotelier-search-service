namespace SearchService.Api;

public class SearchRequest
{
    /// <summary>Location filter (partial match).</summary>
    public string? Location { get; set; }

    public int? NumberOfGuests { get; set; }

    public DateOnly? CheckIn { get; set; }

    public DateOnly? CheckOut { get; set; }

    /// <summary>Minimum average rating filter.</summary>
    public double? MinRating { get; set; }

    /// <summary>Minimum price filter.</summary>
    public decimal? MinPrice { get; set; }

    /// <summary>Maximum price filter.</summary>
    public decimal? MaxPrice { get; set; }

    /// <summary>Filter by specific amenities.</summary>
    public List<string>? Amenities { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}
