# Distributed Bidding Auction Platform Operations Portal

[![CI](https://github.com/pancakebaker/dotnet-blazor-operations-portal/actions/workflows/validation.yml/badge.svg)](https://github.com/pancakebaker/dotnet-blazor-operations-portal/actions/workflows/validation.yml)

Standalone ASP.NET Core Blazor Operations Portal for the Distributed Bidding Auction Platform.

## Responsibility

This repository owns the SystemAdministrator control-plane UI, tenant lifecycle visibility and
controls, RabbitMQ activity consumption, the local activity/history projection, live activity
delivery through SignalR, PDF reporting, authentication/session behavior, and its own PostgreSQL
database migrations.

The portal does not own bidding domain logic, auction persistence, the Bidding PostgreSQL database,
the scheduler, the outbox publisher, Live Feed, Laravel/React, or platform orchestration. Bidding
remains authoritative for auction, bid, tenant, winner, and final-price decisions. Operations
consumes published events and calls Bidding's configured system-administration API; it does not
recalculate prices or infer Buy Now outcomes from ordinary bids.

## Event flow

RabbitMQ publishes integration events to the durable `auction.events` topic exchange. This portal
consumes `BidAccepted`, `AuctionPurchased`, `AuctionClosed`, `WinnerSelected`, and
`AuctionCancelled` through its own `auction-operations.activity` queue. Accepted messages are
persisted to the local `auction_activity` projection before SignalR publication. Duplicate
`eventId` values are acknowledged without a second activity record; distinct events at the same
`aggregateVersion` remain distinct.

`AuctionPurchased` is the authoritative Buy Now event. Operations displays its purchaser, tenant,
final amount, and aggregate version without calculating or overriding those values.

## Authentication and administration

SystemAdministrators authenticate through the local `/login` flow. The portal uses its own short-
lived HttpOnly session cookie and service credentials for configured Bidding and Live Feed admin
operations. Tenant-user authentication and impersonation are outside this repository.

Tenant management uses Bidding REST endpoints with configured expected-version concurrency and
preserves the existing success, `403`, `404`, `409`, and `503` behavior.

## Local setup

### Prerequisites

Install the .NET 10 SDK and run the local PostgreSQL and RabbitMQ instances used by the
platform. The repository's development configuration expects PostgreSQL at `127.0.0.1:55432`
and RabbitMQ at `localhost:5672`, with the local credentials shown in
`src/AuctionOperationsPortal/appsettings.json`. The [DBAP Platform Infrastructure](https://github.com/pancakebaker/docker-dbap-platform)
repository is the intended way to start those shared services.

The portal owns a separate PostgreSQL database named `auction_operations`. RabbitMQ is required
for the activity consumer and health check; the portal can start its HTTP host before the broker
is reachable, but activity consumption and `/health` will remain unhealthy until it is available.
The Bidding Service is needed for tenant-management operations, not for the portal process to
construct its local UI. Live Feed is needed only for the administrator Live Feed handoff.

### Create local configuration

The portal uses the standard ASP.NET Core configuration providers; it does not load a `.env`
file. For a normal local run, use the checked-in `appsettings.Development.json` defaults and
select the Development environment before running any command that starts the portal:

PowerShell:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DATA_PROTECTION_KEYS_PATH = $null
```

The equivalent Bash setup is:

```bash
export ASPNETCORE_ENVIRONMENT=Development
unset DATA_PROTECTION_KEYS_PATH
```

For machine-specific values, use environment variables (double underscores map to nested
configuration) or an untracked `src/AuctionOperationsPortal/appsettings.Development.local.json`.
Do not put real credentials or private keys in tracked files.

The explicit Development setting is part of the fresh-clone setup. The startup validators allow
the checked-in SystemAdminAuth and SystemAdminSession defaults only in Development or Testing;
without it, the portal applies production validation and requires a real signing key and an
existing Data Protection directory. Do not carry a stale `ASPNETCORE_ENVIRONMENT=Production` or
`DATA_PROTECTION_KEYS_PATH` value into this local run.

### Local SystemAdministrator key

Development startup allows the configured SystemAdminAuth defaults, but the portal must have its
RSA private key before it can issue a downstream token for a Bidding tenant-management request or
a Live Feed administrator handoff. The default portal path is:

```text
src/AuctionOperationsPortal/keys/system-admin-private.pem
```

The portal owns this private key. Bidding owns only the matching public verification key at:

```text
src/bidding-service/keys/system-admin-public.pem
```

Do not copy the private key to Bidding or commit either PEM file. The following local-only
OpenSSL commands generate a compatible RSA pair from the portal repository root; use a separate
PowerShell or Bash equivalent if OpenSSL is not already installed:

PowerShell:

```powershell
New-Item -ItemType Directory -Force src/AuctionOperationsPortal/keys | Out-Null
New-Item -ItemType Directory -Force ..\dotnet-bidding-service\src\bidding-service\keys | Out-Null
openssl genrsa -out src/AuctionOperationsPortal/keys/system-admin-private.pem 2048
openssl rsa -in src/AuctionOperationsPortal/keys/system-admin-private.pem -pubout -out ..\dotnet-bidding-service\src\bidding-service\keys\system-admin-public.pem
```

The paths can be overridden with `SystemAdminAuth__PrivateKeyPath` and
`Authentication__SystemAdmin__PublicKeyPath` in their respective services. The default
SystemAdminAuth values must remain aligned with Bidding: issuer `dbap-system-admin`, key ID
`system-admin-development-1`, audience `bidding-service-admin`, and a lifetime from 60 through
600 seconds (the Development default is 300 seconds). Production must provide its own properly
provisioned key material and must not use development credentials or keys.

The private signing key is not required merely to boot the Development portal or to log in to its
local cookie session. It is required when an authenticated portal action issues a downstream
SystemAdministrator token. The local login account is seeded separately from the key material.

### Data Protection and session keys

No Data Protection directory needs to exist in the documented Development setup. With
`DATA_PROTECTION_KEYS_PATH` unset, ASP.NET Core uses its normal local key storage and the portal
does not call `PersistKeysToFileSystem`; the strict `SystemAdminSession` directory check is only
applied outside Development/Testing. This is sufficient for a single local process and its
short-lived cookie session.

For a persistent or multi-instance deployment, set `DATA_PROTECTION_KEYS_PATH` to an existing,
shared directory before startup. For example, in PowerShell:

```powershell
New-Item -ItemType Directory -Path .local\data-protection -Force | Out-Null
$env:DATA_PROTECTION_KEYS_PATH = (Resolve-Path .local\data-protection).Path
```

The directory is shared by portal instances that must decrypt the same cookie/session data. It is
not shared with Bidding, Live Feed, or any other service, and it must not contain private signing
keys. The `.local` directory is ignored by Git. Production also requires an existing directory,
the configured `SystemAdminSession__CookieName`, and a `SystemAdminSession__LifetimeMinutes`
between 15 and 30.

### Database, restore, build, and test

From the portal repository root, restore and apply the existing EF Core migrations. The target
PostgreSQL database must be reachable; EF Core will create the database if the configured
PostgreSQL user has permission to do so. These migrations create the portal activity projection
and system-admin account tables. There is no migration data seed. In Development, application
startup seeds the configured local account when `SystemAdminDemo:Enabled` is true (the default
email is `systemadmin@example.test` and the default password is `system-admin-password`).

```text
dotnet restore
dotnet build --no-restore --warnaserror
dotnet ef database update --project src/AuctionOperationsPortal --startup-project src/AuctionOperationsPortal
dotnet test --no-build --no-restore
```

### Run the portal

After PostgreSQL is migrated and RabbitMQ is available:

```text
dotnet run --project src/AuctionOperationsPortal --urls http://localhost:5099
```

Open the portal at `http://localhost:5099`. The local administrator login is at `/login`.
The configured development account is seeded only in Development; production does not seed this
demo account.

The health endpoint is:

```text
http://localhost:5099/health
```

It reports separate PostgreSQL and RabbitMQ checks. A healthy portal therefore requires both
dependencies to be reachable, even though the HTTP host itself does not call Bidding or Live Feed
during startup. The portal has no Swagger/OpenAPI endpoint.

The optional Live Feed administrator handoff uses the configured Live Feed URL and opaque-code
exchange. It does not copy or require Live Feed source files.

## Configuration

Relevant settings are in `src/AuctionOperationsPortal/appsettings.json` and
`appsettings.Development.json`. Environment variables use the normal ASP.NET configuration
mapping. The most important local settings are:

| Setting | Purpose | Required locally? |
| --- | --- | --- |
| `ConnectionStrings__AuctionOperationsDb` | Portal-owned PostgreSQL activity and admin-account database | Yes, for migration, demo seeding, login, and activity views |
| `RabbitMq__HostName`, `RabbitMq__Port`, `RabbitMq__UserName`, `RabbitMq__Password`, `RabbitMq__VirtualHost` | Activity consumer broker connection | Yes for activity consumption and a healthy `/health` result |
| `RabbitMq__Exchange`, `RabbitMq__Queue`, `RabbitMq__DeadLetterExchange`, `RabbitMq__DeadLetterQueue`, `RabbitMq__PrefetchCount`, `RabbitMq__MaxRequeueAttempts` | Existing activity topology and retry policy | Defaults are suitable for local infrastructure |
| `BiddingService__BaseUrl`, `BiddingService__TenantEndpoint` | Server-side tenant-administration REST client | Only for tenant-management operations; defaults target `http://localhost:5000` and `/api/system/tenants` |
| `SystemAdminAuth__Issuer`, `SystemAdminAuth__KeyId`, `SystemAdminAuth__PrivateKeyPath`, `SystemAdminAuth__AllowedAudiences`, `SystemAdminAuth__TokenLifetimeSeconds` | Portal RS256 SystemAdministrator token issuer | Defaults are allowed only in Development/Testing; the private key is needed when issuing a downstream token |
| `SystemAdminDemo__Enabled`, `SystemAdminDemo__Email`, `SYSTEM_ADMIN_DEMO_PASSWORD` | Development-only local admin seeding | Optional; Development enables the demo account by default |
| `SystemAdminSession__CookieName`, `SystemAdminSession__LifetimeMinutes` | Local admin cookie session | Defaults are suitable for Development |
| `DATA_PROTECTION_KEYS_PATH` | Existing shared ASP.NET Data Protection key directory | Leave unset for single-process Development; required with an existing directory outside Development/Testing |
| `LiveFeedAdmin__BaseUrl`, `LiveFeedAdmin__SystemTokenEndpoint`, `LiveFeedAdmin__BrowserHandoffEndpoint` | Server-side Live Feed admin handoff | Only when using the Live Feed administrator feature |
| `ASPNETCORE_ENVIRONMENT` | Selects Development defaults and local demo seeding | Set to `Development` for the documented local flow |

The `SystemAdminAuth` and `SystemAdminSession` defaults are not security bypasses. Their
`Validate(true)` paths only permit Development/Testing defaults to be present without requiring
production filesystem material. `SystemAdminTokenIssuer` still requires the private PEM at the
configured path when it issues a token. Outside Development/Testing, SystemAdminAuth requires a
valid issuer, key ID, audience, 60-600 second lifetime, and existing private key; SystemAdminSession
requires a cookie name, 15-30 minute lifetime, and existing Data Protection directory.
`AllowedAudiences` must include the requested downstream audience.

The portal does not load `.env`; use ASP.NET environment variables, user secrets if configured
by your local workflow, or the untracked `appsettings.Development.local.json` file instead.

Do not commit secrets or private keys. Platform-level Docker orchestration and deployment remain
external concerns.

## Related repositories

- [Bidding Service](https://github.com/pancakebaker/dotnet-bidding-service) owns authoritative auction, bid, tenant, winner, and final-price decisions.
- [Live Feed](https://github.com/pancakebaker/nodejs-live-feed) provides public real-time Socket.IO delivery.
- [Laravel React Auction Web](https://github.com/pancakebaker/laravel-react-auction-web) is the tenant-facing BFF and client.
- [DBAP Platform Infrastructure](https://github.com/pancakebaker/docker-dbap-platform) provides development PostgreSQL and RabbitMQ.
- [Historical integrated monorepo](https://github.com/pancakebaker/distributed-bidding-auction-platform) preserves the original platform snapshot.

The [platform architecture map](https://github.com/pancakebaker/docker-dbap-platform/blob/main/docs/architecture.md)
summarizes the service boundaries. This is a functioning architecture and
portfolio/demo portal; deployment hardening remains a separate concern. No
license file is currently included in this extracted repository.

## Validation

The standalone workflow restores, builds with analyzers and zero-warning policy, and runs the full
Operations test project. It provisions PostgreSQL and RabbitMQ with health checks. The repository
also retains the source project’s StyleCop, analyzer, EditorConfig, XML documentation, and
100-column conventions.
