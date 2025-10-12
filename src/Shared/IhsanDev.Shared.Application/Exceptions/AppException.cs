namespace IhsanDev.Shared.Application.Exceptions;

/// <summary>
/// Base exception for all application errors
/// Usage: throw new BadRequestException("Error message");
/// </summary>
public abstract class AppException : Exception
{
    public int StatusCode { get; }
    public string Title { get; }

    protected AppException(string message, int statusCode, string title) 
        : base(message)
    {
        StatusCode = statusCode;
        Title = title;
    }
}

// ✅ 400 Bad Request
public class BadRequestException : AppException
{
    public BadRequestException(string message) 
        : base(message, 400, "Bad Request")
    {
    }
}

// ✅ 401 Unauthorized
public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message = "Unauthorized access") 
        : base(message, 401, "Unauthorized")
    {
    }
}

// ✅ 403 Forbidden
public class ForbiddenException : AppException
{
    public ForbiddenException(string message = "Access forbidden") 
        : base(message, 403, "Forbidden")
    {
    }
}

// ✅ 404 Not Found
public class NotFoundException : AppException
{
    public NotFoundException(string message) 
        : base(message, 404, "Not Found")
    {
    }
}

// ✅ 409 Conflict
public class ConflictException : AppException
{
    public ConflictException(string message) 
        : base(message, 409, "Conflict")
    {
    }
}