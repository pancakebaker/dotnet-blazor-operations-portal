using System.Text.Json.Serialization;

namespace AuctionOperationsPortal.Tests.ContractCodegen;

// Handwritten evaluation helper; payload DTOs are emitted by NJsonSchema.
public sealed record GeneratedEventEnvelope<TPayload>(
    [property: JsonPropertyName("eventId")] Guid EventId,
    [property: JsonPropertyName("eventType")] string EventType,
    [property: JsonPropertyName("occurredAtUtc")] DateTimeOffset OccurredAtUtc,
    [property: JsonPropertyName("aggregateType")] string AggregateType,
    [property: JsonPropertyName("aggregateId")] Guid AggregateId,
    [property: JsonPropertyName("aggregateVersion")] long AggregateVersion,
    [property: JsonPropertyName("correlationId")] string? CorrelationId,
    [property: JsonPropertyName("tenantId")] Guid TenantId,
    [property: JsonPropertyName("payload")] TPayload Payload);
