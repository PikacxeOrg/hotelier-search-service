using MassTransit;

using MongoDB.Driver;

using Hotelier.Events;

using SearchService.Domain;

namespace SearchService.Infrastructure;

public class AccommodationRatedConsumer(IMongoDatabase db, ILogger<AccommodationRatedConsumer> logger)
    : IConsumer<AccommodationRated>
{
    public async Task Consume(ConsumeContext<AccommodationRated> context)
    {
        var msg = context.Message;
        logger.LogInformation(
            "Updating rating for accommodation {AccommodationId} – new score {Score}",
            msg.AccommodationId, msg.Score);

        var collection = db.GetCollection<AccommodationIndex>("accommodations");

        var filter = Builders<AccommodationIndex>.Filter
            .Eq(a => a.AccommodationId, msg.AccommodationId);

        var doc = await collection.Find(filter).FirstOrDefaultAsync();
        if (doc is null) return;

        // Recalculate running average
        var newTotal = doc.TotalRatings + 1;
        var newAvg = ((doc.AverageRating * doc.TotalRatings) + msg.Score) / newTotal;

        var update = Builders<AccommodationIndex>.Update
            .Set(a => a.AverageRating, newAvg)
            .Set(a => a.TotalRatings, newTotal)
            .Set(a => a.UpdatedAt, DateTime.UtcNow);

        await collection.UpdateOneAsync(filter, update);
    }
}
