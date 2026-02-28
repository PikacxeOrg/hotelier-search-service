using MassTransit;

using MongoDB.Driver;

using Hotelier.Events;

using SearchService.Domain;

namespace SearchService.Infrastructure;

public class AccommodationUpdatedConsumer(IMongoDatabase db, ILogger<AccommodationUpdatedConsumer> logger)
    : IConsumer<AccommodationUpdated>
{
    public async Task Consume(ConsumeContext<AccommodationUpdated> context)
    {
        var msg = context.Message;
        logger.LogInformation("Updating search index for accommodation {Id}", msg.AccommodationId);

        var filter = Builders<AccommodationIndex>.Filter
            .Eq(a => a.AccommodationId, msg.AccommodationId);

        var update = Builders<AccommodationIndex>.Update
            .Set(a => a.Name, msg.Name)
            .Set(a => a.Location, msg.Location)
            .Set(a => a.Amenities, msg.Amenities)
            .Set(a => a.Pictures, msg.Pictures)
            .Set(a => a.MinGuests, msg.MinGuests)
            .Set(a => a.MaxGuests, msg.MaxGuests)
            .Set(a => a.AutoApproval, msg.AutoApproval)
            .Set(a => a.UpdatedAt, DateTime.UtcNow);

        await db.GetCollection<AccommodationIndex>("accommodations")
            .UpdateOneAsync(filter, update);
    }
}
