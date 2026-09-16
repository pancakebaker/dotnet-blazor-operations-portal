// <copyright file="IntegrationEventEnvelope.cs" company="Distributed Bidding Auction Platform">
// Copyright (c) Distributed Bidding Auction Platform. Licensed under the MIT license.
// </copyright>
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuctionOperationsPortal.Contracts;

/// <summary>Represents the validated envelope received from the auction event stream.</summary>
[method: JsonConstructor]
public sealed partial record IntegrationEventEnvelope(
    Guid EventId,
    string EventType,
    DateTimeOffset OccurredAtUtc,
    string AggregateType,
    Guid AggregateId,
    long AggregateVersion,
    string? CorrelationId,
    Guid TenantId,
    JsonElement Payload);

// The short constructor is retained for existing in-process test fixtures. Wire messages must
// use the primary constructor and carry tenantId explicitly.
public sealed partial record IntegrationEventEnvelope
{
    private static readonly Guid LegacyFixtureTenantId =
        Guid.Parse("aaaaaaaa-1111-4111-8111-111111111111");

    /// <summary>Initializes a new instance of the <see cref="IntegrationEventEnvelope"/> class using the local demo tenant.</summary>
    public IntegrationEventEnvelope(
        Guid eventId,
        string eventType,
        DateTimeOffset occurredAtUtc,
        string aggregateType,
        Guid aggregateId,
        long aggregateVersion,
        string? correlationId,
        JsonElement payload)
        : this(
            eventId,
            eventType,
            occurredAtUtc,
            aggregateType,
            aggregateId,
            aggregateVersion,
            correlationId,
            LegacyFixtureTenantId,
            payload)
    {
    }
}
