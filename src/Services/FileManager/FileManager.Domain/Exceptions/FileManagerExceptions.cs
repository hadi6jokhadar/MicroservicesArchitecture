using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;

namespace FileManager.Domain.Exceptions;

public class FileNotFoundException : NotFoundException
{
    public int FileId { get; }

    public FileNotFoundException(int fileId)
        : base(LocalizationKeys.Exceptions.FileNotFound)
    {
        FileId = fileId;
    }

    public FileNotFoundException(int fileId, ILocalizationService localizationService)
        : base(LocalizationKeys.Exceptions.FileNotFound, localizationService)
    {
        FileId = fileId;
    }
}

public class FileValidationException : BadRequestException
{
    public FileValidationException(string localizationKey)
        : base(localizationKey)
    {
    }

    public FileValidationException(string localizationKey, ILocalizationService localizationService)
        : base(localizationKey, localizationService)
    {
    }
}

public class FileStorageException : Exception
{
    public FileStorageException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
