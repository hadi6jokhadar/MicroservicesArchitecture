# 🏗️ Microservices Architecture

A modern, scalable microservices architecture built with **.NET 8** and **ASP.NET Core**, implementing **Clean Architecture** principles, **Domain-Driven Design (DDD)**, and **CQRS** patterns.

## 📋 Table of Contents

- [🏗️ Architecture Overview](#️-architecture-overview)
- [🚀 Features](#-features)
- [🛠️ Technology Stack](#️-technology-stack)
- [📁 Project Structure](#-project-structure)
- [📋 Prerequisites](#-prerequisites)
- [🔧 Getting Started](#-getting-started)
- [🏛️ Shared Libraries](#️-shared-libraries)
- [🔐 Identity Service](#-identity-service)
- [📦 Package Management](#-package-management)
- [🔧 Configuration](#-configuration)
- [🧪 Testing](#-testing)
- [📚 API Documentation](#-api-documentation)
- [🐳 Docker Support](#-docker-support)
- [🤝 Contributing](#-contributing)
- [📄 License](#-license)

## 🏗️ Architecture Overview

This solution implements a **microservices architecture** with a focus on:

- **Domain-Driven Design (DDD)**: Rich domain models with clear business boundaries
- **Clean Architecture**: Separation of concerns with dependency inversion
- **CQRS Pattern**: Command Query Responsibility Segregation using MediatR
- **Event-Driven Architecture**: Loose coupling through domain events
- **Shared Kernel**: Common abstractions and utilities across services

### Architectural Patterns

- **🏛️ Clean Architecture**: Dependencies flow inward toward the domain
- **📝 CQRS**: Separate read and write operations with MediatR
- **🎯 Domain-Driven Design**: Business logic encapsulated in domain entities
- **� Minimal APIs**: High-performance endpoint routing (replacing traditional controllers)
- **🛡️ Repository Pattern**: Data access abstraction with generic repositories
- **🏭 Dependency Injection**: Built-in .NET DI container with service registration
- **🔄 Pipeline Behaviors**: Request/response processing with validation and logging

## 🚀 Features

### Core Features

- ✅ **Multi-Database Support**: PostgreSQL, SQL Server, MySQL, SQLite
- ✅ **JWT Authentication**: Secure token-based authentication with refresh tokens
- ✅ **Centralized Package Management**: Consistent versioning across services
- ✅ **Global Exception Handling**: Centralized error management
- ✅ **Input Validation**: FluentValidation integration
- ✅ **Auto Mapping**: AutoMapper for object transformations
- ✅ **Swagger/OpenAPI**: Interactive API documentation
- ✅ **Minimal APIs**: Modern endpoint routing with performance benefits (migrated from controllers)
- ✅ **Endpoint Handlers**: Organized API handlers for better maintainability

### Advanced Features

- 🔄 **MediatR Pipeline**: Request/response handling with behaviors
- 🛡️ **BCrypt Password Hashing**: Secure password storage
- 🎯 **Dependency Injection**: Built-in DI container
- 📊 **Structured Logging**: Comprehensive logging system
- 🌐 **CORS Support**: Cross-origin resource sharing
- 🔍 **Role-Based Authorization**: Admin and User role management
- 🔄 **Database Migrations**: Automatic schema management
- 🏢 **Multi-Tenancy Support**: Optional per-tenant configuration (NEW!)
- 🔧 **Configuration-Driven Architecture**: Single build for multiple deployment modes
- 💾 **Configuration Caching**: High-performance tenant config with in-memory caching

## 🛠️ Technology Stack

### Core Framework

- **ASP.NET Core 8.0** - Web API framework
- **Entity Framework Core 8.0** - Object-relational mapping
- **C# 12** - Programming language

### Key Libraries & Packages

| Category              | Package                                       | Version | Purpose                  |
| --------------------- | --------------------------------------------- | ------- | ------------------------ |
| **CQRS & Mediator**   | MediatR                                       | 12.2.0  | Command/Query handling   |
| **Validation**        | FluentValidation                              | 12.0.0  | Input validation         |
| **Mapping**           | AutoMapper                                    | 12.0.1  | Object-to-object mapping |
| **Authentication**    | Microsoft.AspNetCore.Authentication.JwtBearer | 8.0.0   | JWT authentication       |
| **Security**          | BCrypt.Net-Next                               | 4.0.3   | Password hashing         |
| **Database**          | Multiple EF Core providers                    | 8.0.0   | Data access              |
| **API Documentation** | Swashbuckle.AspNetCore                        | 6.5.0   | Swagger/OpenAPI          |

### Database Providers

- **🐘 PostgreSQL** - Npgsql.EntityFrameworkCore.PostgreSQL
- **🗄️ SQL Server** - Microsoft.EntityFrameworkCore.SqlServer
- **🐬 MySQL** - Pomelo.EntityFrameworkCore.MySql
- **📁 SQLite** - Microsoft.EntityFrameworkCore.Sqlite

## 📁 Project Structure

```
MicroservicesArchitecture/
├── 📁 src/
│   ├── 📁 Services/
│   │   ├── 📁 Identity/                    # Identity & Authentication Service
│   │   │   ├── Identity.API/              # 🌐 API Layer (Controllers, Program.cs)
│   │   │   ├── Identity.Application/      # 📋 Application Layer (Commands, Handlers)
│   │   │   ├── Identity.Domain/           # 🏛️ Domain Layer (Entities, Repositories)
│   │   │   └── Identity.Infrastructure/   # 🔧 Infrastructure Layer (Data, Services)
│   │   └── 📁 Tenant/                      # 🏢 Tenant Management Service (NEW!)
│   │       ├── Tenant.API/                # 🌐 API Layer (Endpoints, Configuration)
│   │       ├── Tenant.Application/        # 📋 Application Layer (Commands, Handlers)
│   │       ├── Tenant.Domain/             # 🏛️ Domain Layer (Entities, Repositories)
│   │       └── Tenant.Infrastructure/     # 🔧 Infrastructure Layer (Data, Services)
│   └── 📁 Shared/                          # 🤝 Shared Libraries
│       ├── IhsanDev.Shared.Application/   # 📋 Application abstractions
│       ├── IhsanDev.Shared.Authentication/ # 🔐 Auth components
│       ├── IhsanDev.Shared.Infrastructure/ # 🔧 Infrastructure implementations
│       ├── IhsanDev.Shared.Kernel/        # 🏛️ Domain kernel & primitives
│       ├── IhsanDev.Shared.Messaging/     # 📨 Message bus & events
│       └── IhsanDev.Shared.Notifications/ # 📢 Notification services
├── 📄 Directory.Packages.props            # 📦 Centralized package management
├── 📄 MicroservicesArchitecture.sln       # 🏗️ Solution file
├── 📄 update-csproj.ps1                   # 🔄 Package update utility
├── 📄 MINIMAL_API_MIGRATION.md            # 📋 Migration documentation
├── 📄 MULTI_TENANCY_GUIDE.md              # 🏢 Multi-tenancy comprehensive guide (NEW!)
├── 📄 MULTI_TENANCY_QUICK_START.md        # 🚀 Quick start guide (NEW!)
├── 📄 MULTI_TENANT_DEPLOYMENT_GUIDE.md    # 🐳 Deployment guide (NEW!)
├── 📄 SINGLE_BUILD_MULTIPLE_DEPLOYMENTS.md # 📦 Single build guide (NEW!)
├── 📄 ARCHITECTURE_DIAGRAMS.md            # 🎨 Visual architecture (NEW!)
└── 📄 MULTI_TENANCY_SUMMARY.md            # 📊 Implementation summary (NEW!)
```

### Layer Responsibilities

#### 🌐 API Layer (Identity.API)

- Minimal API endpoints and handlers
- Request/response validation
- Authentication and authorization middleware
- Swagger/OpenAPI configuration
- Endpoint grouping and routing

#### 📋 Application Layer (Identity.Application)

- Commands and queries (CQRS)
- Command/query handlers
- DTOs and mapping profiles
- Business logic orchestration

#### 🏛️ Domain Layer (Identity.Domain)

- Domain entities and value objects
- Repository interfaces
- Domain services
- Business rules and invariants

#### 🔧 Infrastructure Layer (Identity.Infrastructure)

- Entity Framework DbContext
- Repository implementations
- External service integrations
- Database migrations

## 📋 Prerequisites

- **[.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)** or later
- **Database**: PostgreSQL, SQL Server, MySQL, or SQLite
- **IDE**: Visual Studio 2022, VS Code, or JetBrains Rider
- **PowerShell** (for utility scripts)
- **Git** (for version control)

### Optional Tools

- **Docker** (for containerization)
- **Postman** or **curl** (for API testing)
- **pgAdmin** or **SQL Server Management Studio** (database management)

## 🔧 Getting Started

### 1. Clone the Repository

```bash
git clone <repository-url>
cd MicroservicesArchitecture
```

### 2. Restore Dependencies

```bash
dotnet restore
```

### 3. Configure Database

Update the connection string in `appsettings.json`:

```json
{
  "DatabaseSettings": {
    "Provider": "PostgreSql", // or "SqlServer", "MySql", "Sqlite"
    "ConnectionString": "Host=localhost;Database=IdentityDb;Username=your_user;Password=your_password"
  }
}
```

### 4. Configure JWT Settings

Update JWT configuration in `appsettings.json`:

```json
{
  "Jwt": {
    "Secret": "your-super-secret-jwt-key-minimum-32-characters",
    "Issuer": "IdentityService",
    "Audience": "MicroservicesApp",
    "AccessTokenExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 7
  }
}
```

### 5. Run Database Migrations

```bash
cd src/Services/Identity/Identity.API
dotnet ef database update
```

### 6. Build the Solution

```bash
dotnet build
```

### 7. Run the Identity Service

```bash
cd src/Services/Identity/Identity.API
dotnet run
```

The API will be available at:

- **HTTP**: `http://localhost:5000`
- **HTTPS**: `https://localhost:5001`
- **Swagger UI**: `https://localhost:5001/swagger`

## 🏛️ Shared Libraries

### 🏛️ IhsanDev.Shared.Kernel

**Core domain primitives and base classes**

- `BaseEntity` - Base entity with common properties
- `BaseUser` - Base user entity for identity
- Domain events and value objects
- Common enums and constants

### 📋 IhsanDev.Shared.Application

**Application layer abstractions**

- CQRS interfaces (`ICommand`, `IQuery`)
- Validation behaviors with FluentValidation
- Common DTOs and response models
- Application exceptions (`AppException`)
- AutoMapper configuration extensions

### 🔧 IhsanDev.Shared.Infrastructure

**Infrastructure implementations**

- Database context base classes
- Repository pattern implementations
- Unit of Work pattern
- Database extension methods
- Global exception handling middleware

### 🔐 IhsanDev.Shared.Authentication

**Authentication and authorization components**

- JWT token generation and validation
- Authentication middleware
- Authorization policies
- Claims-based security

### 📨 IhsanDev.Shared.Messaging

**Message bus and event handling**

- Event bus abstractions
- Message publishers and subscribers
- Integration event handling
- Domain event dispatching

### 📢 IhsanDev.Shared.Notifications

**Notification services**

- Email notification services
- SMS notification providers
- Push notification handling
- Template-based messaging

## 🔐 Identity Service

The Identity service provides comprehensive user authentication, authorization, and user management capabilities. It implements modern security practices with JWT tokens, role-based access control, and supports multiple database providers.

### Key Features

- ✅ **Secure Authentication**: JWT with refresh token rotation
- ✅ **Role-Based Authorization**: User and Admin role management
- ✅ **Modern Architecture**: Minimal APIs for optimal performance
- ✅ **Multi-Database Support**: PostgreSQL, SQL Server, MySQL, SQLite
- ✅ **Comprehensive API**: User management and admin operations
- ✅ **Security Best Practices**: BCrypt hashing, secure token management

### Quick Access

- 📖 **Detailed Documentation**: [`src/Services/Identity/README.md`](src/Services/Identity/README.md)
- 🔧 **API Specifications**: [`src/Services/Identity/IDENTITY_API_DOCUMENTATION.md`](src/Services/Identity/IDENTITY_API_DOCUMENTATION.md)
- 🌐 **Swagger UI**: `https://localhost:5001/swagger` (when running)

## 🏢 Tenant Service & Multi-Tenancy (NEW!)

The Tenant service enables optional multi-tenancy support, allowing different projects or customers to have isolated configurations while sharing the same Identity Service binary. **This is completely optional and disabled by default**.

### Key Features

- ✅ **Optional Multi-Tenancy**: Disabled by default, zero breaking changes
- ✅ **Single Binary Deployment**: Same code works for tenant and non-tenant modes
- ✅ **Per-Tenant Configuration**: Custom JWT, Database, and CORS settings
- ✅ **Configuration Caching**: High-performance in-memory caching
- ✅ **Automatic Fallback**: Always works even if tenant config fails
- ✅ **Clean Architecture**: Shared abstractions with minimal overhead

### Multi-Tenancy Documentation

- 📖 **Comprehensive Guide**: [`MULTI_TENANCY_GUIDE.md`](MULTI_TENANCY_GUIDE.md) - Full documentation
- 🚀 **Quick Start Guide**: [`MULTI_TENANCY_QUICK_START.md`](MULTI_TENANCY_QUICK_START.md) - Get started in minutes
- 🐳 **Deployment Guide**: [`MULTI_TENANT_DEPLOYMENT_GUIDE.md`](MULTI_TENANT_DEPLOYMENT_GUIDE.md) - Docker, K8s, environments
- 📦 **Single Build Guide**: [`SINGLE_BUILD_MULTIPLE_DEPLOYMENTS.md`](SINGLE_BUILD_MULTIPLE_DEPLOYMENTS.md) - One binary, multiple modes
- 🎨 **Architecture Diagrams**: [`ARCHITECTURE_DIAGRAMS.md`](ARCHITECTURE_DIAGRAMS.md) - Visual architecture
- 📊 **Implementation Summary**: [`MULTI_TENANCY_SUMMARY.md`](MULTI_TENANCY_SUMMARY.md) - What was built

### How It Works

**Single Binary, Multiple Modes:**

```bash
# Deploy without multi-tenancy (Project A)
docker run -e MultiTenancy__Enabled=false identity-service:1.0.0

# Deploy with multi-tenancy (Project B)
docker run -e MultiTenancy__Enabled=true identity-service:1.0.0
```

**Configuration-Driven:**

- When `MultiTenancy:Enabled = false`: Uses appsettings.json (traditional mode)
- When `MultiTenancy:Enabled = true`: Supports per-tenant configuration via Tenant Service
- Automatic fallback to appsettings.json if tenant config is unavailable

**Use Cases:**

- **Without Tenants**: Single application, all users share same configuration
- **With Tenants**: SaaS platform where each customer/tenant has isolated settings

### Quick Example

#### Without Multi-Tenancy (Default)

```json
{
  "MultiTenancy": { "Enabled": false },
  "Jwt": {
    "Secret": "your-secret",
    "Issuer": "YourApp"
  }
}
```

All requests use this configuration.

#### With Multi-Tenancy (Optional)

```json
{
  "MultiTenancy": {
    "Enabled": true,
    "TenantServiceUrl": "http://tenant-service:80"
  },
  "Jwt": {
    "Secret": "default-secret",
    "Issuer": "Platform"
  }
}
```

Requests with `x-tenant-id` header use tenant-specific config, others use default.

### API Endpoints

#### Authentication Endpoints

| Method | Endpoint                    | Description            | Auth Required |
| ------ | --------------------------- | ---------------------- | ------------- |
| `POST` | `/api/auth/register`        | Register new user      | ❌            |
| `POST` | `/api/auth/login`           | User login             | ❌            |
| `POST` | `/api/auth/refresh`         | Refresh token          | ❌            |
| `POST` | `/api/auth/logout`          | User logout            | ✅            |
| `POST` | `/api/auth/forgot-password` | Request password reset | ❌            |

#### User Management Endpoints

| Method   | Endpoint            | Description         | Auth Required |
| -------- | ------------------- | ------------------- | ------------- |
| `GET`    | `/api/user/profile` | Get user profile    | ✅            |
| `PUT`    | `/api/user/profile` | Update user profile | ✅            |
| `DELETE` | `/api/user/me`      | Delete user account | ✅            |

#### Admin Endpoints

| Method   | Endpoint                | Description     | Auth Required |
| -------- | ----------------------- | --------------- | ------------- |
| `GET`    | `/api/admin/users`      | Get all users   | ✅ (Admin)    |
| `GET`    | `/api/admin/users/{id}` | Get user by ID  | ✅ (Admin)    |
| `POST`   | `/api/admin/users`      | Create new user | ✅ (Admin)    |
| `PUT`    | `/api/admin/users/{id}` | Update user     | ✅ (Admin)    |
| `DELETE` | `/api/admin/users/{id}` | Delete user     | ✅ (Admin)    |

### Example Requests

#### Register User

```bash
curl -X POST "https://localhost:5001/api/auth/register" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "user@example.com",
    "password": "SecurePassword123!",
    "firstName": "John",
    "lastName": "Doe"
  }'
```

#### Login

```bash
curl -X POST "https://localhost:5001/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "user@example.com",
    "password": "SecurePassword123!"
  }'
```

#### Get User Profile

```bash
curl -X GET "https://localhost:5001/api/user/profile" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

#### Admin - Get All Users

```bash
curl -X GET "https://localhost:5001/api/admin/users?pageNumber=1&pageSize=10" \
  -H "Authorization: Bearer YOUR_ADMIN_JWT_TOKEN"
```

## 📦 Package Management

This project uses **Central Package Management (CPM)** for consistent versioning.

### How It Works

1. All package versions are defined in `Directory.Packages.props`
2. Project files reference packages without versions
3. Ensures consistency across all projects

### Directory.Packages.props Structure

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="MediatR" Version="12.2.0" />
    <PackageVersion Include="AutoMapper" Version="12.0.1" />
    <!-- ... other packages -->
  </ItemGroup>
</Project>
```

### Updating Packages

1. Update version in `Directory.Packages.props`
2. Run the utility script:

```powershell
.\update-csproj.ps1
```

3. Restore packages:

```bash
dotnet restore
```

## 🔧 Configuration

### Database Configuration

Supports multiple database providers through configuration:

```json
{
  "DatabaseSettings": {
    "Provider": "PostgreSql", // SqlServer, MySql, Sqlite
    "ConnectionString": "Host=localhost;Database=IdentityDb;Username=user;Password=pass",
    "EnableSensitiveDataLogging": false,
    "EnableDetailedErrors": false,
    "CommandTimeout": 30,
    "MaxRetryCount": 3,
    "MaxRetryDelay": 30
  }
}
```

### JWT Configuration

```json
{
  "Jwt": {
    "Secret": "your-secret-key-minimum-32-characters",
    "Issuer": "IdentityService",
    "Audience": "MicroservicesApp",
    "AccessTokenExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 7
  }
}
```

### CORS Configuration

```json
{
  "Cors": {
    "AllowedOrigins": ["http://localhost:4200", "http://localhost:3000"]
  }
}
```

### Logging Configuration

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  }
}
```

## 🧪 Testing

### Running Tests

```bash
# Run all tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test project
dotnet test src/Services/Identity/Identity.Tests
```

### Test Structure

```
Tests/
├── Unit/           # Unit tests for business logic
├── Integration/    # Integration tests for APIs
└── Acceptance/     # End-to-end acceptance tests
```

### Testing Tools

- **xUnit** - Testing framework
- **Moq** - Mocking library
- **FluentAssertions** - Fluent assertion syntax
- **Microsoft.AspNetCore.Mvc.Testing** - Integration testing

## 📚 API Documentation

### Swagger/OpenAPI

Each service exposes interactive API documentation:

- **URL**: `https://localhost:5001/swagger`
- **Features**: Interactive testing, request/response examples
- **Export**: OpenAPI 3.0 specification

### Postman Collection

Import the API endpoints into Postman for testing:

1. Export OpenAPI spec from Swagger
2. Import into Postman
3. Configure environment variables

## 🐳 Docker Support

### Dockerfile Example

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Services/Identity/Identity.API/Identity.API.csproj", "src/Services/Identity/Identity.API/"]
RUN dotnet restore "src/Services/Identity/Identity.API/Identity.API.csproj"
COPY . .
WORKDIR "/src/src/Services/Identity/Identity.API"
RUN dotnet build "Identity.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Identity.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Identity.API.dll"]
```

### Docker Compose

```yaml
version: "3.8"
services:
  identity-api:
    build:
      context: .
      dockerfile: src/Services/Identity/Identity.API/Dockerfile
    ports:
      - "5000:80"
      - "5001:443"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
    depends_on:
      - postgres

  postgres:
    image: postgres:15
    environment:
      POSTGRES_DB: IdentityDb
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: password
    ports:
      - "5432:5432"
```

## 🔄 Migration to Minimal APIs

The Identity Service has been successfully migrated from traditional controllers to **Grouped Minimal APIs** for improved performance and modern .NET practices.

### Migration Benefits

- **🚀 Performance**: ~15% faster startup, ~10% lower memory usage
- **📝 Maintainability**: Better organized handlers and endpoint grouping
- **🔧 Testability**: Simplified unit testing of handler methods
- **📚 Documentation**: Enhanced OpenAPI integration

### Migration Details

- 📋 **Full Documentation**: [`MINIMAL_API_MIGRATION.md`](MINIMAL_API_MIGRATION.md)
- 🔧 **Handler Structure**: Organized by domain (Auth, User, Admin)
- 🎯 **Endpoint Grouping**: Logical API grouping with shared middleware
- ✅ **Backward Compatibility**: All existing API contracts maintained

## 🤝 Contributing

### Development Guidelines

1. **Fork** the repository
2. **Create** a feature branch (`git checkout -b feature/amazing-feature`)
3. **Follow** coding standards and patterns
4. **Write** unit tests for new features
5. **Commit** changes (`git commit -m 'Add amazing feature'`)
6. **Push** to branch (`git push origin feature/amazing-feature`)
7. **Open** a Pull Request

### Code Standards

- ✅ Follow C# coding conventions
- ✅ Use meaningful names
- ✅ Write XML documentation for public APIs
- ✅ Maintain test coverage above 80%
- ✅ Follow Clean Architecture principles

### Commit Convention

```
feat: add new feature
fix: bug fix
docs: documentation update
style: formatting changes
refactor: code refactoring
test: add or update tests
chore: maintenance tasks
```

## 🗺️ Roadmap

### Phase 1 - Foundation ✅

- [x] Identity Service implementation
- [x] Shared libraries setup
- [x] Clean Architecture structure
- [x] JWT authentication with refresh tokens
- [x] Minimal APIs implementation
- [x] Role-based authorization
- [x] Multi-database support

### Phase 2 - Infrastructure 🚧

- [ ] API Gateway integration
- [ ] Service discovery (Consul/Eureka)
- [ ] Docker containerization
- [ ] Health checks implementation

### Phase 3 - Advanced Features 📋

- [ ] Event sourcing implementation
- [ ] Distributed caching (Redis)
- [ ] Message bus (RabbitMQ/Azure Service Bus)
- [ ] Distributed tracing (Jaeger/Zipkin)

### Phase 4 - Production Ready 🎯

- [ ] Circuit breaker pattern
- [ ] Rate limiting
- [ ] Comprehensive monitoring
- [ ] Performance optimization

## 📊 Performance & Monitoring

### Metrics to Monitor

- **Response Time**: API endpoint performance
- **Throughput**: Requests per second
- **Error Rate**: 4xx and 5xx responses
- **Database Performance**: Query execution time
- **Memory Usage**: Application memory consumption

### Recommended Tools

- **Application Performance Monitoring**: Application Insights
- **Logging**: Serilog with structured logging
- **Metrics**: Prometheus + Grafana
- **Tracing**: OpenTelemetry

## 🔒 Security Considerations

### Authentication & Authorization

- ✅ JWT tokens with expiration
- ✅ Refresh token rotation
- ✅ BCrypt password hashing
- ✅ Role-based access control

### API Security

- ✅ HTTPS enforcement
- ✅ CORS configuration
- ✅ Input validation
- ✅ SQL injection prevention

### Best Practices

- 🔐 Secure secret management
- 🛡️ Regular dependency updates
- 🔍 Security scanning
- 📋 Audit logging

## 📄 License

This project is licensed under the **MIT License** - see the [LICENSE](LICENSE) file for details.

## 👥 Authors & Contributors

- **YOUR_USERNAME** - _Initial work_ - [@hadyj](https://github.com/hadyj)
- **IhsanDev** - _Architecture & Design_

## 🙏 Acknowledgments

- **Clean Architecture** by Robert C. Martin
- **Domain-Driven Design** by Eric Evans
- **Building Microservices** by Sam Newman
- **Microsoft .NET Documentation**
- **ASP.NET Core Community**

## 📞 Support & Contact

- 📧 **Email**: support@ihsandev.com
- 💬 **Issues**: [GitHub Issues](https://github.com/hadi6jokhadar/MicroservicesArchitecture/issues)
- 📖 **Documentation**: Service-specific README files in each service directory
- � **Identity Service**: [`src/Services/Identity/README.md`](src/Services/Identity/README.md)

---

<div align="center">

**Built with ❤️ using .NET 8 & ASP.NET Core**

⭐ **Star this repository if it helped you!**

</div>
