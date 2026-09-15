using System.Globalization;
using System.Text.Json;
using AuctionOperationsPortal.Contracts;
using AuctionOperationsPortal.Persistence;

namespace AuctionOperationsPortal.Tests;

public sealed class IntegrationContractFixtureTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEnumerable<object[]> Fixtures() =>
        [
            ["auction-bid-accepted.json", typeof(BidAcceptedPayload)],
            ["auction-closed.json", typeof(AuctionClosedPayload)],
            ["winner-selected.json", typeof(WinnerSelectedPayload)]
        ];

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void V1FixtureDeserializesAndRoundTripsStructurally(string fileName, Type payloadType)
    {
        var fixture = File.ReadAllText(FixturePath(fileName));
        var envelope = JsonSerializer.Deserialize<IntegrationEventEnvelope>(fixture, JsonOptions);

        Assert.NotNull(envelope);
        Assert.Equal(envelope.AggregateId, envelope.Payload.GetProperty("auctionId").GetGuid());
        Assert.Equal(envelope.AggregateVersion, envelope.Payload.GetProperty("auctionVersion").GetInt64());
        Assert.Equal(envelope.TenantId, envelope.Payload.GetProperty("tenantId").GetGuid());
        Assert.Equal(envelope.EventType, ExpectedEventType(fileName));

        var activity = IntegrationEventMapper.ToActivity(
            envelope,
            DateTimeOffset.Parse("2026-01-01T00:10:00Z", CultureInfo.InvariantCulture));
        Assert.Equal(envelope.EventId, activity.EventId);
        Assert.Equal(envelope.TenantId, activity.TenantId);
        Assert.Equal(envelope.AggregateVersion, activity.AggregateVersion);
        Assert.Equal(envelope.AggregateId, activity.AggregateId);

        var payload = JsonSerializer.Deserialize(envelope.Payload.GetRawText(), payloadType, JsonOptions);
        Assert.NotNull(payload);

        var roundTrippedPayload = JsonSerializer.SerializeToElement(payload, payloadType, JsonOptions);
        var equivalent = envelope with { Payload = roundTrippedPayload };
        var roundTripped = JsonSerializer.SerializeToElement(equivalent, JsonOptions);

        AssertCompatible(JsonDocument.Parse(fixture).RootElement, roundTripped, fileName);
    }

    private static string FixturePath(string fileName) =>
        Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..",
                "tests", "contracts", "fixtures", "v1", fileName));

    private static string ExpectedEventType(string fileName) => fileName switch
    {
        "auction-bid-accepted.json" => "BidAccepted",
        "auction-closed.json" => "AuctionClosed",
        "winner-selected.json" => "WinnerSelected",
        _ => throw new ArgumentOutOfRangeException(nameof(fileName), fileName, null)
    };

    private static void AssertCompatible(JsonElement expected, JsonElement actual, string path)
    {
        if (expected.ValueKind == JsonValueKind.Object)
        {
            var expectedProperties = expected.EnumerateObject().ToDictionary(property => property.Name);
            var actualProperties = actual.EnumerateObject().ToDictionary(property => property.Name);
            Assert.Equal(expectedProperties.Keys.Order(), actualProperties.Keys.Order());
            foreach (var property in expectedProperties)
            {
                AssertCompatible(property.Value.Value, actualProperties[property.Key].Value, $"{path}.{property.Key}");
            }

            return;
        }

        if (expected.ValueKind == JsonValueKind.String
            && path.EndsWith("AtUtc", StringComparison.Ordinal))
        {
            Assert.Equal(expected.GetDateTimeOffset(), actual.GetDateTimeOffset());
            return;
        }

        if (expected.ValueKind == JsonValueKind.Number)
        {
            Assert.Equal(expected.GetDecimal(), actual.GetDecimal());
            return;
        }

        Assert.True(JsonElement.DeepEquals(expected, actual), $"Unexpected value at {path}.");
    }
}
