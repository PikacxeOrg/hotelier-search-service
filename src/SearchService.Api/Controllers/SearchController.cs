using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using MongoDB.Driver;

using SearchService.Domain;

namespace SearchService.Api;

[ApiController]
[Route("api/[controller]")]
public class SearchController(
    IMongoDatabase mongoDb,
    ILogger<SearchController> logger) : ControllerBase
{
    private IMongoCollection<AccommodationIndex> Collection
        => mongoDb.GetCollection<AccommodationIndex>("accommodations");

    // -------------------------------------------------------
    // GET /api/search   (1.7 – search & filter accommodations)
    // -------------------------------------------------------
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] SearchRequest request)
    {
        var filterBuilder = Builders<AccommodationIndex>.Filter;
        var filters = new List<FilterDefinition<AccommodationIndex>>();

        // Location (case-insensitive partial match)
        if (!string.IsNullOrWhiteSpace(request.Location))
        {
            filters.Add(filterBuilder.Regex(
                a => a.Location,
                new MongoDB.Bson.BsonRegularExpression(request.Location, "i")));
        }

        // Guest count
        if (request.NumberOfGuests.HasValue)
        {
            filters.Add(filterBuilder.Lte(a => a.MinGuests, request.NumberOfGuests.Value));
            filters.Add(filterBuilder.Gte(a => a.MaxGuests, request.NumberOfGuests.Value));
        }

        // Minimum rating
        if (request.MinRating.HasValue)
        {
            filters.Add(filterBuilder.Gte(a => a.AverageRating, request.MinRating.Value));
        }

        // Amenities (must have all specified)
        if (request.Amenities is { Count: > 0 })
        {
            filters.Add(filterBuilder.All(a => a.Amenities, request.Amenities));
        }

        // Date/price availability filtering is done in-memory after fetch
        // because AvailabilityWindows is a nested array with complex overlap logic.
        // For production scale, this should use aggregation pipeline.
        var filter = filters.Count > 0
            ? filterBuilder.And(filters)
            : filterBuilder.Empty;

        // Get candidate documents from MongoDB
        var options = new FindOptions<AccommodationIndex>
        {
            Sort = Builders<AccommodationIndex>.Sort.Descending(a => a.AverageRating)
        };

        using var cursor = await Collection.FindAsync(filter, options);
        var candidates = await cursor.ToListAsync();

        // Apply in-memory date/price filtering
        var results = candidates.Select(a =>
        {
            decimal? unitPrice = null;
            decimal? totalPrice = null;

            if (request.CheckIn.HasValue && request.CheckOut.HasValue)
            {
                // Find matching availability window
                var window = a.AvailabilityWindows.FirstOrDefault(w =>
                    w.IsAvailable
                    && w.FromDate <= request.CheckIn.Value
                    && w.ToDate >= request.CheckOut.Value);

                if (window is null)
                    return null; // Not available for these dates

                unitPrice = window.Price;
                var nights = (int)(request.CheckOut.Value.Date - request.CheckIn.Value.Date).TotalDays;
                if (nights < 1) nights = 1;

                totalPrice = window.PriceType == "PerGuest" && request.NumberOfGuests.HasValue
                    ? window.Price * nights * request.NumberOfGuests.Value
                    : window.Price * nights;
            }

            // Price filter
            if (request.MinPrice.HasValue && unitPrice.HasValue && unitPrice < request.MinPrice)
                return null;
            if (request.MaxPrice.HasValue && unitPrice.HasValue && unitPrice > request.MaxPrice)
                return null;

            return new SearchResponse
            {
                AccommodationId = a.AccommodationId,
                HostId = a.HostId,
                Name = a.Name,
                Location = a.Location,
                Amenities = a.Amenities,
                Pictures = a.Pictures,
                MinGuests = a.MinGuests,
                MaxGuests = a.MaxGuests,
                AutoApproval = a.AutoApproval,
                UnitPrice = unitPrice,
                TotalPrice = totalPrice,
                AverageRating = a.AverageRating,
                TotalRatings = a.TotalRatings
            };
        })
        .Where(r => r is not null)
        .Cast<SearchResponse>()
        .ToList();

        // Pagination
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var totalCount = results.Count;
        var paged = results.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        logger.LogInformation(
            "Search returned {Total} results (page {Page}/{TotalPages})",
            totalCount, page, (int)Math.Ceiling((double)totalCount / pageSize));

        return Ok(new SearchPagedResponse
        {
            Items = paged,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    // -------------------------------------------------------
    // GET /api/search/{accommodationId}   (get single indexed doc)
    // -------------------------------------------------------
    [AllowAnonymous]
    [HttpGet("{accommodationId:guid}")]
    public async Task<IActionResult> GetById(Guid accommodationId)
    {
        var filter = Builders<AccommodationIndex>.Filter.Eq(a => a.AccommodationId, accommodationId);
        using var cursor = await Collection.FindAsync(filter);
        var doc = await cursor.FirstOrDefaultAsync();

        if (doc is null) return NotFound();

        return Ok(new SearchResponse
        {
            AccommodationId = doc.AccommodationId,
            HostId = doc.HostId,
            Name = doc.Name,
            Location = doc.Location,
            Amenities = doc.Amenities,
            Pictures = doc.Pictures,
            MinGuests = doc.MinGuests,
            MaxGuests = doc.MaxGuests,
            AutoApproval = doc.AutoApproval,
            AverageRating = doc.AverageRating,
            TotalRatings = doc.TotalRatings
        });
    }
}
