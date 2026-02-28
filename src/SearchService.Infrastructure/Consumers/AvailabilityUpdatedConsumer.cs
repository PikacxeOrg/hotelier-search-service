using MassTransit;

using MongoDB.Driver;

using Hotelier.Events;

using SearchService.Domain;

namespace SearchService.Infrastructure;

public class AvailabilityUpdatedConsumer(IMongoDatabase db, ILogger<AvailabilityUpdatedConsumer> logger)
    : IConsumer<AvailabilityUpdated>
{
    public async Task Consume(ConsumeContext<AvailabilityUpdated> context)
    {
        var msg = context.Message;
        logger.LogInformation(
            "Updating availability for accommodation {AccommodationId}, window {From}-{To}",
            msg.AccommodationId, msg.FromDate, msg.ToDate);

        var collection = db.GetCollection<AccommodationIndex>("accommodations");

        var filter = Builders<AccommodationIndex>.Filter
            .Eq(a => a.AccommodationId, msg.AccommodationId);

        // Remove any existing window that overlaps, then add the new one
        var pullFilter = Builders<AccommodationIndex>.Update.PullFilter(
            a => a.AvailabilityWindows,
            w => w.FromDate == msg.FromDate && w.ToDate == msg.ToDate);

        await collection.UpdateOneAsync(filter, pullFilter);

        var newWindow = new AvailabilityWindow
        {
            FromDate = msg.FromDate,
            ToDate = msg.ToDate,
            Price = msg.Price,
            PriceType = msg.PriceType,
            IsAvailable = msg.IsAvailable
        };

        var push = Builders<AccommodationIndex>.Update
            .Push(a => a.AvailabilityWindows, newWindow)
            .Set(a => a.UpdatedAt, DateTime.UtcNow);

        await collection.UpdateOneAsync(filter, push);
    }
}
