using System.Text.Json;
using System.Text.Json.Nodes;
using AuctionOperationsPortal.Contracts;
using Dbap.Operations.ContractCodegen.Generated;
using Json.Schema;

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
        var schemaFile = eventType switch
        {
            "BidAccepted" => "bid-accepted.schema.json",
            "AuctionClosed" => "auction-closed.schema.json",
            "WinnerSelected" => "winner-selected.schema.json",
            _ => throw new ArgumentOutOfRangeException(nameof(eventType))
        };
        var validation = EvaluateSchema(schemaFile, json);
        Assert.True(validation.IsValid, ValidationMessage(validation));
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

    [Fact]
    public void GeneratedDtoUsesApprovedClrTypes()
    {
        Assert.Equal(typeof(decimal), typeof(GeneratedBidAcceptedPayload).GetProperty(nameof(GeneratedBidAcceptedPayload.Amount))!.PropertyType);
        Assert.Equal(typeof(Guid), typeof(GeneratedBidAcceptedPayload).GetProperty(nameof(GeneratedBidAcceptedPayload.AuctionId))!.PropertyType);
        Assert.Equal(typeof(DateTimeOffset), typeof(GeneratedBidAcceptedPayload).GetProperty(nameof(GeneratedBidAcceptedPayload.OccurredAtUtc))!.PropertyType);
        Assert.Equal(typeof(decimal?), typeof(GeneratedAuctionClosedPayload).GetProperty(nameof(GeneratedAuctionClosedPayload.FinalBidAmount))!.PropertyType);
    }

    [Fact]
    public void CanonicalBidAcceptedFixturePassesDraft202012SchemaValidation()
    {
        var result = EvaluateSchema("bid-accepted.schema.json", File.ReadAllText(FixturePath("auction-bid-accepted.json")));
        Assert.True(result.IsValid, ValidationMessage(result));
    }

    [Fact]
    public void MissingRequiredAmountFailsSchemaValidationWithPath()
    {
        var node = JsonNode.Parse(File.ReadAllText(FixturePath("auction-bid-accepted.json")))!.AsObject();
        node["payload"]!.AsObject().Remove("amount");
        var result = EvaluateSchema("bid-accepted.schema.json", node);
        Assert.False(result.IsValid);
        Assert.Contains("payload", ValidationMessage(result));
    }

    [Fact]
    public void WrongUuidFailsSchemaValidation()
    {
        var node = JsonNode.Parse(File.ReadAllText(FixturePath("auction-bid-accepted.json")))!.AsObject();
        node["eventId"] = "not-a-uuid";
        var result = EvaluateSchema("bid-accepted.schema.json", node);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void NumericStringFailsSchemaValidationEvenThoughDtoCanCoerceIt()
    {
        var node = JsonNode.Parse(File.ReadAllText(FixturePath("auction-bid-accepted.json")))!.AsObject();
        node["payload"]!.AsObject()["amount"] = "125.50";
        var result = EvaluateSchema("bid-accepted.schema.json", node);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void WrongTimestampFailsSchemaValidation()
    {
        var node = JsonNode.Parse(File.ReadAllText(FixturePath("auction-bid-accepted.json")))!.AsObject();
        node["occurredAtUtc"] = "yesterday";
        var result = EvaluateSchema("bid-accepted.schema.json", node);
        Assert.False(result.IsValid);
    }

    private static EvaluationResults EvaluateSchema(string schemaFile, string json) =>
        EvaluateSchema(schemaFile, JsonNode.Parse(json)!);

    private static EvaluationResults EvaluateSchema(string schemaFile, JsonNode instance)
    {
        SchemaRegistry.Global.Fetch = _ => null!;
        foreach (var path in Directory.GetFiles(SchemaRoot(), "*.schema.json").OrderBy(path => path, StringComparer.Ordinal))
        {
            var schema = JsonSchema.FromFile(path);
            SchemaRegistry.Global.Register(schema);
        }

        var root = JsonSchema.FromFile(Path.Combine(SchemaRoot(), schemaFile));
        return root.Evaluate(instance, new EvaluationOptions
        {
            OutputFormat = OutputFormat.List,
            RequireFormatValidation = true
        });
    }

    private static string ValidationMessage(EvaluationResults result)
    {
        var messages = new List<string>();
        CollectValidationMessages(result, messages);
        return string.Join("; ", messages);
    }

    private static void CollectValidationMessages(EvaluationResults result, ICollection<string> messages)
    {
        if (result.HasErrors)
        {
            foreach (var error in result.Errors!)
            {
                messages.Add($"{result.InstanceLocation}: {error.Key} {error.Value}");
            }
        }

        if (result.HasDetails)
        {
            foreach (var detail in result.Details!)
            {
                CollectValidationMessages(detail, messages);
            }
        }
    }

    private static string SchemaRoot() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "contracts", "schemas", "v1"));
    private static string FixturePath(string fileName) => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "contracts", "fixtures", "v1", fileName));
}
