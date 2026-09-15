using System.Text.Json;
using System.Text.Json.Nodes;
using AuctionOperationsPortal.Contracts;
using Dbap.Operations.ContractCodegen.Generated;

namespace AuctionOperationsPortal.Tests.ContractCodegen;
public sealed class GeneratedContractDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public static IEnumerable<object[]> CanonicalFixtures() =>
        [["auction-bid-accepted.json", typeof(GeneratedBidAcceptedPayload), "BidAccepted"], ["auction-closed.json", typeof(GeneratedAuctionClosedPayload), "AuctionClosed"], ["winner-selected.json", typeof(GeneratedWinnerSelectedPayload), "WinnerSelected"]];
    [Theory]
    [MemberData(nameof(CanonicalFixtures))]
    public void CanonicalFixtureDeserializesWithSystemTextJson(string fileName, Type payloadType, string eventType)
    {
        var json = File.ReadAllText(FixturePath(fileName));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var payload = JsonSerializer.Deserialize(root.GetProperty("payload").GetRawText(), payloadType, JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal(eventType, root.GetProperty("eventType").GetString());
        Assert.Equal(root.GetProperty("aggregateId").GetGuid(), root.GetProperty("payload").GetProperty("auctionId").GetGuid());
        Assert.Equal(root.GetProperty("aggregateVersion").GetInt64(), root.GetProperty("payload").GetProperty("auctionVersion").GetInt64());
        Assert.Equal(root.GetProperty("tenantId").GetGuid(), root.GetProperty("payload").GetProperty("tenantId").GetGuid());
    }
    [Fact]
    public void GeneratedDtoCanAdaptToExistingOperationsEnvelope()
    {
        var generated = JsonSerializer.Deserialize<GeneratedEventEnvelope<GeneratedBidAcceptedPayload>>(File.ReadAllText(FixturePath("auction-bid-accepted.json")), JsonOptions);
        Assert.NotNull(generated);
        var existing = new IntegrationEventEnvelope(generated.EventId, generated.EventType, generated.OccurredAtUtc, generated.AggregateType, generated.AggregateId, generated.AggregateVersion, generated.CorrelationId, generated.TenantId, JsonSerializer.SerializeToElement(generated.Payload, JsonOptions));
        Assert.Equal(generated.AggregateId, existing.AggregateId);
        Assert.Equal(generated.AggregateVersion, existing.AggregateVersion);
        Assert.Equal(generated.Payload.Amount, existing.Payload.GetProperty("amount").GetDecimal());
    }

    [Fact]
    public void GeneratedDtoIgnoresCompatibleAdditionalField()
    {
        var json = File.ReadAllText(FixturePath("auction-bid-accepted.json"));
        using var document = JsonDocument.Parse(json);
        var node = JsonNode.Parse(document.RootElement.GetRawText())!.AsObject();
        node["futureOptionalField"] = "preserved-by-wire-policy";

        var generated = JsonSerializer.Deserialize<GeneratedEventEnvelope<GeneratedBidAcceptedPayload>>(node.ToJsonString(), JsonOptions);

        Assert.NotNull(generated);
        Assert.Equal("preserved-by-wire-policy", node["futureOptionalField"]!.GetValue<string>());
    }

    [Fact]
    public void GeneratedDtoRequiresSchemaValidationForMissingFields()
    {
        var json = File.ReadAllText(FixturePath("auction-bid-accepted.json"));
        using var document = JsonDocument.Parse(json);
        var node = JsonNode.Parse(document.RootElement.GetProperty("payload").GetRawText())!.AsObject();
        node.Remove("amount");

        var generated = JsonSerializer.Deserialize<GeneratedBidAcceptedPayload>(node.ToJsonString(), JsonOptions);

        Assert.NotNull(generated);
        Assert.Equal(0m, generated.Amount);
    }

    [Fact]
    public void GeneratedDtoRequiresSchemaValidationForNumericString()
    {
        var json = File.ReadAllText(FixturePath("auction-bid-accepted.json"));
        using var document = JsonDocument.Parse(json);
        var node = JsonNode.Parse(document.RootElement.GetProperty("payload").GetRawText())!.AsObject();
        node["amount"] = "125.50";

        var generated = JsonSerializer.Deserialize<GeneratedBidAcceptedPayload>(node.ToJsonString(), JsonOptions);

        Assert.NotNull(generated);
        Assert.Equal(125.50m, generated.Amount);
    }
    private static string FixturePath(string fileName) => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "contracts", "fixtures", "v1", fileName));
}
