using FileManager.API.Tests.Infrastructure;
using FileManager.Application.Commands;
using FileManager.Application.Interfaces;
using FileManager.Application.Queries;
using FileManager.Domain.Enums;
using FileManager.Domain.Exceptions;
using IhsanDev.Shared.Application.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FileManager.API.Tests.Endpoints;

/// <summary>
/// Integration tests for file upload and save operations
/// Tests use MediatR handlers directly to bypass .NET 9.0 PipeWriter bug
/// </summary>
[Collection("Sequential")]
public class SaveFileEndpointsTests : IntegrationTestBase
{
    public SaveFileEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
    {
        factory.UsePostgreSQL = true;
    }

    #region SaveFile Tests

    [Fact]
    public async Task SaveFile_WithValidData_ShouldReturnFileMetadata()
    {
        // Arrange
        using var fileStream = CreateTestFileStream("Test file content");
        var formFile = CreateFormFile(fileStream, "testfile.txt", "text/plain");
        var command = new SaveFileCommand(
            File: formFile,
            Group: FileGroup.Personal,
            UserId: 1
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.Name.Should().Contain("testfile");
        result.Extension.Should().Be(".txt");
        result.Group.Should().Be(FileGroup.Personal);
        result.Type.Should().Be(FileType.Other);
        result.UserId.Should().Be(1);
        result.Path.Should().NotBeNullOrEmpty();
        result.Url.Should().StartWith("https://localhost:5005");
    }

    [Fact]
    public async Task SaveFile_WithImageExtension_ShouldAutoDetectImageType()
    {
        // Arrange
        using var fileStream = CreateTestFileStream("Fake image content");
        var formFile = CreateFormFile(fileStream, "photo.jpg", "image/jpeg");
        var command = new SaveFileCommand(
            File: formFile,
            Group: FileGroup.Personal,
            UserId: 1
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Type.Should().Be(FileType.Image);
    }

    [Fact]
    public async Task SaveFile_WithMusicExtension_ShouldAutoDetectMusicType()
    {
        // Arrange
        using var fileStream = CreateTestFileStream("Fake audio content");
        var formFile = CreateFormFile(fileStream, "song.mp3", "audio/mpeg");
        var command = new SaveFileCommand(
            File: formFile,
            Group: FileGroup.Personal,
            UserId: 1
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Type.Should().Be(FileType.Music);
    }

    [Fact]
    public async Task SaveFile_WithAudioWebmContentType_ShouldAutoDetectMusicType()
    {
        // Arrange
        using var fileStream = CreateTestFileStream("Fake webm audio content");
        var formFile = CreateFormFile(fileStream, "song.webm", "audio/webm");
        var command = new SaveFileCommand(
            File: formFile,
            Group: FileGroup.Personal,
            UserId: 1
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Type.Should().Be(FileType.Music);
    }

    [Fact]
    public async Task SaveFile_WithVideoExtension_ShouldAutoDetectVideoType()
    {
        // Arrange
        using var fileStream = CreateTestFileStream("Fake video content");
        var formFile = CreateFormFile(fileStream, "video.mp4", "video/mp4");
        var command = new SaveFileCommand(
            File: formFile,
            Group: FileGroup.Personal,
            UserId: 1
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Type.Should().Be(FileType.Video);
    }

    [Fact]
    public async Task SaveFile_WithVideoWebmContentType_ShouldAutoDetectVideoType()
    {
        // Arrange
        using var fileStream = CreateTestFileStream("Fake webm video content");
        var formFile = CreateFormFile(fileStream, "video.webm", "video/webm");
        var command = new SaveFileCommand(
            File: formFile,
            Group: FileGroup.Personal,
            UserId: 1
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Type.Should().Be(FileType.Video);
    }

    [Fact]
    public async Task SaveFile_WithDifferentGroups_ShouldSaveCorrectly()
    {
        // Arrange & Act
        var groups = new[] { FileGroup.Personal, FileGroup.Shared, FileGroup.Project, FileGroup.Archive };
        var results = new List<FileManager.Application.DTOs.FileManagerResponse>();

        foreach (var group in groups)
        {
            using var fileStream = CreateTestFileStream($"{group} file content");
            var formFile = CreateFormFile(fileStream, $"{group.ToString().ToLower()}file.txt", "text/plain");
            var command = new SaveFileCommand(
                File: formFile,
                Group: group,
                UserId: 1
            );
            var result = await SendAsync(command);
            results.Add(result);
        }

        // Assert
        results.Should().HaveCount(4);
        results[0].Group.Should().Be(FileGroup.Personal);
        results[1].Group.Should().Be(FileGroup.Shared);
        results[2].Group.Should().Be(FileGroup.Project);
        results[3].Group.Should().Be(FileGroup.Archive);
    }

    [Fact]
    public async Task SaveFile_WithInvalidExtension_ShouldThrowBadRequestException()
    {
        // Arrange
        using var fileStream = CreateTestFileStream("Invalid file");
        var formFile = CreateFormFile(fileStream, "badfile.exe", "application/octet-stream");
        var command = new SaveFileCommand(
            File: formFile,
            Group: FileGroup.Personal,
            UserId: 1
        );

        // Act & Assert
        await FluentActions.Invoking(() => SendAsync(command))
            .Should().ThrowAsync<FileValidationException>()
            .WithMessage("*not allowed*");
    }

    [Fact]
    public async Task SaveFile_WithExcessiveSize_ShouldThrowBadRequestException()
    {
        // Arrange
        long maxSize = 104857600; // 100 MB
        using var fileStream = CreateTestFileStreamWithSize(maxSize + 1);
        var formFile = CreateFormFile(fileStream, "hugefile.txt", "text/plain");
        var command = new SaveFileCommand(
            File: formFile,
            Group: FileGroup.Personal,
            UserId: 1
        );

        // Act & Assert
        await FluentActions.Invoking(() => SendAsync(command))
            .Should().ThrowAsync<FileValidationException>()
            .WithMessage("*exceeds*");
    }

    [Fact]
    public async Task SaveFile_WithEmptyStream_ShouldThrowException()
    {
        // Arrange
        using var emptyStream = new MemoryStream();
        var formFile = CreateFormFile(emptyStream, "emptyfile.txt", "text/plain");
        var command = new SaveFileCommand(
            File: formFile,
            Group: FileGroup.Personal,
            UserId: 1
        );

        // Act & Assert
        await FluentActions.Invoking(() => SendAsync(command))
            .Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task SaveFile_CreatesUniqueFileName_ForDuplicateNames()
    {
        // Arrange
        using var fileStream1 = CreateTestFileStream("Content 1");
        using var fileStream2 = CreateTestFileStream("Content 2");
        
        var formFile1 = CreateFormFile(fileStream1, "duplicate.txt", "text/plain");
        var command1 = new SaveFileCommand(
            File: formFile1,
            Group: FileGroup.Personal,
            UserId: 1
        );

        var formFile2 = CreateFormFile(fileStream2, "duplicate.txt", "text/plain");
        var command2 = new SaveFileCommand(
            File: formFile2,
            Group: FileGroup.Personal,
            UserId: 1
        );

        // Act
        var result1 = await SendAsync(command1);
        var result2 = await SendAsync(command2);

        // Assert
        result1.Path.Should().NotBe(result2.Path);
        result1.Id.Should().NotBe(result2.Id);
    }

    [Fact]
    public async Task SaveFile_PersistsToDatabase_WithCorrectMetadata()
    {
        // Arrange
        using var fileStream = CreateTestFileStream("Database test content");
        var formFile = CreateFormFile(fileStream, "dbtest.txt", "text/plain");
        var command = new SaveFileCommand(
            File: formFile,
            Group: FileGroup.Personal,
            UserId: 1
        );

        // Act
        var result = await SendAsync(command);

        // Assert - Verify in database
        var fileFromDb = await ExecuteDbContextAsync(async context =>
        {
            return await context.FileManager.FirstOrDefaultAsync(f => f.Id == result.Id);
        });

        fileFromDb.Should().NotBeNull();
        fileFromDb!.Extension.Should().Be(".txt");
        fileFromDb.Group.Should().Be(FileGroup.Personal);
        fileFromDb.Type.Should().Be(FileType.Other);
        fileFromDb.UserId.Should().Be(1);
        fileFromDb.Status.Should().BeTrue();
        fileFromDb.IsArchived.Should().BeFalse();
    }

    [Fact]
    public async Task SaveFile_WithoutUserId_ShouldSaveSuccessfully()
    {
        // Arrange
        using var fileStream = CreateTestFileStream("Test content");
        var formFile = CreateFormFile(fileStream, "file.txt", "text/plain");
        var command = new SaveFileCommand(
            File: formFile,
            Group: FileGroup.Personal,
            UserId: null
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.UserId.Should().BeNull();
    }

    [Fact]
    public async Task SaveFile_WithPdfDocument_ShouldDetectOtherType()
    {
        // Arrange
        using var fileStream = CreateTestFileStream("Fake PDF content");
        var formFile = CreateFormFile(fileStream, "document.pdf", "application/pdf");
        var command = new SaveFileCommand(
            File: formFile,
            Group: FileGroup.Shared,
            UserId: 1
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Type.Should().Be(FileType.Other);
        result.Extension.Should().Be(".pdf");
        result.Group.Should().Be(FileGroup.Shared);
    }

    #endregion

    #region Physical File Storage Tests

    [Fact]
    public async Task SaveFile_ShouldCreatePhysicalFileOnDisk()
    {
        // Arrange
        var content = "Test file content for physical storage verification";
        using var fileStream = CreateTestFileStream(content);
        var formFile = CreateFormFile(fileStream, "physical-test.txt", "text/plain");
        var command = new SaveFileCommand(
            File: formFile,
            Group: FileGroup.Personal,
            UserId: 1
        );

        // Act
        var result = await SendAsync(command);

        // Assert - Verify database record
        result.Should().NotBeNull();
        result.Path.Should().NotBeNullOrEmpty();

        // Get the actual file path from database (not the URL)
        var relativePath = await ExecuteDbContextAsync(async context =>
        {
            var file = await context.FileManager.FirstOrDefaultAsync(f => f.Id == result.Id);
            return file!.Path;
        });

        // Assert - Verify physical file exists on disk
        using var scope = Factory.Services.CreateScope();
        var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        var exists = await fileStorage.ExistsAsync(relativePath);
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task SaveFile_ShouldPreserveOriginalFileContent()
    {
        // Arrange
        var originalContent = "This is the original file content with special chars: Hello World! 你好 🎉 #Test@123";
        using var fileStream = CreateTestFileStream(originalContent);
        var formFile = CreateFormFile(fileStream, "content-test.txt", "text/plain");
        var command = new SaveFileCommand(
            File: formFile,
            Group: FileGroup.Personal,
            UserId: 1
        );

        // Act
        var result = await SendAsync(command);

        // Get the actual file path from database
        var relativePath = await ExecuteDbContextAsync(async context =>
        {
            var file = await context.FileManager.FirstOrDefaultAsync(f => f.Id == result.Id);
            return file!.Path;
        });

        // Assert - Read file from disk and verify content matches
        using var scope = Factory.Services.CreateScope();
        var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        using var savedStream = await fileStorage.GetAsync(relativePath);
        using var reader = new StreamReader(savedStream);
        var savedContent = await reader.ReadToEndAsync();
        
        savedContent.Should().Be(originalContent);
    }

    [Fact]
    public async Task SaveFile_WithBinaryContent_ShouldPreserveByteIntegrity()
    {
        // Arrange - Create fake binary content (JPEG header + some data)
        var originalBytes = new byte[] { 
            0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 
            0x49, 0x46, 0x00, 0x01, 0x01, 0x01, 0x00, 0x48 
        };
        using var fileStream = new MemoryStream(originalBytes);
        var formFile = CreateFormFile(fileStream, "binary-test.jpg", "image/jpeg");
        var command = new SaveFileCommand(
            File: formFile,
            Group: FileGroup.Personal,
            UserId: 1
        );

        // Act
        var result = await SendAsync(command);

        // Get the actual file path from database
        var relativePath = await ExecuteDbContextAsync(async context =>
        {
            var file = await context.FileManager.FirstOrDefaultAsync(f => f.Id == result.Id);
            return file!.Path;
        });

        // Assert - Verify bytes match exactly
        using var scope = Factory.Services.CreateScope();
        var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        using var savedStream = await fileStorage.GetAsync(relativePath);
        using var memStream = new MemoryStream();
        await savedStream.CopyToAsync(memStream);
        var savedBytes = memStream.ToArray();
        
        savedBytes.Should().BeEquivalentTo(originalBytes);
        result.Size.Should().Be(originalBytes.Length);
    }

    [Fact]
    public async Task SaveFile_WithLargeFile_ShouldSaveCompletely()
    {
        // Arrange - Create 5MB file (smaller than 100MB limit for faster testing)
        var fileSize = 5 * 1024 * 1024; // 5MB
        var largeContent = new byte[fileSize];
        new Random(42).NextBytes(largeContent); // Seed for reproducibility
        using var fileStream = new MemoryStream(largeContent);
        var formFile = CreateFormFile(fileStream, "large-file.zip", "application/zip");
        var command = new SaveFileCommand(
            File: formFile,
            Group: FileGroup.Personal,
            UserId: 1
        );

        // Act
        var result = await SendAsync(command);

        // Assert - Verify size matches in database
        result.Size.Should().Be(fileSize);
        
        // Get the actual file path from database
        var relativePath = await ExecuteDbContextAsync(async context =>
        {
            var file = await context.FileManager.FirstOrDefaultAsync(f => f.Id == result.Id);
            return file!.Path;
        });

        // Assert - Verify complete file saved to disk with correct size
        using var scope = Factory.Services.CreateScope();
        var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        using var savedStream = await fileStorage.GetAsync(relativePath);
        using var memStream = new MemoryStream();
        await savedStream.CopyToAsync(memStream);
        memStream.Length.Should().Be(fileSize);
        
        // Verify first and last 100 bytes match (quick integrity check without comparing all bytes)
        var savedBytes = memStream.ToArray();
        savedBytes.Take(100).Should().BeEquivalentTo(largeContent.Take(100));
        savedBytes.TakeLast(100).Should().BeEquivalentTo(largeContent.TakeLast(100));
    }

    #endregion
}
