---
name: be-verify-service
description: Audit an existing .NET microservice against Clean Architecture and project standards — checks all four layers (Domain, Application, Infrastructure, API), localization, and observability compliance. Use this whenever the user asks to verify, audit, review, validate, or check a .NET service against project standards. Also invoke it proactively before declaring any service implementation complete.
---

# Service Verification Workflow

Use this workflow to audit an existing service against the project's architectural standards.

## 1. Load Standards

Read `.claude/instructions/Dotnet.instructions.md` and `.claude/instructions/database-strategy.instructions.md` for the reference patterns before proceeding.

## 2. Analyze Target Service

User provides the service path (e.g., `src/Services/Catalog`).

1. **Explore the Structure**: Map out `API`, `Application`, `Domain`, and `Infrastructure` directories
2. **Sample Key Files**: Inspect one Entity, one DTO, one Repository, one Endpoint

## 3. Verification Checklist

### Domain Layer
- [ ] Entities inherit from `BaseEntity`
- [ ] Repository interfaces inherit from `IRepository<T>`

### Application Layer
- [ ] DTOs inherit from `BaseDto`
- [ ] Commands/Queries use MediatR (`IRequest`)
- [ ] Validators inherit from `LocalizedValidator<T>`
- [ ] No hardcoded strings — all messages use `LocalizationKeys`

### Infrastructure Layer
- [ ] DbContext inherits from `BaseDbContext`
- [ ] DbContext uses correct strategy pattern (A/B/C/D) with proper `OnConfiguring` fallback
- [ ] Repository inherits from `Repository<T>` and implements the domain interface
- [ ] DI registration in an `InfrastructureServiceExtensions` class

### API Layer
- [ ] Endpoints use Minimal API `MapGroup` in an Extensions class
- [ ] Validation uses `AddEndpointFilter<ValidationFilter<T>>` where `ValidationFilter` inherits from `SharedValidationFilter`
- [ ] Static handler methods used to keep endpoint mapping clean
- [ ] No `[ApiController]` — no controllers

### Shared Kernel Usage
- [ ] `ILocalizationService` and `LocalizationKeys` used instead of hardcoded strings
- [ ] Standard `AppException` subclasses used for all user-facing errors

### Observability
- [ ] `builder.Services.AddPlatformObservability(builder.Configuration, "{ServiceName}")` in `Program.cs`
- [ ] `app.MapPrometheusScrapingEndpoint("/metrics")` before `app.Run()`
- [ ] `"Observability": { "OtlpEndpoint": "http://localhost:4317" }` in `appsettings.json`
- [ ] Service port listed in `prometheus.yml` at repo root

## 4. Report Results

- **Compliant**: List areas that follow the standard
- **Violations**: List specific files or patterns that deviate
- **Recommendations**: How to fix violations to align with the Identity service pattern
