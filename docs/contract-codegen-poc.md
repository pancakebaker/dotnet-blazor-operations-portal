# Operations Contract DTO Codegen POC

This test-only POC evaluates schema-native C# wire DTOs for the Operations
consumer boundary. It uses the pinned `v1.0.0` contract snapshot from
`pancakebaker/dbap-integration-contracts` at commit
`6c89fb85d96110b881ff789470e3d7acfd5331e1`. The generated-style output lives
under `tests/AuctionOperationsPortal.Tests/ContractCodegen/Generated/` and is not referenced by production
code, RabbitMQ handling, persistence, reports, SignalR, or UI models.

## Generator

The selected candidate is NJsonSchema.CodeGeneration.CSharp `11.3.2`, evaluated
with `System.Text.Json` settings. It is schema-native and can preserve explicit
nullable, `Guid`, `DateTimeOffset`, and numeric mappings. The POC snapshot keeps
the output small and reviewable; a future adoption should generate it from a
checked-in tool configuration rather than hand-editing it.

The canonical event schemas use draft-2020-12 URN references. Generation must
therefore use a deterministic local resolver/bundle step. The POC deliberately
keeps the existing repository-local schema snapshot and provenance; it does not
introduce a sibling checkout or runtime dependency.

## Results

- Generated types cover the shared envelope and `BidAccepted`, `AuctionPurchased`,
  `AuctionClosed`, `WinnerSelected`, and `AuctionCancelled` payloads.
- The three canonical fixtures deserialize with `System.Text.Json` and preserve
  event type, tenant, aggregate identifier, version, UUID, timestamp, and
  decimal values.
- An adapter to the existing `IntegrationEventEnvelope` proves the generated
  payload can enter the current Operations mapping boundary without changing
  production code.
- `additionalProperties: true` remains a policy concern: System.Text.Json
  ignores unknown fields by default, which is compatible for additive fields but
  does not capture them.
- Unknown enum values are not represented by these payload DTOs; future enum
  generation must use string-compatible handling or explicit tolerant wrappers.
- Missing required fields and malformed values require schema validation before
  deserialization. DTO deserialization alone can produce default values for
  some absent members and is not a validator.
- `aggregateVersion` and `eventId` remain separate fields; the generated shape
  does not collapse same-version companion events.

The snapshot is 1 generated source file containing 6 DTO families and 53
properties. The existing manual Operations contract file also contains the
production envelope and payload records, but the meaningful mapper and
projection behavior remains outside that mechanical surface. The POC therefore
reduces declaration duplication but does not remove the activity mapper or
Operations-specific validation.

## Decision

**ADOPT LATER.** The POC meets the System.Text.Json, deterministic-shape,
decimal, UUID, timestamp, fixture, and adapter goals, but the generator's
resolver configuration and schema-validation boundary need to be made
reproducible before production adoption. Phase 6 should consider replacing only
the external consumer wire DTO declarations, retaining the current mapper and
all Operations-local models and behavior. Do not generate a NuGet package until
that migration proves a material maintenance reduction.

## Phase 6 hardening

The POC now uses the repository-local snapshot under
`tests/contracts/schemas/v1/`, with provenance in `tests/contracts/CONTRACT_SOURCE.md`
and SHA256 entries in `tests/contracts/codegen/schema-manifest.json`. The
snapshot is the pinned `v1.0.0` release from the canonical contract repository;
Operations does not own or fetch these schemas.

Run `pwsh ./scripts/generate-contract-dtos.ps1` to regenerate the test-only
output, or add `-Verify` to perform a byte-for-byte drift check without changing
the worktree. The script resolves the repository root, checks the exact schema
inventory and hashes, disables network schema retrieval in the test validator,
and uses the stable local template/output pair. No sibling checkout or network
access is required after dependencies are restored.

The conformance boundary is explicit: fixture JSON is validated against the
local draft-2020-12 schemas, deserialized with `System.Text.Json`, and then
passed through the existing test adapter. Tests cover missing required fields,
UUIDs, numeric types, timestamps, and diagnostic validation paths. Canonical
schemas allow additional properties, so unknown fields remain accepted by the
schema and ignored by the DTO. Unknown enum behavior remains a future adoption
risk and is documented rather than hidden.

CI runs both snapshot-integrity and byte-for-byte generated-output verification.
The production build has no generator or validator dependency. Generator
version changes require deliberate regeneration and review; schema upgrades
require a new pinned release, provenance/hash update, regeneration, and
conformance review.
