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

Prerequisites: .NET 10 SDK, PostgreSQL, and RabbitMQ. Copy the development configuration as needed,
provide safe local credentials, and create the `auction_operations` database before applying
migrations.

```text
dotnet restore
dotnet build --no-restore
dotnet test
dotnet run --project src/AuctionOperationsPortal --urls http://localhost:5099
```

Apply migrations with:

```text
dotnet ef database update --project src/AuctionOperationsPortal --startup-project src/AuctionOperationsPortal
```

The optional Live Feed administrator handoff uses the configured Live Feed URL and public-key
trust settings. It does not copy or require Live Feed source files.

## Configuration

Relevant settings are in `src/AuctionOperationsPortal/appsettings.json` and
`appsettings.Development.json`. Environment variables use the normal ASP.NET configuration
mapping, including:

- `ConnectionStrings__AuctionOperationsDb` for the portal-owned PostgreSQL database;
- `BiddingService__BaseUrl` and `BiddingService__TenantEndpoint` for Bidding REST access;
- `RabbitMq__HostName`, `RabbitMq__Port`, `RabbitMq__UserName`, `RabbitMq__Password`, exchange,
  queue, dead-letter, and prefetch settings;
- `LiveFeedAdmin__BaseUrl` and its token/key settings for Live Feed administration;
- `SystemAdminAuth__PrivateKeyPath` and session/data-protection settings for local admin auth.

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
