# Identity Service API Documentation

## Overview

This comprehensive Identity Service provides complete user management and authentication functionality with separate endpoints for regular users and administrators. It implements CQRS pattern with MediatR, includes JWT-based authentication, and supports pagination for listing operations.

## Architecture

The service follows clean architecture principles with modern .NET patterns:

- **API Layer**: Controllers with proper HTTP endpoints and OpenAPI documentation
- **Application Layer**: Commands, Queries, Handlers, DTOs with AutoMapper, and Services
- **Domain Layer**: Entities and Repository interfaces
- **Infrastructure Layer**: Repository implementations, services, and data access

### Key Architectural Patterns

- **CQRS (Command Query Responsibility Segregation)** with MediatR
- **Repository Pattern** with generic base repository
- **AutoMapper Integration** with `IMapFrom<T>` interface for clean object mapping
- **Dependency Injection** with built-in .NET DI container
- **Global Exception Handling** with custom middleware

## Controllers and Endpoints

### 1. AuthController (`api/auth`)

**Public endpoints for authentication:**

| Endpoint                    | Method | Description                              | Authentication |
| --------------------------- | ------ | ---------------------------------------- | -------------- |
| `/api/auth/register`        | POST   | Register a new user account              | None           |
| `/api/auth/login`           | POST   | Login with email and password            | None           |
| `/api/auth/refresh`         | POST   | Refresh access token using refresh token | None           |
| `/api/auth/logout`          | POST   | Logout current user                      | Bearer Token   |
| `/api/auth/forgot-password` | POST   | Request password reset                   | None           |

### 2. UserController (`api/user`)

**User-specific endpoints:**

| Endpoint                    | Method | Description                               | Authentication |
| --------------------------- | ------ | ----------------------------------------- | -------------- |
| `/api/user/login`           | POST   | Alternative login endpoint                | None           |
| `/api/user/refresh-token`   | POST   | Alternative refresh token endpoint        | None           |
| `/api/user/profile`         | GET    | Get current user profile                  | Bearer Token   |
| `/api/user/profile`         | PUT    | Update current user profile               | Bearer Token   |
| `/api/user/forget-password` | POST   | Request password reset                    | None           |
| `/api/user/me`              | DELETE | Delete current user account (soft delete) | Bearer Token   |

### 3. AdminUsersController (`api/admin/users`)

**Admin-only endpoints for user management:**

| Endpoint                              | Method | Description                                 | Authentication   |
| ------------------------------------- | ------ | ------------------------------------------- | ---------------- |
| `/api/admin/users`                    | GET    | Get all users with pagination and filtering | Admin/SuperAdmin |
| `/api/admin/users/{id}`               | GET    | Get user by ID                              | Admin/SuperAdmin |
| `/api/admin/users`                    | POST   | Create a new user                           | Admin/SuperAdmin |
| `/api/admin/users/{id}`               | PUT    | Update existing user                        | Admin/SuperAdmin |
| `/api/admin/users/{id}`               | DELETE | Delete user (soft delete)                   | Admin/SuperAdmin |
| `/api/admin/users/{id}/toggle-status` | PATCH  | Enable/disable user account                 | Admin/SuperAdmin |

## Request/Response Models

### Authentication Models

```csharp
// Register Request
{
  "email": "user@example.com",
  "password": "SecurePass123!",
  "firstName": "John",
  "lastName": "Doe",
  "phoneNumber": "+1234567890" // optional
}

// Login Request
{
  "email": "user@example.com",
  "password": "SecurePass123!"
}

// Authentication Response
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "base64-encoded-refresh-token",
  "expiresAt": "2025-10-12T15:30:00Z",
  "user": {
    "id": 1,
    "email": "user@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "role": "User"
  }
}
```

### User Profile Models

```csharp
// Update Profile Request
{
  "firstName": "John",
  "lastName": "Doe",
  "phoneNumber": "+1234567890",
  "profilePictureUrl": "https://example.com/avatar.jpg"
}

// User Profile Response
{
  "id": 1,
  "email": "user@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "phoneNumber": "+1234567890",
  "role": "User",
  "profilePictureUrl": "https://example.com/avatar.jpg",
  "emailConfirmed": true,
  "created": "2025-01-01T10:00:00Z",
  "lastLogin": "2025-10-12T14:00:00Z"
}
```

### Admin User Management Models

```csharp
// Create User Request
{
  "email": "newuser@example.com",
  "password": "SecurePass123!",
  "firstName": "Jane",
  "lastName": "Smith",
  "role": "User", // User, Admin, SuperAdmin
  "phoneNumber": "+1234567890" // optional
}

// Update User Request
{
  "firstName": "Jane",
  "lastName": "Smith",
  "role": "Admin",
  "phoneNumber": "+1234567890",
  "emailConfirmed": true,
  "status": true
}

// User List Response (with pagination)
{
  "items": [
    {
      "id": 1,
      "email": "user@example.com",
      "firstName": "John",
      "lastName": "Doe",
      "role": "User",
      "phoneNumber": "+1234567890",
      "emailConfirmed": true,
      "status": true,
      "created": "2025-01-01T10:00:00Z",
      "lastLogin": "2025-10-12T14:00:00Z"
    }
  ],
  "pageNumber": 1,
  "totalPages": 5,
  "totalCount": 50,
  "hasPreviousPage": false,
  "hasNextPage": true
}
```

## Query Parameters for User Listing

When calling `GET /api/admin/users`, you can use these query parameters:

- `pageNumber`: Page number (default: 1)
- `pageSize`: Items per page (default: 10, max: 100)
- `searchTerm`: Search in firstName, lastName, and email
- `role`: Filter by role (User, Admin, SuperAdmin)
- `status`: Filter by status (true/false)

Example: `/api/admin/users?pageNumber=2&pageSize=20&searchTerm=john&role=User&status=true`

## Authentication & Authorization

### JWT Configuration

The service uses JWT tokens with the following claims:

- `NameIdentifier`: User ID
- `Email`: User email address
- `GivenName`: First name
- `Surname`: Last name
- `Role`: User role

### Role-Based Access

- **Public**: Registration, login, password reset
- **Authenticated Users**: Profile management, account deletion
- **Admin/SuperAdmin**: Complete user management

## Features Implemented

### Core Features ✅

- User registration and login
- JWT token authentication with refresh tokens
- Password hashing with BCrypt
- User profile management
- Soft delete functionality
- Email validation
- Role-based authorization

### Admin Features ✅

- Complete user CRUD operations
- User search and filtering
- Pagination support
- User status toggle (enable/disable)
- Role management

### Advanced Features ✅

- CQRS pattern with MediatR
- Command and Query separation
- **AutoMapper integration with IMapFrom<T> interface** for efficient object mapping
- **ProjectTo for optimized database queries** in pagination scenarios
- Comprehensive validation with FluentValidation
- Global exception handling
- Structured logging support
- Repository pattern implementation

### Mapping Strategy ✅

- **Consistent AutoMapper Usage**: All DTOs implement `IMapFrom<User>` interface
- **Custom Mapping Configuration**: Role enum to string conversion
- **Query Optimization**: Uses `ProjectTo<T>()` for efficient database projections
- **Centralized Mapping**: All mapping logic is contained within DTOs

```csharp
// Example DTO with AutoMapper integration
public record UserListDto(...) : IMapFrom<User>
{
    public void Mapping(Profile profile)
    {
        profile.CreateMap<User, UserListDto>()
            .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Role.ToString()));
    }
}
```

### Security Features ✅

- BCrypt password hashing
- JWT token security
- Refresh token mechanism
- Role-based access control
- Input validation and sanitization
- Secure password requirements

## Database Schema

The service uses Entity Framework Core with support for multiple databases (PostgreSQL, SQL Server, SQLite). The main entity is `User` which extends `BaseUser` and includes:

- **Identity**: Id, Email, PasswordHash
- **Personal**: FirstName, LastName, PhoneNumber, ProfilePictureUrl
- **System**: Role, Status, EmailConfirmed, Created, LastModified
- **Authentication**: RefreshToken, RefreshTokenExpiryTime, LastLogin
- **Soft Delete**: IsArchived

## Error Handling

The API returns consistent error responses:

```json
{
  "error": "Error message describing what went wrong"
}
```

Common HTTP status codes:

- `200 OK`: Success
- `201 Created`: Resource created successfully
- `400 Bad Request`: Validation errors or bad input
- `401 Unauthorized`: Authentication required
- `403 Forbidden`: Insufficient permissions
- `404 Not Found`: Resource not found
- `409 Conflict`: Resource already exists

## Configuration

Key configuration settings in `appsettings.json`:

```json
{
  "Jwt": {
    "Key": "your-secret-key-minimum-256-bits-long",
    "Issuer": "IdentityService",
    "Audience": "MicroservicesApp",
    "ExpiryInMinutes": 60,
    "RefreshTokenExpiryInDays": 7
  },
  "DatabaseSettings": {
    "Provider": "PostgreSql",
    "ConnectionString": "your-connection-string"
  }
}
```

This comprehensive Identity Service provides enterprise-level user management capabilities with clean architecture, proper security measures, and extensive functionality for both end users and administrators.
