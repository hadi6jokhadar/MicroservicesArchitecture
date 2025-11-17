using IhsanDev.Shared.Application.Localization;

namespace IhsanDev.Shared.Application.Exceptions;

/// <summary>
/// Base exception for all application errors
/// Usage: 
/// - throw new BadRequestException(LocalizationKeys.Exceptions.BadRequest);
/// - throw new BadRequestException(LocalizationKeys.Exceptions.UserNotFound, localizationService);
/// </summary>
public abstract class AppException : Exception
{
    public int StatusCode { get; }
    public string Title { get; }
    public string LocalizationKey { get; }

    /// <summary>
    /// Create exception with localization key (message will be the key itself if localization service not available)
    /// </summary>
    protected AppException(string localizationKey, int statusCode, string titleKey) 
        : base(localizationKey)
    {
        StatusCode = statusCode;
        LocalizationKey = localizationKey;
        Title = titleKey;
    }

    /// <summary>
    /// Create exception with localization key and service (message will be localized)
    /// </summary>
    protected AppException(string localizationKey, int statusCode, string titleKey, ILocalizationService localizationService) 
        : base(localizationService?.GetString(localizationKey) ?? localizationKey)
    {
        StatusCode = statusCode;
        LocalizationKey = localizationKey;
        Title = localizationService?.GetString(titleKey) ?? titleKey;
    }

    /// <summary>
    /// Create exception with localization key, service, and format arguments
    /// </summary>
    protected AppException(string localizationKey, int statusCode, string titleKey, ILocalizationService localizationService, params object[] args) 
        : base(localizationService?.GetString(localizationKey, args) ?? localizationKey)
    {
        StatusCode = statusCode;
        LocalizationKey = localizationKey;
        Title = localizationService?.GetString(titleKey) ?? titleKey;
    }
}

// ✅ 400 Bad Request
public class BadRequestException : AppException
{
    /// <summary>
    /// Create with localization key only (for exception middleware to localize)
    /// </summary>
    public BadRequestException(string localizationKey = LocalizationKeys.Exceptions.BadRequest) 
        : base(localizationKey, 400, LocalizationKeys.Exceptions.BadRequest)
    {
    }

    /// <summary>
    /// Create with localization service (message localized immediately)
    /// </summary>
    public BadRequestException(string localizationKey, ILocalizationService localizationService) 
        : base(localizationKey, 400, LocalizationKeys.Exceptions.BadRequest, localizationService)
    {
    }

    /// <summary>
    /// Create with localization service and format arguments
    /// </summary>
    public BadRequestException(string localizationKey, ILocalizationService localizationService, params object[] args) 
        : base(localizationKey, 400, LocalizationKeys.Exceptions.BadRequest, localizationService, args)
    {
    }
}

// ✅ 401 Unauthorized
public class UnauthorizedException : AppException
{
    public UnauthorizedException(string localizationKey = LocalizationKeys.Exceptions.Unauthorized) 
        : base(localizationKey, 401, LocalizationKeys.Exceptions.Unauthorized)
    {
    }

    public UnauthorizedException(string localizationKey, ILocalizationService localizationService) 
        : base(localizationKey, 401, LocalizationKeys.Exceptions.Unauthorized, localizationService)
    {
    }

    public UnauthorizedException(string localizationKey, ILocalizationService localizationService, params object[] args) 
        : base(localizationKey, 401, LocalizationKeys.Exceptions.Unauthorized, localizationService, args)
    {
    }
}

// ✅ 403 Forbidden
public class ForbiddenException : AppException
{
    public ForbiddenException(string localizationKey = LocalizationKeys.Exceptions.Forbidden) 
        : base(localizationKey, 403, LocalizationKeys.Exceptions.Forbidden)
    {
    }

    public ForbiddenException(string localizationKey, ILocalizationService localizationService) 
        : base(localizationKey, 403, LocalizationKeys.Exceptions.Forbidden, localizationService)
    {
    }

    public ForbiddenException(string localizationKey, ILocalizationService localizationService, params object[] args) 
        : base(localizationKey, 403, LocalizationKeys.Exceptions.Forbidden, localizationService, args)
    {
    }
}

// ✅ 404 Not Found
public class NotFoundException : AppException
{
    public NotFoundException(string localizationKey = LocalizationKeys.Exceptions.NotFound) 
        : base(localizationKey, 404, LocalizationKeys.Exceptions.NotFound)
    {
    }

    public NotFoundException(string localizationKey, ILocalizationService localizationService) 
        : base(localizationKey, 404, LocalizationKeys.Exceptions.NotFound, localizationService)
    {
    }

    public NotFoundException(string localizationKey, ILocalizationService localizationService, params object[] args) 
        : base(localizationKey, 404, LocalizationKeys.Exceptions.NotFound, localizationService, args)
    {
    }
}

// ✅ 409 Conflict
public class ConflictException : AppException
{
    public ConflictException(string localizationKey = LocalizationKeys.Exceptions.Conflict) 
        : base(localizationKey, 409, LocalizationKeys.Exceptions.Conflict)
    {
    }

    public ConflictException(string localizationKey, ILocalizationService localizationService) 
        : base(localizationKey, 409, LocalizationKeys.Exceptions.Conflict, localizationService)
    {
    }

    public ConflictException(string localizationKey, ILocalizationService localizationService, params object[] args) 
        : base(localizationKey, 409, LocalizationKeys.Exceptions.Conflict, localizationService, args)
    {
    }
}

// ✅ 500 Internal Server Error - General Error
public class GeneralException : AppException
{
    public GeneralException(string localizationKey = LocalizationKeys.Exceptions.InternalServerError) 
        : base(localizationKey, 500, LocalizationKeys.Exceptions.InternalServerError)
    {
    }

    public GeneralException(string localizationKey, ILocalizationService localizationService) 
        : base(localizationKey, 500, LocalizationKeys.Exceptions.InternalServerError, localizationService)
    {
    }

    public GeneralException(string localizationKey, ILocalizationService localizationService, params object[] args) 
        : base(localizationKey, 500, LocalizationKeys.Exceptions.InternalServerError, localizationService, args)
    {
    }
}