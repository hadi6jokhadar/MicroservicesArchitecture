namespace FileManager.Domain.Exceptions;

public class FileNotFoundException : Exception
{
    public FileNotFoundException(int fileId)
        : base($"File with ID {fileId} was not found.")
    {
    }
}

public class FileValidationException : Exception
{
    public FileValidationException(string message)
        : base(message)
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
