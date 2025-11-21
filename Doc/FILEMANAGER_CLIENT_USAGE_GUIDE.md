# FileManager Service Client - Usage Guide

## Overview

The `AddFileManagerServiceClient()` extension method provides a simple, reusable way to integrate FileManager service into any microservice for fast file operations.

## Quick Setup

### 1. Add to Program.cs

```csharp
using IhsanDev.Shared.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Register FileManager service client
builder.Services.AddFileManagerServiceClient(
    builder.Configuration,
    "YourServiceName",  // Replace with your service name
    builder.Environment.IsDevelopment());

var app = builder.Build();
app.Run();
```

### 2. Configure in appsettings.json

```json
{
  "Services": {
    "FileManagerService": {
      "BaseUrl": "https://localhost:5005",
      "Timeout": 5
    }
  },
  "ServiceCommunication": {
    "SharedSecret": "your-shared-secret-here",
    "Enabled": true
  }
}
```

### 3. Inject and Use in Your Handlers

```csharp
using IhsanDev.Shared.Application.Common.Interfaces;

public class YourCommandHandler
{
    private readonly IFileManagerServiceClient _fileManagerClient;

    public YourCommandHandler(IFileManagerServiceClient fileManagerClient)
    {
        _fileManagerClient = fileManagerClient;
    }

    public async Task<YourResponse> Handle(YourCommand request, CancellationToken ct)
    {
        // Get file metadata
        var file = await _fileManagerClient.GetFileByIdAsync(
            request.FileId,
            tenantId: "optional-tenant-id",
            ct);

        if (file == null)
        {
            // File not found - handle gracefully
            return new YourResponse { File = null };
        }

        // Use file metadata
        return new YourResponse
        {
            FileUrl = file.Url,
            FileName = file.Name,
            FileSize = file.Size
        };
    }
}
```

## Real-World Examples

### Example 1: Identity Service (Profile Pictures)

```csharp
// Program.cs
builder.Services.AddFileManagerServiceClient(
    builder.Configuration,
    "IdentityService",
    builder.Environment.IsDevelopment());

// Handler
public async Task<UserDto> Handle(GetUserProfileCommand request, CancellationToken ct)
{
    var user = await _userRepository.GetByIdAsync(request.UserId, ct);
    var userDto = UserDto.MapFrom(user);

    if (request.IncludeProfilePicture && user.ProfilePictureId.HasValue)
    {
        userDto.ProfilePicture = await _fileManagerClient.GetFileByIdAsync(
            user.ProfilePictureId.Value,
            _tenantContext.CurrentTenant?.TenantId,
            ct);
    }

    return userDto;
}
```

### Example 2: Notification Service (Email Attachments)

```csharp
// Program.cs
builder.Services.AddFileManagerServiceClient(
    builder.Configuration,
    "NotificationService",
    builder.Environment.IsDevelopment());

// Handler
public async Task<EmailDto> Handle(SendEmailCommand request, CancellationToken ct)
{
    var attachments = new List<FileManagerDto>();

    foreach (var fileId in request.AttachmentIds)
    {
        var file = await _fileManagerClient.GetFileByIdAsync(fileId, null, ct);
        if (file != null)
        {
            attachments.Add(file);
        }
    }

    await _emailService.SendAsync(
        request.To,
        request.Subject,
        request.Body,
        attachments);

    return new EmailDto { Sent = true };
}
```

### Example 3: Document Service (File References)

```csharp
// Program.cs
builder.Services.AddFileManagerServiceClient(
    builder.Configuration,
    "DocumentService",
    builder.Environment.IsDevelopment());

// Handler
public async Task<DocumentDto> Handle(GetDocumentCommand request, CancellationToken ct)
{
    var document = await _documentRepository.GetByIdAsync(request.Id, ct);
    var dto = DocumentDto.MapFrom(document);

    // Load all file references
    if (request.IncludeFiles && document.FileIds.Any())
    {
        var fileTasks = document.FileIds.Select(fileId =>
            _fileManagerClient.GetFileByIdAsync(fileId, request.TenantId, ct));

        var files = await Task.WhenAll(fileTasks);
        dto.Files = files.Where(f => f != null).ToList();
    }

    return dto;
}
```

## Configuration Options

### Basic Configuration

```json
{
  "Services": {
    "FileManagerService": {
      "BaseUrl": "https://localhost:5005"
    }
  }
}
```

### Advanced Configuration

```json
{
  "Services": {
    "FileManagerService": {
      "BaseUrl": "https://filemanager.yourdomain.com",
      "Timeout": 10
    }
  },
  "ServiceCommunication": {
    "SharedSecret": "prod-secret-key",
    "Enabled": true,
    "AllowedServices": [
      "IdentityService",
      "NotificationService",
      "DocumentService"
    ]
  }
}
```

### Environment-Specific Configuration

**Development (appsettings.Development.json)**

```json
{
  "Services": {
    "FileManagerService": {
      "BaseUrl": "https://localhost:5005",
      "Timeout": 5
    }
  }
}
```

**Production (appsettings.Production.json)**

```json
{
  "Services": {
    "FileManagerService": {
      "BaseUrl": "https://filemanager-internal.production.com",
      "Timeout": 3
    }
  }
}
```

## Features

### ✅ Automatic Configuration

- Base URL from configuration
- Service authentication headers
- SSL validation (dev/prod)
- Timeout configuration

### ✅ Error Handling

- Returns `null` on errors (graceful)
- Logs warnings/errors automatically
- No exceptions thrown

### ✅ Multi-Tenant Support

- Optional `tenantId` parameter
- Automatic tenant context passing
- Works with global and tenant-specific files

### ✅ Performance

- Uses internal endpoint (fast)
- Bypasses rate limiting
- Connection pooling
- Configurable timeout

## API Reference

### AddFileManagerServiceClient

```csharp
IServiceCollection AddFileManagerServiceClient(
    this IServiceCollection services,
    IConfiguration configuration,
    string serviceName,
    bool isDevelopment = false)
```

**Parameters:**

- `services`: Service collection
- `configuration`: App configuration
- `serviceName`: Your service name (for logging/tracking)
- `isDevelopment`: Whether to bypass SSL validation

**Returns:** Service collection for chaining

### IFileManagerServiceClient.GetFileByIdAsync

```csharp
Task<FileManagerDto?> GetFileByIdAsync(
    int fileId,
    string? tenantId = null,
    CancellationToken cancellationToken = default)
```

**Parameters:**

- `fileId`: File ID to retrieve
- `tenantId`: Optional tenant ID
- `cancellationToken`: Cancellation token

**Returns:** File metadata or `null` if not found

## FileManagerDto Properties

```csharp
public class FileManagerDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Extension { get; set; }
    public long Size { get; set; }
    public string Path { get; set; }      // Storage path
    public string Url { get; set; }       // Public URL
    public int Group { get; set; }
    public int Type { get; set; }
    public bool Temp { get; set; }
    public bool Status { get; set; }
    public bool IsArchived { get; set; }
    public int? UserId { get; set; }
    public string Created { get; set; }
    public string? LastModified { get; set; }
}
```

## Best Practices

### ✅ DO:

- Use `async/await` properly
- Handle `null` returns gracefully
- Pass `tenantId` when working with tenant-specific files
- Use cancellation tokens
- Log errors for monitoring

### ❌ DON'T:

- Assume file always exists
- Call service for every list item (performance)
- Ignore null returns
- Use public endpoints for service calls
- Hardcode file URLs

## Troubleshooting

| Issue           | Solution                                           |
| --------------- | -------------------------------------------------- |
| `null` returned | Check FileManager is running, verify file exists   |
| 403 Forbidden   | Verify `X-Service-Secret` matches in both services |
| Timeout         | Increase timeout in configuration or check network |
| SSL errors      | Ensure `isDevelopment` is set correctly            |

## Migration from Manual HttpClient

**Before:**

```csharp
services.AddHttpClient<IFileManagerServiceClient, FileManagerServiceClient>(client =>
{
    client.BaseAddress = new Uri("https://localhost:5005");
    client.DefaultRequestHeaders.Add("X-Service-Secret", "secret");
    // ... more configuration
});
```

**After:**

```csharp
services.AddFileManagerServiceClient(
    configuration,
    "YourServiceName",
    Environment.IsDevelopment());
```

## Complete Example

```csharp
// Program.cs
using IhsanDev.Shared.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

// Add FileManager client (ONE LINE!)
builder.Services.AddFileManagerServiceClient(
    builder.Configuration,
    "MyAwesomeService",
    builder.Environment.IsDevelopment());

var app = builder.Build();
app.Run();
```

```csharp
// MyHandler.cs
using IhsanDev.Shared.Application.Common.Interfaces;
using MediatR;

public class MyHandler : IRequestHandler<MyCommand, MyResponse>
{
    private readonly IFileManagerServiceClient _fileManager;

    public MyHandler(IFileManagerServiceClient fileManager)
    {
        _fileManager = fileManager;
    }

    public async Task<MyResponse> Handle(MyCommand request, CancellationToken ct)
    {
        var file = await _fileManager.GetFileByIdAsync(request.FileId, null, ct);

        return new MyResponse
        {
            Success = file != null,
            FileUrl = file?.Url,
            FileName = file?.Name
        };
    }
}
```

That's it! 🎉 Your service can now communicate with FileManager in just 3 lines of code!
