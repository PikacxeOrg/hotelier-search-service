using MassTransit;

using MongoDB.Driver;

using Hotelier.Events;

using SearchService.Domain;

namespace SearchService.Infrastructure;

public class AccommodationDeletedConsumer(IMongoDatabase db, ILogger<AccommodationDeletedConsumer> logger)
    : IConsumer<AccommodationDeleted>
{
    public async Task Consume(ConsumeContext<AccommodationDeleted> context)
    {
        var msg = context.Message;
        logger.LogInformation("Removing accommodation {Id} from search index", msg.AccommodationId);

        var filter = Builders<AccommodationIndex>.Filter
            .Eq(a => a.AccommodationId, msg.AccommodationId);

        await db.GetCollection<AccommodationIndex>("accommodations")
            .DeleteOneAsync(filter);
    }
}
