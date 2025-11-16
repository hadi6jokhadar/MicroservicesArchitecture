using FileManager.API.Tests.Infrastructure;
using FileManager.Application.Commands;
using FileManager.Domain.Enums;
using FileManager.Domain.Exceptions;
using IhsanDev.Shared.Application.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace FileManager.API.Tests.Endpoints;

/// <summary>
/// Integration tests for file update operations
/// </summary>
[Collection("Sequential")]
public class UpdateFileEndpointsTests : IntegrationTestBase
{
    public UpdateFileEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
    {
        factory.UsePostgreSQL = true;
    }

    #region UpdateFile Tests

    [Fact]
    public async Task UpdateFile_WithValidData_ShouldUpdateSuccessfully()
    {
        // Arrange
        var file = await CreateTestFileAsync(name: "original", group: FileGroup.Personal);
        var command = new UpdateFileCommand(
            Id: file.Id,
            Name: "updated",
            Group: FileGroup.Shared
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(file.Id);
        result.Name.Should().Be("updated");
        result.Group.Should().Be(FileGroup.Shared);
    }

    [Fact]
    public async Task UpdateFile_ChangingName_ShouldPersistToDatabase()
    {
        // Arrange
        var file = await CreateTestFileAsync(name: "oldname");
        var command = new UpdateFileCommand(
            Id: file.Id,
            Name: "newname",
            Group: file.Group
        );

        // Act
        await SendAsync(command);

        // Assert - Verify in database
        var updatedFile = await ExecuteDbContextAsync(async context =>
        {
            return await context.FileManager.FirstOrDefaultAsync(f => f.Id == file.Id);
        });

        updatedFile.Should().NotBeNull();
        updatedFile!.Name.Should().Be("newname");
        updatedFile.LastModified.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateFile_ChangingGroup_ShouldUpdateGroupOnly()
    {
        // Arrange
        var file = await CreateTestFileAsync(name: "myfile", group: FileGroup.Personal);
        var originalName = file.Name;
        
        var command = new UpdateFileCommand(
            Id: file.Id,
            Name: originalName,
            Group: FileGroup.Project
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Name.Should().Be(originalName);
        result.Group.Should().Be(FileGroup.Project);
    }

    [Fact]
    public async Task UpdateFile_WithNonExistingId_ShouldThrowNotFoundException()
    {
        // Arrange
        var command = new UpdateFileCommand(
            Id: 99999,
            Name: "updated",
            Group: FileGroup.Personal
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<FileManager.Domain.Exceptions.FileNotFoundException>(
            async () => await SendAsync(command)
        );
        
        exception.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task UpdateFile_WithArchivedFile_ShouldSucceed()
    {
        // Arrange
        var file = await CreateTestFileAsync(name: "archived");
        
        // Archive the file
        await ExecuteDbContextAsync(async context =>
        {
            var fileToArchive = await context.FileManager.FindAsync(file.Id);
            if (fileToArchive != null)
            {
                fileToArchive.IsArchived = true;
                await context.SaveChangesAsync();
            }
        });

        var command = new UpdateFileCommand(
            Id: file.Id,
            Name: "updated",
            Group: FileGroup.Personal
        );

        // Act
        var result = await SendAsync(command);

        // Assert - Service allows updating archived files
        result.Should().NotBeNull();
        result.Id.Should().Be(file.Id);
        result.Name.Should().Be("updated");
        result.Group.Should().Be(FileGroup.Personal);
    }

    [Fact]
    public async Task UpdateFile_WithNullName_ShouldKeepOriginalName()
    {
        // Arrange
        var file = await CreateTestFileAsync(name: "original");
        var command = new UpdateFileCommand(
            Id: file.Id,
            Name: null,
            Group: file.Group
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Name.Should().Be("original");
    }

    [Fact]
    public async Task UpdateFile_UpdatesLastModifiedTimestamp()
    {
        // Arrange
        var file = await CreateTestFileAsync(name: "timestamptest");
        var originalLastModified = file.LastModified;

        // Wait a bit to ensure timestamp difference
        await Task.Delay(100);

        var command = new UpdateFileCommand(
            Id: file.Id,
            Name: "updated",
            Group: file.Group
        );

        // Act
        await SendAsync(command);

        // Assert
        var updatedFile = await ExecuteDbContextAsync(async context =>
        {
            return await context.FileManager.FirstOrDefaultAsync(f => f.Id == file.Id);
        });

        updatedFile!.LastModified.Should().NotBeNull();
        updatedFile.LastModified.Should().BeAfter(file.Created);
    }

    [Fact]
    public async Task UpdateFile_AllGroups_ShouldWorkCorrectly()
    {
        // Arrange
        var file = await CreateTestFileAsync(name: "grouptest");
        var groups = new[] { FileGroup.Personal, FileGroup.Shared, FileGroup.Project, FileGroup.Archive };

        foreach (var group in groups)
        {
            // Act
            var command = new UpdateFileCommand(
                Id: file.Id,
                Name: file.Name,
                Group: group
            );
            var result = await SendAsync(command);

            // Assert
            result.Group.Should().Be(group);
        }
    }

    [Fact]
    public async Task UpdateFile_DoesNotChangeImmutableProperties()
    {
        // Arrange
        var file = await CreateTestFileAsync(
            name: "immutable",
            extension: ".txt",
            size: 2048,
            type: FileType.Other,
            userId: 1
        );

        var command = new UpdateFileCommand(
            Id: file.Id,
            Name: "updated",
            Group: FileGroup.Shared
        );

        // Act
        await SendAsync(command);

        // Assert - Verify immutable properties unchanged
        var updatedFile = await ExecuteDbContextAsync(async context =>
        {
            return await context.FileManager.FirstOrDefaultAsync(f => f.Id == file.Id);
        });

        updatedFile!.Extension.Should().Be(file.Extension);
        updatedFile.Size.Should().Be(file.Size);
        updatedFile.Type.Should().Be(file.Type);
        updatedFile.UserId.Should().Be(file.UserId);
        updatedFile.Path.Should().Be(file.Path);
        updatedFile.Created.Should().BeCloseTo(file.Created, TimeSpan.FromMilliseconds(1));
    }

    #endregion
}
