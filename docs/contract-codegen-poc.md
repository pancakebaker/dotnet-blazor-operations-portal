# Operations Contract DTO Codegen

This document records the adopted narrow schema-native C# wire DTO path for the Operations
consumer boundary. It uses the pinned `v1.0.0` contract snapshot from
`pancakebaker/dbap-integration-contracts` at commit
`6c89fb85d96110b881ff789470e3d7acfd5331e1`. The generated output lives
under `src/AuctionOperationsPortal/Contracts/Generated/` and is used by the
production external-event deserialization boundary. The envelope, mapper,
persistence, reports, SignalR, and UI models remain intentionally local.

## Generator

The selected generator is NJsonSchema.CodeGeneration.CSharp `11.3.2`, invoked
by `tools/ContractDtoGenerator` with explicit `System.Text.Json`, decimal,
`Guid`, `DateTimeOffset`, nullable, native-record, and namespace settings. The
payload DTO source is emitted directly from the local schemas. A small
handwritten envelope helper remains in the test project because the canonical
envelope is generic at this evaluation boundary.

The canonical event schemas use draft-2020-12 URN references. Generation must
therefore use a deterministic local resolver/bundle step. The repository keeps
the schema snapshot and provenance locally for reproducible generation; it does
not introduce a sibling checkout or runtime dependency.

## Results

- Generated types cover `BidAccepted`, `AuctionPurchased`, `AuctionClosed`,
  `WinnerSelected`, and `AuctionCancelled` payloads. The envelope remains a
  handwritten local type because its generic payload boundary is an Operations
  adapter concern.
- The three canonical fixtures deserialize with `System.Text.Json` and preserve
  event type, tenant, aggregate identifier, version, UUID, timestamp, and
  decimal values.
- An adapter to the existing `IntegrationEventEnvelope` proves the generated
  payload can enter the current Operations mapping boundary without changing
  production code.
- `additionalProperties: true` remains a policy concern: the emitted DTOs use
  System.Text.Json extension-data properties, so compatible additive fields are
  accepted and retained without becoming typed members.
- Unknown enum values are not represented by these payload DTOs; future enum
  generation must use string-compatible handling or explicit tolerant wrappers.
- Missing required fields and malformed values require schema validation before
  deserialization. DTO deserialization alone can produce default values for
  some absent members and is not a validator.
- `aggregateVersion` and `eventId` remain separate fields; the generated shape
  does not collapse same-version companion events.

The generated source contains 5 payload DTO families and 35 properties. The
handwritten evaluation envelope adds one generic type. The meaningful mapper
and projection behavior remains outside that mechanical surface; generation
reduces declaration duplication without replacing Operations-specific
validation.

## Decision

**ADOPTED NARROWLY.** The generated payload declarations now serve the
production external consumer boundary. The handwritten envelope, adapter,
activity mapper, persistence behavior, and all Operations-local models remain
unchanged in responsibility. The generator and schema validator remain
build/test-time tooling; the production application has no NJsonSchema runtime
dependency. No NuGet package is introduced.

## Phase 6 hardening

The adopted flow uses the repository-local snapshot under
`tests/contracts/schemas/v1/`, with provenance in `tests/contracts/CONTRACT_SOURCE.md`
and SHA256 entries in `tests/contracts/codegen/schema-manifest.json`. The
snapshot is the pinned `v1.0.0` release from the canonical contract repository;
Operations does not own or fetch these schemas.

Run `pwsh ./scripts/generate-contract-dtos.ps1` to regenerate the production
generated source, or add `-Verify` to perform a byte-for-byte drift check
without changing the worktree. The script resolves the repository root, checks
the exact schema inventory and hashes, disables network schema retrieval in the
test validator, and invokes the pinned local NJsonSchema harness. No sibling
checkout or network access is required after dependencies are restored.

The conformance boundary is explicit: fixture JSON is validated against the
local draft-2020-12 schemas, deserialized with `System.Text.Json`, and then
passed through the existing test adapter. Tests cover missing required fields,
UUIDs, numeric types, timestamps, and diagnostic validation paths. Canonical
schemas allow additional properties, so unknown fields remain accepted by the
schema and retained by the generated DTO extension-data property. Unknown enum
behavior remains a future adoption risk and is documented rather than hidden.

CI runs both snapshot-integrity and byte-for-byte generated-output verification.
The production build has no generator or validator dependency. Generator
version changes require deliberate regeneration and review; schema upgrades
require a new pinned release, provenance/hash update, regeneration, and
conformance review. This keeps the external wire boundary reproducible without
publishing a package or changing the canonical contract in this repository.
