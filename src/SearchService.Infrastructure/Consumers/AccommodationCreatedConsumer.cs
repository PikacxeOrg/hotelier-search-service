using MassTransit;

using MongoDB.Driver;

using Hotelier.Events;

using SearchService.Domain;

namespace SearchService.Infrastructure;

public class AccommodationCreatedConsumer(IMongoDatabase db, ILogger<AccommodationCreatedConsumer> logger)
    : IConsumer<AccommodationCreated>
{
    public async Task Consume(ConsumeContext<AccommodationCreated> context)
    {
        var msg = context.Message;
        logger.LogInformation("Indexing new accommodation {Id}", msg.AccommodationId);

        var doc = new AccommodationIndex
        {
            AccommodationId = msg.AccommodationId,
            HostId = msg.HostId,
            Name = msg.Name,
            Location = msg.Location,
            Amenities = msg.Amenities,
            Pictures = msg.Pictures,
            MinGuests = msg.MinGuests,
            MaxGuests = msg.MaxGuests,
            AutoApproval = msg.AutoApproval
        };

        await db.GetCollection<AccommodationIndex>("accommodations").InsertOneAsync(doc);
    }
}
