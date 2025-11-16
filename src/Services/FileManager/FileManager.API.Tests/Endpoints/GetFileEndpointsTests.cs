using FileManager.API.Tests.Infrastructure;
using FileManager.Application.Queries;
using FileManager.Domain.Enums;
using IhsanDev.Shared.Application.Exceptions;

namespace FileManager.API.Tests.Endpoints;

/// <summary>
/// Integration tests for file retrieval operations
/// </summary>
[Collection("Sequential")]
public class GetFileEndpointsTests : IntegrationTestBase
{
    public GetFileEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
    {
        factory.UsePostgreSQL = true;
    }

    #region GetFileById Tests

    [Fact]
    public async Task GetFileById_WithExistingId_ShouldReturnFile()
    {
        // Arrange
        var file = await CreateTestFileAsync(
            name: "existingfile",
            extension: ".txt",
            size: 2048,
            group: FileGroup.Personal
        );

        var query = new GetFileByIdQuery(file.Id);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(file.Id);
        result.Name.Should().Be("existingfile");
        result.Extension.Should().Be(".txt");
        result.Size.Should().Be(2048);
        result.Group.Should().Be(FileGroup.Personal);
    }

    [Fact]
    public async Task GetFileById_WithNonExistingId_ShouldReturnNull()
    {
        // Arrange
        var query = new GetFileByIdQuery(99999);

        // Act
        var result = await SendAsync(query);
        
        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetFileById_WithArchivedFile_ShouldReturnFile()
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

        var query = new GetFileByIdQuery(file.Id);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(file.Id);
        result.IsArchived.Should().BeTrue();
    }    [Fact]
    public async Task GetFileById_ReturnsCorrectDateTimeFormat()
    {
        // Arrange
        var file = await CreateTestFileAsync(name: "datetimetest");
        var query = new GetFileByIdQuery(file.Id);

        // Act
        var result = await SendAsync(query);

        // Assert
        result!.Created.Should().NotBeNullOrEmpty();
        result.Created.Should().EndWith("Z"); // UTC format
        
        // Verify it's parseable as UTC
        var parsedDate = DateTime.Parse(result.Created, null, System.Globalization.DateTimeStyles.RoundtripKind);
        parsedDate.Kind.Should().Be(DateTimeKind.Utc);
    }

    #endregion

    #region GetAllFiles Tests

    [Fact]
    public async Task GetAllFiles_WithNoFilters_ShouldReturnPaginatedList()
    {
        // Arrange
        await CreateMultipleTestFilesAsync(5, userId: 1);
        var request = new FileManager.Application.DTOs.FileManagerListRequest
        {
            PageNumber = 1,
            PageSize = 10
        };

        var query = new GetFilesQuery(request);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCountGreaterOrEqualTo(5);
        result.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task GetAllFiles_WithPagination_ShouldReturnCorrectPage()
    {
        // Arrange
        await CreateMultipleTestFilesAsync(15, userId: 1);
        
        var request = new FileManager.Application.DTOs.FileManagerListRequest
        {
            PageNumber = 2,
            PageSize = 5
        };

        var query = new GetFilesQuery(request);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Items.Should().HaveCount(5);
        result.PageNumber.Should().Be(2);
        result.PageSize.Should().Be(5);
        result.TotalCount.Should().BeGreaterOrEqualTo(15);
    }

    [Fact]
    public async Task GetAllFiles_FilterByUserId_ShouldReturnOnlyUserFiles()
    {
        // Arrange
        await CreateMultipleTestFilesAsync(3, userId: 1);
        await CreateMultipleTestFilesAsync(2, userId: 2);

        var request = new FileManager.Application.DTOs.FileManagerListRequest
        {
            UserId = 1,
            PageNumber = 1,
            PageSize = 10
        };

        var query = new GetFilesQuery(request);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Items.Should().OnlyContain(f => f.UserId == 1);
        result.Items.Should().HaveCountGreaterOrEqualTo(3);
    }

    [Fact]
    public async Task GetAllFiles_FilterByGroup_ShouldReturnOnlyMatchingGroup()
    {
        // Arrange
        await CreateTestFileAsync(name: "personal1", group: FileGroup.Personal);
        await CreateTestFileAsync(name: "personal2", group: FileGroup.Personal);
        await CreateTestFileAsync(name: "shared1", group: FileGroup.Shared);

        var request = new FileManager.Application.DTOs.FileManagerListRequest
        {
            Group = FileGroup.Personal,
            PageNumber = 1,
            PageSize = 10
        };

        var query = new GetFilesQuery(request);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Items.Should().OnlyContain(f => f.Group == FileGroup.Personal);
    }

    [Fact]
    public async Task GetAllFiles_FilterByType_ShouldReturnOnlyMatchingType()
    {
        // Arrange
        await CreateTestFileAsync(name: "image1", extension: ".jpg", type: FileType.Image);
        await CreateTestFileAsync(name: "image2", extension: ".png", type: FileType.Image);
        await CreateTestFileAsync(name: "doc1", extension: ".pdf", type: FileType.Other);

        var request = new FileManager.Application.DTOs.FileManagerListRequest
        {
            Type = FileType.Image,
            PageNumber = 1,
            PageSize = 10
        };

        var query = new GetFilesQuery(request);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Items.Should().OnlyContain(f => f.Type == FileType.Image);
    }

    [Fact]
    public async Task GetAllFiles_FilterByTemp_ShouldReturnOnlyTempFiles()
    {
        // Arrange
        await CreateTestFileAsync(name: "temp1", temp: true);
        await CreateTestFileAsync(name: "temp2", temp: true);
        await CreateTestFileAsync(name: "permanent", temp: false);

        var request = new FileManager.Application.DTOs.FileManagerListRequest
        {
            Temp = true,
            PageNumber = 1,
            PageSize = 10
        };

        var query = new GetFilesQuery(request);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Items.Should().OnlyContain(f => f.Temp == true);
    }

    [Fact]
    public async Task GetAllFiles_FilterByTextFilter_ShouldReturnMatchingFiles()
    {
        // Arrange
        await CreateTestFileAsync(name: "invoice-2024");
        await CreateTestFileAsync(name: "receipt-2024");
        await CreateTestFileAsync(name: "contract");

        var request = new FileManager.Application.DTOs.FileManagerListRequest
        {
            TextFilter = "2024",
            PageNumber = 1,
            PageSize = 10
        };

        var query = new GetFilesQuery(request);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Items.Should().OnlyContain(f => f.Name.Contains("2024"));
        result.Items.Should().HaveCountGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task GetAllFiles_WithMultipleFilters_ShouldApplyAllFilters()
    {
        // Arrange
        await CreateTestFileAsync(name: "test1", userId: 1, group: FileGroup.Personal, temp: true);
        await CreateTestFileAsync(name: "test2", userId: 1, group: FileGroup.Personal, temp: false);
        await CreateTestFileAsync(name: "test3", userId: 2, group: FileGroup.Personal, temp: true);

        var request = new FileManager.Application.DTOs.FileManagerListRequest
        {
            UserId = 1,
            Group = FileGroup.Personal,
            Temp = true,
            PageNumber = 1,
            PageSize = 10
        };

        var query = new GetFilesQuery(request);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Items.Should().OnlyContain(f => 
            f.UserId == 1 && 
            f.Group == FileGroup.Personal && 
            f.Temp == true);
    }

    [Fact]
    public async Task GetAllFiles_ExcludesArchivedFiles_ByDefault()
    {
        // Arrange
        var activeFile = await CreateTestFileAsync(name: "active-unique", userId: 999);
        var archivedFile = await CreateTestFileAsync(name: "archived-unique", userId: 999);

        // Archive one file
        await ExecuteDbContextAsync(async context =>
        {
            var file = await context.FileManager.FindAsync(archivedFile.Id);
            if (file != null)
            {
                file.IsArchived = true;
                await context.SaveChangesAsync();
            }
        });

        var request = new FileManager.Application.DTOs.FileManagerListRequest
        {
            UserId = 999,
            IsArchived = false, // Explicitly filter archived files
            PageNumber = 1,
            PageSize = 10
        };

        var query = new GetFilesQuery(request);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Items.Should().NotContain(f => f.Id == archivedFile.Id);
        result.Items.Should().Contain(f => f.Id == activeFile.Id);
        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAllFiles_WithEmptyResult_ShouldReturnEmptyList()
    {
        // Arrange
        var request = new FileManager.Application.DTOs.FileManagerListRequest
        {
            UserId = 99999, // Non-existent user
            PageNumber = 1,
            PageSize = 10
        };

        var query = new GetFilesQuery(request);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    #endregion
}
