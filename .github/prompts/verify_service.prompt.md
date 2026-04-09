---
agent: "agent"
description: "Verifies if a specific service follows the MicroservicesArchitecture standards."
---

# Service Verification Workflow

Use this workflow to audit an existing service against the project's architectural standards.

## 1. Load Standards

First, read the architectural standards defined in the `create_new_service` prompt:
`.github/prompts/create_new_service.prompt.md`

## 2. Analyze Target Service

The user should provide the path to the service they want verified (e.g., `src/Services/Catalog`).

1. **Explore the Structure**: Map out the service's `API`, `Application`, `Domain`, and `Infrastructure` directories.
2. **Sample Key Files**: Inspect representative files in each layer (e.g., one Entity, one DTO, one Repository, one Endpoint).

## 3. Verification Checklist

Compare the service's implementation against these key requirements:

### Domain Layer

- [ ] **Entities**: Must inherit from `BaseEntity`.
- [ ] **Repository Interfaces**: Must inherit from `IRepository<T>`.

### Application Layer

- [ ] **DTOs**: Must inherit from `BaseDto`.
- [ ] **Commands/Queries**: Must use `MediatR` (`IRequest`).
- [ ] **Validators**: Must inherit from `LocalizedValidator<T>`.

### Infrastructure Layer

- [ ] **DbContext**: Must inherit from `BaseDbContext`.
- [ ] **Repository Implementation**: Must inherit from `Repository<T>` and implement the domain interface.
- [ ] **Dependency Injection**: Services and repositories must be registered in an `Extensions` class (e.g., `InfrastructureServiceExtensions`).

### API Layer

- [ ] **Endpoints**: Must be defined using Minimal API `MapGroup` in an `Extensions` class (e.g., `EndpointMappingExtensions.cs`).
- [ ] **Validation**: Must use `AddEndpointFilter<ValidationFilter<T>>` where `ValidationFilter` inherits from `SharedValidationFilter`.
- [ ] **Handlers**: Should use static handler methods to keep the endpoint mapping clean.

### Shared Kernel Usage

- [ ] **Localization**: References `ILocalizationService` and `LocalizationKeys` instead of hardcoded strings.
- [ ] **Result Types**: Uses standard result wrappers or HTTP responses consistent with the Identity service.

## 4. Report Results

Generate a verification report for the user:

- **✅ Compliant**: List areas that follow the standard perfectly.
- **❌ Violations**: List specific files or patterns that deviate from the standard.
- **⚠️ Recommendations**: Suggest how to fix the violations to align with the `Identity` service pattern.
