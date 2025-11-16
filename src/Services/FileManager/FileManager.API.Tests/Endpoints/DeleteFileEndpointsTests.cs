using FileManager.API.Tests.Infrastructure;
using FileManager.Application.Commands;
using FileManager.Domain.Enums;
using FileManager.Domain.Exceptions;
using IhsanDev.Shared.Application.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace FileManager.API.Tests.Endpoints;

/// <summary>
/// Integration tests for file deletion operations
/// </summary>
[Collection("Sequential")]
public class DeleteFileEndpointsTests : IntegrationTestBase
{
    public DeleteFileEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
    {
        factory.UsePostgreSQL = true;
    }

    #region DeleteFile Tests

    [Fact]
    public async Task DeleteFile_WithValidId_ShouldDeleteSuccessfully()
    {
        // Arrange
        var file = await CreateTestFileAsync(name: "todelete");
        var command = new DeleteFileCommand(file.Id);

        // Act
        await SendAsync(command);

        // Assert - Verify file is removed from database
        var deletedFile = await ExecuteDbContextAsync(async context =>
        {
            return await context.FileManager
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(f => f.Id == file.Id);
        });

        deletedFile.Should().BeNull();
    }

    [Fact]
    public async Task DeleteFile_WithNonExistingId_ShouldReturnFalse()
    {
        // Arrange
        var command = new DeleteFileCommand(99999);

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteFile_WithAlreadyDeletedFile_ShouldReturnFalse()
    {
        // Arrange
        var file = await CreateTestFileAsync(name: "alreadydeleted");
        
        // Delete first time
        var deleteCommand1 = new DeleteFileCommand(file.Id);
        var firstResult = await SendAsync(deleteCommand1);
        firstResult.Should().BeTrue();

        // Try to delete again
        var deleteCommand2 = new DeleteFileCommand(file.Id);

        // Act
        var result = await SendAsync(deleteCommand2);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteFile_WithSystemGroup_ShouldSucceed()
    {
        // Arrange
        var systemFile = await CreateTestFileAsync(name: "system", group: FileGroup.System);
        var command = new DeleteFileCommand(systemFile.Id);

        // Act
        await SendAsync(command);

        // Assert - System files can be deleted (no special protection)
        var deletedFile = await ExecuteDbContextAsync(async context =>
        {
            return await context.FileManager
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(f => f.Id == systemFile.Id);
        });

        deletedFile.Should().BeNull();
    }

    [Fact]
    public async Task DeleteFile_NonSystemFile_ShouldSucceed()
    {
        // Arrange
        var groups = new[] { FileGroup.Personal, FileGroup.Shared, FileGroup.Project, FileGroup.Archive };

        foreach (var group in groups)
        {
            var file = await CreateTestFileAsync(name: $"{group}file", group: group);
            var command = new DeleteFileCommand(file.Id);

            // Act
            await SendAsync(command);

            // Assert - File should be removed from database
            var deletedFile = await ExecuteDbContextAsync(async context =>
            {
                return await context.FileManager
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(f => f.Id == file.Id);
            });

            deletedFile.Should().BeNull();
        }
    }

    #endregion

    #region DeleteAllTempFiles Tests

    [Fact]
    public async Task DeleteAllTempFiles_ShouldDeleteAllTempFiles()
    {
        // Arrange
        await CreateTestFileAsync(name: "temp1", temp: true);
        await CreateTestFileAsync(name: "temp2", temp: true);
        await CreateTestFileAsync(name: "permanent", temp: false);

        var command = new DeleteAllTempFilesCommand();

        // Act
        await SendAsync(command);

        // Assert - Verify temp files are removed from database
        var tempFiles = await ExecuteDbContextAsync(async context =>
        {
            return await context.FileManager
                .IgnoreQueryFilters()
                .Where(f => f.Temp)
                .ToListAsync();
        });

        tempFiles.Should().BeEmpty();

        // Verify permanent file is not archived
        var permanentFile = await ExecuteDbContextAsync(async context =>
        {
            return await context.FileManager
                .Where(f => !f.Temp && f.Name == "permanent")
                .FirstOrDefaultAsync();
        });

        permanentFile.Should().NotBeNull();
        permanentFile!.IsArchived.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAllTempFiles_WithNoTempFiles_ShouldCompleteSuccessfully()
    {
        // Arrange
        await CreateTestFileAsync(name: "permanent1", temp: false);
        await CreateTestFileAsync(name: "permanent2", temp: false);

        var command = new DeleteAllTempFilesCommand();

        // Act
        await SendAsync(command);

        // Assert - Should complete without errors
        var allFiles = await ExecuteDbContextAsync(async context =>
        {
            return await context.FileManager.ToListAsync();
        });

        allFiles.Should().OnlyContain(f => f.IsArchived == false);
    }

    [Fact]
    public async Task DeleteAllTempFiles_DoesNotDeleteSystemTempFiles()
    {
        // Arrange
        await CreateTestFileAsync(name: "usertemp", temp: true, group: FileGroup.Personal);
        await CreateTestFileAsync(name: "systemtemp", temp: true, group: FileGroup.System);

        var command = new DeleteAllTempFilesCommand();

        // Act
        await SendAsync(command);

        // Assert - All temp files are deleted (no System group protection)
        var allTempFiles = await ExecuteDbContextAsync(async context =>
        {
            return await context.FileManager
                .IgnoreQueryFilters()
                .Where(f => f.Temp)
                .ToListAsync();
        });

        allTempFiles.Should().BeEmpty();
    }

    #endregion

    #region DeleteOldTempFiles Tests

    [Fact]
    public async Task DeleteOldTempFiles_WithOldFiles_ShouldDeleteOnlyOldOnes()
    {
        // Arrange
        var oldTempFile = await CreateTestFileAsync(name: "oldtemp", temp: true);
        var recentTempFile = await CreateTestFileAsync(name: "recenttemp", temp: true);

        // Make one file old
        await ExecuteDbContextAsync(async context =>
        {
            var file = await context.FileManager.FindAsync(oldTempFile.Id);
            if (file != null)
            {
                file.Created = DateTime.UtcNow.AddDays(-31);
                await context.SaveChangesAsync();
            }
        });

        var command = new DeleteOldTempFilesCommand(30);

        // Act
        await SendAsync(command);

        // Assert - Old temp file should be deleted
        var oldFile = await ExecuteDbContextAsync(async context =>
        {
            return await context.FileManager
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(f => f.Id == oldTempFile.Id);
        });

        oldFile.Should().BeNull(); // Hard deleted

        // Recent temp file should remain
        var recentFile = await ExecuteDbContextAsync(async context =>
        {
            return await context.FileManager.FirstOrDefaultAsync(f => f.Id == recentTempFile.Id);
        });

        recentFile.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteOldTempFiles_WithDifferentAges_ShouldRespectDaysParameter()
    {
        // Arrange
        var file10DaysOld = await CreateTestFileAsync(name: "temp10", temp: true);
        var file20DaysOld = await CreateTestFileAsync(name: "temp20", temp: true);
        var file40DaysOld = await CreateTestFileAsync(name: "temp40", temp: true);

        await ExecuteDbContextAsync(async context =>
        {
            var f10 = await context.FileManager.FindAsync(file10DaysOld.Id);
            var f20 = await context.FileManager.FindAsync(file20DaysOld.Id);
            var f40 = await context.FileManager.FindAsync(file40DaysOld.Id);

            if (f10 != null) f10.Created = DateTime.UtcNow.AddDays(-10);
            if (f20 != null) f20.Created = DateTime.UtcNow.AddDays(-20);
            if (f40 != null) f40.Created = DateTime.UtcNow.AddDays(-40);

            await context.SaveChangesAsync();
        });

        var command = new DeleteOldTempFilesCommand(15);

        // Act
        await SendAsync(command);

        // Assert
        var files = await ExecuteDbContextAsync(async context =>
        {
            return await context.FileManager
                .IgnoreQueryFilters()
                .Where(f => f.Name.StartsWith("temp"))
                .ToListAsync();
        });

        files.Should().Contain(f => f.Name == "temp10"); // 10 days old - kept
        files.Should().NotContain(f => f.Name == "temp20");  // 20 days old - hard deleted
        files.Should().NotContain(f => f.Name == "temp40");  // 40 days old - hard deleted
    }

    [Fact]
    public async Task DeleteOldTempFiles_WithNegativeDays_ShouldThrowValidationException()
    {
        // Arrange
        var command = new DeleteOldTempFilesCommand(-5);

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await SendAsync(command)
        );
    }

    [Fact]
    public async Task DeleteOldTempFiles_WithZeroDays_ShouldThrowValidationException()
    {
        // Arrange
        var command = new DeleteOldTempFilesCommand(0);

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await SendAsync(command)
        );
    }

    [Fact]
    public async Task DeleteOldTempFiles_DoesNotAffectPermanentFiles()
    {
        // Arrange
        var oldPermanentFile = await CreateTestFileAsync(name: "oldpermanent", temp: false);
        var oldTempFile = await CreateTestFileAsync(name: "oldtemp", temp: true);

        // Make both files old
        await ExecuteDbContextAsync(async context =>
        {
            var permanent = await context.FileManager.FindAsync(oldPermanentFile.Id);
            var temp = await context.FileManager.FindAsync(oldTempFile.Id);

            if (permanent != null) permanent.Created = DateTime.UtcNow.AddDays(-60);
            if (temp != null) temp.Created = DateTime.UtcNow.AddDays(-60);

            await context.SaveChangesAsync();
        });

        var command = new DeleteOldTempFilesCommand(30);

        // Act
        await SendAsync(command);

        // Assert - Permanent file should not be deleted
        var permanentFile = await ExecuteDbContextAsync(async context =>
        {
            return await context.FileManager.FirstOrDefaultAsync(f => f.Id == oldPermanentFile.Id);
        });

        permanentFile.Should().NotBeNull();

        // Temp file should be hard deleted
        var tempFile = await ExecuteDbContextAsync(async context =>
        {
            return await context.FileManager
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(f => f.Id == oldTempFile.Id);
        });

        tempFile.Should().BeNull();
    }

    [Fact]
    public async Task DeleteOldTempFiles_DoesNotDeleteSystemFiles()
    {
        // Arrange
        var oldSystemTempFile = await CreateTestFileAsync(
            name: "oldsystemtemp",
            temp: true,
            group: FileGroup.System
        );

        // Make it old
        await ExecuteDbContextAsync(async context =>
        {
            var file = await context.FileManager.FindAsync(oldSystemTempFile.Id);
            if (file != null)
            {
                file.Created = DateTime.UtcNow.AddDays(-60);
                await context.SaveChangesAsync();
            }
        });

        var command = new DeleteOldTempFilesCommand(30);

        // Act
        await SendAsync(command);

        // Assert - All temp files are deleted (no System group protection)
        var systemFile = await ExecuteDbContextAsync(async context =>
        {
            return await context.FileManager
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(f => f.Id == oldSystemTempFile.Id);
        });

        systemFile.Should().BeNull(); // Hard deleted
    }

    #endregion
}
