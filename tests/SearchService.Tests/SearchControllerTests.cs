using FluentAssertions;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using MongoDB.Driver;

using Moq;

using SearchService.Api;
using SearchService.Domain;

namespace SearchService.Tests;

public class SearchControllerTests
{
    private readonly Mock<IMongoDatabase> _mockDb;
    private readonly Mock<IMongoCollection<AccommodationIndex>> _mockCollection;
    private readonly SearchController _sut;

    public SearchControllerTests()
    {
        _mockDb = new Mock<IMongoDatabase>();
        _mockCollection = new Mock<IMongoCollection<AccommodationIndex>>();

        _mockDb.Setup(db => db.GetCollection<AccommodationIndex>("accommodations", null))
            .Returns(_mockCollection.Object);

        var logger = new Mock<ILogger<SearchController>>();
        _sut = new SearchController(_mockDb.Object, logger.Object);
    }

    // ------ helpers ------

    private void SetupFind(List<AccommodationIndex> results)
    {
        var mockCursor = new Mock<IAsyncCursor<AccommodationIndex>>();
        mockCursor
            .SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        mockCursor.Setup(c => c.Current).Returns(results);

        _mockCollection
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<AccommodationIndex>>(),
                It.IsAny<FindOptions<AccommodationIndex, AccommodationIndex>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCursor.Object);

        // Also match FindOptions<AccommodationIndex> (no second type param)
        _mockCollection
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<AccommodationIndex>>(),
                It.IsAny<FindOptions<AccommodationIndex>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCursor.Object);
    }

    private static AccommodationIndex MakeAccommodation(
        string name = "Beach Hotel",
        string location = "Miami",
        int minGuests = 1,
        int maxGuests = 4,
        double avgRating = 4.5,
        int totalRatings = 10,
        List<string>? amenities = null,
        List<AvailabilityWindow>? windows = null)
    {
        return new AccommodationIndex
        {
            AccommodationId = Guid.NewGuid(),
            HostId = Guid.NewGuid(),
            Name = name,
            Location = location,
            MinGuests = minGuests,
            MaxGuests = maxGuests,
            AverageRating = avgRating,
            TotalRatings = totalRatings,
            Amenities = amenities ?? ["WiFi", "Pool"],
            Pictures = ["pic1.jpg"],
            AvailabilityWindows = windows ?? [],
            AutoApproval = true
        };
    }

    // =======================================================
    //  SEARCH
    // =======================================================

    [Fact]
    public async Task Search_NoFilters_ReturnsAll()
    {
        var docs = new List<AccommodationIndex>
        {
            MakeAccommodation("Hotel A"),
            MakeAccommodation("Hotel B")
        };
        SetupFind(docs);

        var result = await _sut.Search(new SearchRequest());

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<SearchPagedResponse>().Subject;
        body.TotalCount.Should().Be(2);
        body.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Search_WithDateFilter_FiltersUnavailable()
    {
        var available = MakeAccommodation("Available Hotel", windows:
        [
            new AvailabilityWindow
            {
                FromDate = new DateTime(2025, 7, 1),
                ToDate = new DateTime(2025, 7, 31),
                Price = 100m,
                PriceType = "PerUnit",
                IsAvailable = true
            }
        ]);
        var unavailable = MakeAccommodation("Unavailable Hotel"); // no windows

        SetupFind([available, unavailable]);

        var result = await _sut.Search(new SearchRequest
        {
            CheckIn = new DateTime(2025, 7, 5),
            CheckOut = new DateTime(2025, 7, 10)
        });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<SearchPagedResponse>().Subject;
        body.TotalCount.Should().Be(1);
        body.Items[0].Name.Should().Be("Available Hotel");
    }

    [Fact]
    public async Task Search_DateFilter_CalculatesPrice_PerUnit()
    {
        var hotel = MakeAccommodation(windows:
        [
            new AvailabilityWindow
            {
                FromDate = new DateTime(2025, 7, 1),
                ToDate = new DateTime(2025, 7, 31),
                Price = 100m,
                PriceType = "PerUnit",
                IsAvailable = true
            }
        ]);

        SetupFind([hotel]);

        var result = await _sut.Search(new SearchRequest
        {
            CheckIn = new DateTime(2025, 7, 5),
            CheckOut = new DateTime(2025, 7, 10),
            NumberOfGuests = 2
        });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<SearchPagedResponse>().Subject;
        body.Items[0].UnitPrice.Should().Be(100m);
        body.Items[0].TotalPrice.Should().Be(500m); // 5 nights × 100 (PerUnit ignores guests)
    }

    [Fact]
    public async Task Search_DateFilter_CalculatesPrice_PerGuest()
    {
        var hotel = MakeAccommodation(windows:
        [
            new AvailabilityWindow
            {
                FromDate = new DateTime(2025, 7, 1),
                ToDate = new DateTime(2025, 7, 31),
                Price = 50m,
                PriceType = "PerGuest",
                IsAvailable = true
            }
        ]);

        SetupFind([hotel]);

        var result = await _sut.Search(new SearchRequest
        {
            CheckIn = new DateTime(2025, 7, 5),
            CheckOut = new DateTime(2025, 7, 10),
            NumberOfGuests = 3
        });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<SearchPagedResponse>().Subject;
        body.Items[0].TotalPrice.Should().Be(750m); // 5 nights × 50 × 3 guests
    }

    [Fact]
    public async Task Search_PriceFilter_ExcludesOverMax()
    {
        var cheap = MakeAccommodation("Cheap", windows:
        [
            new AvailabilityWindow
            {
                FromDate = new DateTime(2025, 7, 1),
                ToDate = new DateTime(2025, 7, 31),
                Price = 50m,
                PriceType = "PerUnit",
                IsAvailable = true
            }
        ]);
        var expensive = MakeAccommodation("Expensive", windows:
        [
            new AvailabilityWindow
            {
                FromDate = new DateTime(2025, 7, 1),
                ToDate = new DateTime(2025, 7, 31),
                Price = 500m,
                PriceType = "PerUnit",
                IsAvailable = true
            }
        ]);

        SetupFind([cheap, expensive]);

        var result = await _sut.Search(new SearchRequest
        {
            CheckIn = new DateTime(2025, 7, 5),
            CheckOut = new DateTime(2025, 7, 10),
            MaxPrice = 100m
        });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<SearchPagedResponse>().Subject;
        body.TotalCount.Should().Be(1);
        body.Items[0].Name.Should().Be("Cheap");
    }

    [Fact]
    public async Task Search_Pagination_ReturnsCorrectPage()
    {
        var docs = Enumerable.Range(1, 5)
            .Select(i => MakeAccommodation($"Hotel {i}"))
            .ToList();

        SetupFind(docs);

        var result = await _sut.Search(new SearchRequest { Page = 2, PageSize = 2 });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<SearchPagedResponse>().Subject;
        body.TotalCount.Should().Be(5);
        body.Items.Should().HaveCount(2);
        body.Page.Should().Be(2);
        body.TotalPages.Should().Be(3);
    }

    [Fact]
    public async Task Search_Empty_ReturnsEmptyPage()
    {
        SetupFind([]);

        var result = await _sut.Search(new SearchRequest());

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<SearchPagedResponse>().Subject;
        body.TotalCount.Should().Be(0);
        body.Items.Should().BeEmpty();
    }

    // =======================================================
    //  GET by ID
    // =======================================================

    [Fact]
    public async Task GetById_Found_ReturnsOk()
    {
        var id = Guid.NewGuid();
        var doc = MakeAccommodation();
        doc.AccommodationId = id;

        var mockCursor = new Mock<IAsyncCursor<AccommodationIndex>>();
        mockCursor
            .SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        mockCursor.Setup(c => c.Current).Returns([doc]);

        _mockCollection
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<AccommodationIndex>>(),
                It.IsAny<FindOptions<AccommodationIndex, AccommodationIndex>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCursor.Object);

        _mockCollection
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<AccommodationIndex>>(),
                It.IsAny<FindOptions<AccommodationIndex>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCursor.Object);

        var result = await _sut.GetById(id);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<SearchResponse>().Subject;
        body.AccommodationId.Should().Be(id);
    }

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        var mockCursor = new Mock<IAsyncCursor<AccommodationIndex>>();
        mockCursor
            .SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        mockCursor.Setup(c => c.Current).Returns([]);

        _mockCollection
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<AccommodationIndex>>(),
                It.IsAny<FindOptions<AccommodationIndex, AccommodationIndex>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCursor.Object);

        _mockCollection
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<AccommodationIndex>>(),
                It.IsAny<FindOptions<AccommodationIndex>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCursor.Object);

        var result = await _sut.GetById(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }
}
