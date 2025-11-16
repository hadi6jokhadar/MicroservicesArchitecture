using FileManager.Domain.Entities;
using FileManager.Domain.Enums;
using FileManager.Infrastructure.Persistence;
using IhsanDev.Shared.Testing.Infrastructure;

namespace FileManager.API.Tests.Infrastructure;

/// <summary>
/// Base class for FileManager API integration tests
/// Inherits from shared testing base and adds FileManager-specific helpers
/// </summary>
public abstract class IntegrationTestBase : 
    IhsanDev.Shared.Testing.Infrastructure.IntegrationTestBase<FileManagerDbContext, Program>,
    IClassFixture<CustomWebApplicationFactory>
{
    protected IntegrationTestBase(CustomWebApplicationFactory factory) : base(factory)
    {
        // Note: Setting UsePostgreSQL here is too late - factory is already configured
        // To use PostgreSQL, override in CustomWebApplicationFactory constructor instead
    }

    /// <summary>
    /// Create a test file entity in the database
    /// </summary>
    protected async Task<FileManagerEntity> CreateTestFileAsync(
        string? name = null,
        string extension = ".txt",
        long size = 1024,
        string path = "/test/file.txt",
        FileGroup group = FileGroup.Personal,
        FileType type = FileType.Other,
        bool temp = false,
        int userId = 1)
    {
        return await ExecuteDbContextAsync(async context =>
        {
            var file = new FileManagerEntity
            {
                Name = name ?? GenerateUniqueString("testfile"),
                Extension = extension,
                Size = size,
                Path = path,
                Group = group,
                Type = type,
                Temp = temp,
                UserId = userId,
                Created = DateTime.UtcNow,
                IsArchived = false,
                Status = true
            };

            context.FileManager.Add(file);
            await context.SaveChangesAsync();
            return file;
        });
    }

    /// <summary>
    /// Create multiple test files
    /// </summary>
    protected async Task<List<FileManagerEntity>> CreateMultipleTestFilesAsync(
        int count,
        int userId = 1,
        bool temp = false)
    {
        var files = new List<FileManagerEntity>();
        for (int i = 0; i < count; i++)
        {
            var file = await CreateTestFileAsync(
                name: $"testfile-{i}",
                userId: userId,
                temp: temp
            );
            files.Add(file);
        }
        return files;
    }

    /// <summary>
    /// Create a temporary in-memory file stream for testing
    /// </summary>
    protected MemoryStream CreateTestFileStream(string content = "Test file content")
    {
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        writer.Write(content);
        writer.Flush();
        stream.Position = 0;
        return stream;
    }

    /// <summary>
    /// Create a test file stream with specific size
    /// </summary>
    protected MemoryStream CreateTestFileStreamWithSize(long sizeInBytes)
    {
        var stream = new MemoryStream();
        var buffer = new byte[sizeInBytes];
        stream.Write(buffer, 0, buffer.Length);
        stream.Position = 0;
        return stream;
    }

    /// <summary>
    /// Create a test IFormFile from stream
    /// </summary>
    protected Microsoft.AspNetCore.Http.IFormFile CreateFormFile(Stream stream, string fileName, string contentType = "application/octet-stream")
    {
        return new Microsoft.AspNetCore.Http.FormFile(stream, 0, stream.Length, "file", fileName)
        {
            Headers = new Microsoft.AspNetCore.Http.HeaderDictionary(),
            ContentType = contentType
        };
    }
}
