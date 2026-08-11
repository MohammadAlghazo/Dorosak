using System.Text.Json;
using Dorosak.Application.Features.Communications;

namespace Dorosak.Application.UnitTests.Communications;

public sealed class CommunicationsRealtimeContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void DurableEventPayloadsContainMetadataOnly()
    {
        Guid resourceId = Guid.CreateVersion7();
        Guid userId = Guid.CreateVersion7();
        Guid courseId = Guid.CreateVersion7();
        string[] events =
        [
            Serialize(
                CommunicationsRealtimeEvents.ConversationCreated,
                new ConversationCreatedRealtimePayload(resourceId, userId, courseId)),
            Serialize(
                CommunicationsRealtimeEvents.MessageCreated,
                new MessageCreatedRealtimePayload(resourceId, Guid.CreateVersion7(), userId, 7)),
            Serialize(
                CommunicationsRealtimeEvents.ConversationLeft,
                new ConversationLeftRealtimePayload(resourceId, userId)),
            Serialize(
                CommunicationsRealtimeEvents.AnnouncementCreated,
                new AnnouncementCreatedRealtimePayload(resourceId, courseId, userId, 1, 25)),
            Serialize(
                CommunicationsRealtimeEvents.AnnouncementUpdated,
                new AnnouncementUpdatedRealtimePayload(resourceId, courseId, userId, 2, 25)),
            Serialize(
                CommunicationsRealtimeEvents.AnnouncementDeleted,
                new AnnouncementDeletedRealtimePayload(resourceId, courseId, userId, 3)),
        ];

        Assert.Equal(6, events.Length);
        Assert.All(events, payload =>
        {
            Assert.DoesNotContain("\"body\"", payload, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"title\"", payload, StringComparison.OrdinalIgnoreCase);
            using JsonDocument document = JsonDocument.Parse(payload);
            Assert.Equal(
                CommunicationsRealtimeEvents.SchemaVersion,
                document.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.NotEqual(Guid.Empty, document.RootElement.GetProperty("eventId").GetGuid());
            Assert.Equal(JsonValueKind.Object, document.RootElement.GetProperty("payload").ValueKind);
        });
    }

    private static string Serialize<TPayload>(string eventType, TPayload payload)
        where TPayload : class =>
        JsonSerializer.Serialize(
            new CommunicationsRealtimeEnvelope<TPayload>(
                Guid.CreateVersion7(),
                eventType,
                CommunicationsRealtimeEvents.SchemaVersion,
                DateTimeOffset.UtcNow,
                payload),
            JsonOptions);
}
