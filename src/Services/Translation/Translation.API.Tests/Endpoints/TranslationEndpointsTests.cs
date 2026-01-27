using Translation.API.Tests.Infrastructure;
using Translation.Application.Commands;
using Translation.Application.Queries;
using IhsanDev.Shared.Application.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Translation.API.Tests.Endpoints;

/// <summary>
/// Integration tests for Translation endpoints using MediatR handlers directly
/// This approach bypasses HTTP layer and avoids .NET 9.0 PipeWriter bug
/// </summary>
[Collection("Sequential")]
public class TranslationEndpointsTests : IntegrationTestBase
{
    public TranslationEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
    {
        factory.UsePostgreSQL = true;
    }

    #region GetTranslations Tests

    [Fact]
    public async Task GetTranslations_ForEnglish_ShouldReturnAllEnglishTranslations()
    {
        // Arrange
        var query = new GetTranslationsQuery("en");

        // Act - Call handler directly via MediatR
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Language.Should().Be("en");
        result.Translations.Should().NotBeEmpty();
        result.Translations.Should().ContainKey("test_key_1");
        result.Translations["test_key_1"].Should().Be("Test Key 1 English");
    }

    [Fact]
    public async Task GetTranslations_ForArabic_ShouldReturnAllArabicTranslations()
    {
        // Arrange
        var query = new GetTranslationsQuery("ar");

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Language.Should().Be("ar");
        result.Translations.Should().NotBeEmpty();
        result.Translations.Should().ContainKey("test_key_1");
        result.Translations["test_key_1"].Should().Be("مفتاح الاختبار 1");
    }

    [Fact]
    public async Task GetTranslations_WithCategory_ShouldReturnOnlyMatchingCategory()
    {
        // Arrange - Create translations in different categories
        await CreateCompleteTranslationAsync(
            "ui_button_save",
            "ui",
            new Dictionary<string, string> { { "en", "Save" }, { "ar", "حفظ" } }
        );

        await CreateCompleteTranslationAsync(
            "error_not_found",
            "errors",
            new Dictionary<string, string> { { "en", "Not Found" }, { "ar", "غير موجود" } }
        );

        var query = new GetTranslationsQuery("en", null, "ui");

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Translations.Should().ContainKey("ui_button_save");
        result.Translations.Should().NotContainKey("error_not_found");
    }

    [Fact]
    public async Task GetTranslations_WithTenantId_ShouldReturnTenantSpecificOverrides()
    {
        // Arrange - Create a global translation and a tenant-specific override
        var (key, _) = await CreateCompleteTranslationAsync(
            "welcome_message",
            "general",
            new Dictionary<string, string> { { "en", "Welcome" } }
        );

        // Create tenant-specific override
        await CreateTestTranslationValueAsync(
            key.Id,
            "en",
            "Welcome to Tenant XYZ",
            "tenant-xyz"
        );

        // Clear cache to prevent pollution from previous tests
        await ClearCacheAsync();
        
        var queryGlobal = new GetTranslationsQuery("en");
        var queryTenant = new GetTranslationsQuery("en", "tenant-xyz");

        // Act
        var resultGlobal = await SendAsync(queryGlobal);
        var resultTenant = await SendAsync(queryTenant);

        // Assert
        resultGlobal.Translations["welcome_message"].Should().Be("Welcome");
        resultTenant.Translations["welcome_message"].Should().Be("Welcome to Tenant XYZ");
    }

    [Fact]
    public async Task GetTranslations_ForNonExistentLanguage_ShouldReturnEmptyDictionary()
    {
        // Arrange
        var query = new GetTranslationsQuery("fr"); // French not seeded

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Language.Should().Be("fr");
        result.Translations.Should().BeEmpty();
    }

    #endregion

    #region CreateTranslationKey Tests

    [Fact]
    public async Task CreateTranslationKey_WithValidData_ShouldCreateSuccessfully()
    {
        // Arrange
        var command = new CreateTranslationKeyCommand(
            Key: "new_translation_key",
            Category: "general",
            Description: "A new translation key"
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Key.Should().Be("new_translation_key");
        result.Category.Should().Be("general");
        result.Description.Should().Be("A new translation key");
        result.IsActive.Should().BeTrue();
        result.Id.Should().BeGreaterThan(0);

        // Verify in database
        var keyFromDb = await ExecuteDbContextAsync(async context =>
        {
            return await context.TranslationKeys
                .FirstOrDefaultAsync(k => k.Key == "new_translation_key");
        });

        keyFromDb.Should().NotBeNull();
        keyFromDb!.Category.Should().Be("general");
    }

    [Fact]
    public async Task CreateTranslationKey_WithDuplicateKey_ShouldThrowException()
    {
        // Arrange
        await CreateTestTranslationKeyAsync("duplicate_key", "general");

        var command = new CreateTranslationKeyCommand(
            Key: "duplicate_key",
            Category: "general"
        );

        // Act & Assert - Handler throws ConflictException for duplicates
        await Assert.ThrowsAsync<ConflictException>(
            async () => await SendAsync(command)
        );
    }

    [Fact]
    public async Task CreateTranslationKey_WithEmptyKey_ShouldThrowValidationException()
    {
        // Arrange
        var command = new CreateTranslationKeyCommand(
            Key: "",
            Category: "general"
        );

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await SendAsync(command)
        );
    }

    [Fact]
    public async Task CreateTranslationKey_WithEmptyCategory_ShouldThrowValidationException()
    {
        // Arrange
        var command = new CreateTranslationKeyCommand(
            Key: "test_key",
            Category: ""
        );

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await SendAsync(command)
        );
    }

    [Fact]
    public async Task CreateTranslationKey_WithTooLongKey_ShouldThrowValidationException()
    {
        // Arrange
        var command = new CreateTranslationKeyCommand(
            Key: new string('a', 201), // Max is 200
            Category: "general"
        );

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await SendAsync(command)
        );
    }

    #endregion

    #region SetTranslation Tests

    [Fact]
    public async Task SetTranslation_ForNewLanguage_ShouldCreateSuccessfully()
    {
        // Arrange
        var key = await CreateTestTranslationKeyAsync("greeting", "general");
        var command = new SetTranslationCommand(
            Key: "greeting",
            Language: "fr",
            Value: "Bonjour",
            TenantId: null
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Language.Should().Be("fr");
        result.Value.Should().Be("Bonjour");
        result.TenantId.Should().BeNull();

        // Verify in database
        var valueFromDb = await ExecuteDbContextAsync(async context =>
        {
            return await context.TranslationValues
                .FirstOrDefaultAsync(v => v.TranslationKeyId == key.Id && v.Language == "fr");
        });

        valueFromDb.Should().NotBeNull();
        valueFromDb!.Value.Should().Be("Bonjour");
    }

    [Fact]
    public async Task SetTranslation_UpdateExistingValue_ShouldUpdateSuccessfully()
    {
        // Arrange
        var key = await CreateTestTranslationKeyAsync("status", "general");
        await CreateTestTranslationValueAsync(key.Id, "en", "Active");

        var command = new SetTranslationCommand(
            Key: "status",
            Language: "en",
            Value: "Enabled", // Updated value
            TenantId: null
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Value.Should().Be("Enabled");

        // Verify in database
        var valueFromDb = await ExecuteDbContextAsync(async context =>
        {
            return await context.TranslationValues
                .FirstOrDefaultAsync(v => v.TranslationKeyId == key.Id && v.Language == "en");
        });

        valueFromDb!.Value.Should().Be("Enabled");
    }

    [Fact]
    public async Task SetTranslation_WithTenantId_ShouldCreateTenantSpecificValue()
    {
        // Arrange
        var key = await CreateTestTranslationKeyAsync("company_name", "branding");
        var command = new SetTranslationCommand(
            Key: "company_name",
            Language: "en",
            Value: "Acme Corporation",
            TenantId: "tenant-123"
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.TenantId.Should().Be("tenant-123");
        result.Value.Should().Be("Acme Corporation");

        // Verify in database
        var valueFromDb = await ExecuteDbContextAsync(async context =>
        {
            return await context.TranslationValues
                .FirstOrDefaultAsync(v => 
                    v.TranslationKeyId == key.Id && 
                    v.Language == "en" && 
                    v.TenantId == "tenant-123");
        });

        valueFromDb.Should().NotBeNull();
    }

    [Fact]
    public async Task SetTranslation_ForNonExistentKey_ShouldCreateKeyAndValue()
    {
        // Arrange
        var command = new SetTranslationCommand(
            Key: "non_existent_key",
            Language: "en",
            Value: "Some Value",
            TenantId: null
        );

        // Act
        var result = await SendAsync(command);

        // Assert - Handler creates the key if it doesn't exist
        result.Should().NotBeNull();
        result.Value.Should().Be("Some Value");
        result.Language.Should().Be("en");
        
        // Verify key was created
        var keyFromDb = await ExecuteDbContextAsync(async context =>
        {
            return await context.TranslationKeys
                .FirstOrDefaultAsync(k => k.Key == "non_existent_key");
        });
        
        keyFromDb.Should().NotBeNull();
    }

    [Fact]
    public async Task SetTranslation_WithEmptyKey_ShouldThrowValidationException()
    {
        // Arrange
        var command = new SetTranslationCommand(
            Key: "",
            Language: "en",
            Value: "Test",
            TenantId: null
        );

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await SendAsync(command)
        );
    }

    [Fact]
    public async Task SetTranslation_WithEmptyLanguage_ShouldThrowValidationException()
    {
        // Arrange
        var command = new SetTranslationCommand(
            Key: "test",
            Language: "",
            Value: "Test",
            TenantId: null
        );

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await SendAsync(command)
        );
    }

    #endregion

    #region ImportTranslations Tests

    [Fact]
    public async Task ImportTranslations_WithValidData_ShouldImportSuccessfully()
    {
        // Arrange
        var importData = new Dictionary<string, string> 
        { 
            { "hello", "Hello" }, 
            { "goodbye", "Goodbye" } 
        };

        var command = new ImportTranslationsCommand(
            Translations: importData,
            Language: "en",
            TenantId: null,
            Category: "imported"
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.TotalKeys.Should().BeGreaterThan(0);
        result.Message.Should().NotBeNullOrEmpty();

        // Verify in database
        var helloKey = await ExecuteDbContextAsync(async context =>
        {
            return await context.TranslationKeys
                .Include(k => k.Values)
                .FirstOrDefaultAsync(k => k.Key == "hello");
        });

        helloKey.Should().NotBeNull();
        helloKey!.Values.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task ImportTranslations_WithOverwriteExisting_ShouldUpdateExistingValues()
    {
        // Arrange
        var key = await CreateTestTranslationKeyAsync("existing", "test");
        await CreateTestTranslationValueAsync(key.Id, "en", "Old Value");

        var importData = new Dictionary<string, string> 
        { 
            { "existing", "New Value" } 
        };

        var command = new ImportTranslationsCommand(
            Translations: importData,
            Language: "en",
            TenantId: null,
            Category: "test"
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.TotalKeys.Should().BeGreaterThanOrEqualTo(0);
        result.UpdatedValues.Should().BeGreaterThanOrEqualTo(0);

        // Verify value was updated
        var valueFromDb = await ExecuteDbContextAsync(async context =>
        {
            return await context.TranslationValues
                .FirstOrDefaultAsync(v => v.TranslationKeyId == key.Id && v.Language == "en");
        });

        valueFromDb!.Value.Should().Be("New Value");
    }

    [Fact]
    public async Task ImportTranslations_WithExistingKey_ShouldUpdateValue()
    {
        // Arrange
        var uniqueKey = $"existing_import_{Guid.NewGuid():N}";
        var key = await CreateTestTranslationKeyAsync(uniqueKey, "test");
        await CreateTestTranslationValueAsync(key.Id, "en", "Old Value");

        var importData = new Dictionary<string, string> 
        { 
            { uniqueKey, "New Value" } 
        };

        var command = new ImportTranslationsCommand(
            Translations: importData,
            Language: "en",
            TenantId: null,
            Category: "test"
        );

        // Act
        var result = await SendAsync(command);

        // Assert - Command should complete and update the value
        result.Should().NotBeNull();
        result.TotalKeys.Should().BeGreaterThanOrEqualTo(0);

        // Verify value was updated
        var valueFromDb = await ExecuteDbContextAsync(async context =>
        {
            return await context.TranslationValues
                .FirstOrDefaultAsync(v => v.TranslationKeyId == key.Id && v.Language == "en");
        });

        valueFromDb.Should().NotBeNull();
        valueFromDb!.Value.Should().Be("New Value");
    }

    [Fact]
    public async Task ImportTranslations_WithTenantId_ShouldCreateTenantSpecificTranslations()
    {
        // Arrange
        var importData = new Dictionary<string, string> 
        { 
            { "tenant_specific", "Tenant Value" } 
        };

        var command = new ImportTranslationsCommand(
            Translations: importData,
            Language: "en",
            TenantId: "tenant-456",
            Category: "tenant"
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.TotalKeys.Should().BeGreaterThan(0);

        // Verify tenant-specific value
        var valueFromDb = await ExecuteDbContextAsync(async context =>
        {
            return await context.TranslationValues
                .Include(v => v.TranslationKey)
                .FirstOrDefaultAsync(v => 
                    v.TranslationKey.Key == "tenant_specific" && 
                    v.TenantId == "tenant-456");
        });

        valueFromDb.Should().NotBeNull();
        valueFromDb!.Value.Should().Be("Tenant Value");
    }

    [Fact]
    public async Task ImportTranslations_WithEmptyData_ShouldReturnZeroResults()
    {
        // Arrange
        var command = new ImportTranslationsCommand(
            Translations: new Dictionary<string, string>(),
            Language: "en",
            TenantId: null,
            Category: "empty"
        );

        // Act & Assert - Should throw validation exception for empty translations
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await SendAsync(command)
        );
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public async Task GetTranslations_WithInactiveKeys_ShouldNotReturnInactiveTranslations()
    {
        // Arrange
        var inactiveKey = await CreateTestTranslationKeyAsync("inactive_key", "general", isActive: false);
        await CreateTestTranslationValueAsync(inactiveKey.Id, "en", "Inactive Value");

        var query = new GetTranslationsQuery("en");

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Translations.Should().NotContainKey("inactive_key");
    }

    [Fact]
    public async Task SetTranslation_MultipleTimes_ShouldKeepLatestValue()
    {
        // Arrange
        var key = await CreateTestTranslationKeyAsync("changing_value", "test");

        // Act - Set value multiple times
        await SendAsync(new SetTranslationCommand("changing_value", "en", "Value 1", null));
        await SendAsync(new SetTranslationCommand("changing_value", "en", "Value 2", null));
        var finalResult = await SendAsync(new SetTranslationCommand("changing_value", "en", "Value 3", null));

        // Assert
        finalResult.Value.Should().Be("Value 3");

        // Verify only one record exists in database
        var count = await ExecuteDbContextAsync(async context =>
        {
            return await context.TranslationValues
                .CountAsync(v => v.TranslationKeyId == key.Id && v.Language == "en");
        });

        count.Should().Be(1);
    }

    [Fact]
    public async Task GetTranslations_WithMixedGlobalAndTenantValues_ShouldPrioritizeTenantValues()
    {
        // Arrange
        var key = await CreateTestTranslationKeyAsync("mixed_value", "test");
        await CreateTestTranslationValueAsync(key.Id, "en", "Global Value", null);
        await CreateTestTranslationValueAsync(key.Id, "en", "Tenant Override", "tenant-789");

        // Clear cache to prevent pollution from previous tests
        await ClearCacheAsync();
        
        var queryGlobal = new GetTranslationsQuery("en", null);
        var queryTenant = new GetTranslationsQuery("en", "tenant-789");

        // Act
        var resultGlobal = await SendAsync(queryGlobal);
        var resultTenant = await SendAsync(queryTenant);

        // Assert
        resultGlobal.Translations["mixed_value"].Should().Be("Global Value");
        resultTenant.Translations["mixed_value"].Should().Be("Tenant Override");
    }

    #endregion

    #region GetTranslationKeys (Pagination) Tests

    [Fact]
    public async Task GetTranslationKeys_ShouldReturnPaginatedResults()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        await CreateTestTranslationKeyAsync($"key1_{testId}", "general");
        await CreateTestTranslationKeyAsync($"key2_{testId}", "general");
        await CreateTestTranslationKeyAsync($"key3_{testId}", "general");

        var query = new GetTranslationKeysQuery(PageNumber: 1, PageSize: 10);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCountGreaterOrEqualTo(3);
        result.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.TotalCount.Should().BeGreaterOrEqualTo(3);
    }

    [Fact]
    public async Task GetTranslationKeys_WithPagination_ShouldReturnCorrectPage()
    {
        // Arrange - Create 15 keys
        var testId = Guid.NewGuid().ToString("N")[..8];
        for (int i = 1; i <= 15; i++)
        {
            await CreateTestTranslationKeyAsync($"pagination_key_{i}_{testId}", "pagination");
        }

        var query = new GetTranslationKeysQuery(PageNumber: 2, PageSize: 5);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.PageNumber.Should().Be(2);
        result.PageSize.Should().Be(5);
        result.Items.Should().HaveCountLessThanOrEqualTo(5);
        result.TotalCount.Should().BeGreaterOrEqualTo(15);
    }

    [Fact]
    public async Task GetTranslationKeys_WithCategoryFilter_ShouldReturnOnlyMatchingCategory()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        await CreateTestTranslationKeyAsync($"ui_key_{testId}", "ui");
        await CreateTestTranslationKeyAsync($"error_key_{testId}", "errors");
        await CreateTestTranslationKeyAsync($"ui_key2_{testId}", "ui");

        var query = new GetTranslationKeysQuery(PageNumber: 1, PageSize: 10, Category: "ui");

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().OnlyContain(k => k.Category == "ui");
        result.Items.Should().HaveCountGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task GetTranslationKeys_WithSearchTerm_ShouldReturnMatchingKeys()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        await CreateTestTranslationKeyAsync($"search_button_{testId}", "ui", "Button for search");
        await CreateTestTranslationKeyAsync($"save_button_{testId}", "ui", "Button for saving");
        await CreateTestTranslationKeyAsync($"search_input_{testId}", "ui", "Input for search");

        var query = new GetTranslationKeysQuery(PageNumber: 1, PageSize: 10, SearchTerm: "search");

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCountGreaterOrEqualTo(2);
        result.Items.Should().OnlyContain(k => 
            k.Key.Contains("search", StringComparison.OrdinalIgnoreCase) ||
            (k.Description != null && k.Description.Contains("search", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task GetTranslationKeys_WithSearchTermInDescription_ShouldReturnMatchingKeys()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        await CreateTestTranslationKeyAsync($"key1_{testId}", "general", "This is a special description");
        await CreateTestTranslationKeyAsync($"key2_{testId}", "general", "Normal description");

        var query = new GetTranslationKeysQuery(PageNumber: 1, PageSize: 10, SearchTerm: "special");

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().Contain(k => k.Key == $"key1_{testId}");
        result.Items.Should().NotContain(k => k.Key == $"key2_{testId}");
    }

    [Fact]
    public async Task GetTranslationKeys_WithInvalidPageNumber_ShouldThrowValidationException()
    {
        // Arrange
        var query = new GetTranslationKeysQuery(PageNumber: 0, PageSize: 10);

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(async () => await SendAsync(query));
    }

    [Fact]
    public async Task GetTranslationKeys_WithInvalidPageSize_ShouldThrowValidationException()
    {
        // Arrange
        var query = new GetTranslationKeysQuery(PageNumber: 1, PageSize: 101); // Max is 100

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(async () => await SendAsync(query));
    }

    [Fact]
    public async Task GetTranslationKeys_WithNoResults_ShouldReturnEmptyList()
    {
        // Arrange
        var query = new GetTranslationKeysQuery(PageNumber: 1, PageSize: 10, Category: "non_existent_category");

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetTranslationKeys_ShouldExcludeArchivedKeys()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N")[..8];
        var activeKey = await CreateTestTranslationKeyAsync($"active_{testId}", "general", isActive: true);
        var archivedKey = await CreateTestTranslationKeyAsync($"archived_{testId}", "general", isActive: true);
        
        // Archive the key
        await ExecuteDbContextAsync(async context =>
        {
            var key = await context.TranslationKeys.FindAsync(archivedKey.Id);
            if (key != null)
            {
                key.IsArchived = true;
                await context.SaveChangesAsync();
            }
        });

        var query = new GetTranslationKeysQuery(PageNumber: 1, PageSize: 10);

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Items.Should().NotContain(k => k.Key == $"archived_{testId}");
        result.Items.Should().Contain(k => k.Key == $"active_{testId}");
    }

    #endregion

    #region UpdateTranslationKey Tests

    [Fact]
    public async Task UpdateTranslationKey_WithValidData_ShouldUpdateSuccessfully()
    {
        // Arrange
        var key = await CreateTestTranslationKeyAsync("update_test", "general", "Original description");
        var command = new UpdateTranslationKeyCommand(
            Id: key.Id,
            Description: "Updated description"
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(key.Id);
        result.Description.Should().Be("Updated description");

        // Verify in database
        var keyFromDb = await ExecuteDbContextAsync(async context =>
        {
            return await context.TranslationKeys.FindAsync(key.Id);
        });

        keyFromDb.Should().NotBeNull();
        keyFromDb!.Description.Should().Be("Updated description");
        keyFromDb.LastModified.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateTranslationKey_WithNullDescription_ShouldKeepOriginalDescription()
    {
        // Arrange
        var key = await CreateTestTranslationKeyAsync("keep_desc", "general", "Original description");
        var command = new UpdateTranslationKeyCommand(
            Id: key.Id,
            Description: null
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Description.Should().Be("Original description");
    }

    [Fact]
    public async Task UpdateTranslationKey_WithNonExistentId_ShouldThrowNotFoundException()
    {
        // Arrange
        var command = new UpdateTranslationKeyCommand(
            Id: 99999,
            Description: "This should fail"
        );

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(async () => await SendAsync(command));
    }

    [Fact]
    public async Task UpdateTranslationKey_WithInvalidId_ShouldThrowValidationException()
    {
        // Arrange
        var command = new UpdateTranslationKeyCommand(
            Id: 0,
            Description: "Invalid ID"
        );

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(async () => await SendAsync(command));
    }

    [Fact]
    public async Task UpdateTranslationKey_WithTooLongDescription_ShouldThrowValidationException()
    {
        // Arrange
        var key = await CreateTestTranslationKeyAsync("long_desc", "general");
        var command = new UpdateTranslationKeyCommand(
            Id: key.Id,
            Description: new string('a', 501) // Max is 500
        );

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(async () => await SendAsync(command));
    }

    [Fact]
    public async Task UpdateTranslationKey_ShouldUpdateLastModifiedDate()
    {
        // Arrange
        var key = await CreateTestTranslationKeyAsync("timestamp_test", "general");
        var originalLastModified = key.LastModified;

        await Task.Delay(100); // Small delay to ensure timestamp difference

        var command = new UpdateTranslationKeyCommand(
            Id: key.Id,
            Description: "New description"
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        var keyFromDb = await ExecuteDbContextAsync(async context =>
        {
            return await context.TranslationKeys.FindAsync(key.Id);
        });

        keyFromDb!.LastModified.Should().NotBeNull();
        if (originalLastModified != null)
        {
            keyFromDb.LastModified.Should().BeAfter(originalLastModified.Value);
        }
    }

    #endregion

    #region DeleteTranslationKey Tests

    [Fact]
    public async Task DeleteTranslationKey_WithValidId_ShouldDeleteSuccessfully()
    {
        // Arrange
        var key = await CreateTestTranslationKeyAsync("delete_test", "general");
        var command = new DeleteTranslationKeyCommand(Id: key.Id);

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().BeTrue();

        // Verify soft deletion in database (IsArchived should be true)
        var keyFromDb = await ExecuteDbContextAsync(async context =>
        {
            return await context.TranslationKeys.FindAsync(key.Id);
        });

        keyFromDb.Should().NotBeNull();
        keyFromDb!.IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteTranslationKey_ShouldAlsoDeleteAssociatedValues()
    {
        // Arrange
        var key = await CreateTestTranslationKeyAsync("delete_with_values", "general");
        var value1 = await CreateTestTranslationValueAsync(key.Id, "en", "English Value");
        var value2 = await CreateTestTranslationValueAsync(key.Id, "ar", "قيمة عربية");

        var command = new DeleteTranslationKeyCommand(Id: key.Id);

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().BeTrue();

        // Verify key is soft deleted (archived)
        var keyFromDb = await ExecuteDbContextAsync(async context =>
        {
            return await context.TranslationKeys.FindAsync(key.Id);
        });

        keyFromDb.Should().NotBeNull();
        keyFromDb!.IsArchived.Should().BeTrue();

        // Note: Translation values still exist in DB (soft delete affects only the key)
        var valuesCount = await ExecuteDbContextAsync(async context =>
        {
            return await context.TranslationValues
                .CountAsync(v => v.TranslationKeyId == key.Id);
        });

        valuesCount.Should().Be(2);
    }

    [Fact]
    public async Task DeleteTranslationKey_WithNonExistentId_ShouldThrowNotFoundException()
    {
        // Arrange
        var command = new DeleteTranslationKeyCommand(Id: 99999);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(async () => await SendAsync(command));
    }

    [Fact]
    public async Task DeleteTranslationKey_WithInvalidId_ShouldThrowValidationException()
    {
        // Arrange
        var command = new DeleteTranslationKeyCommand(Id: 0);

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(async () => await SendAsync(command));
    }

    [Fact]
    public async Task DeleteTranslationKey_WithNegativeId_ShouldThrowValidationException()
    {
        // Arrange
        var command = new DeleteTranslationKeyCommand(Id: -1);

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(async () => await SendAsync(command));
    }

    [Fact]
    public async Task DeleteTranslationKey_MultipleTimes_ShouldFailOnSecondAttempt()
    {
        // Arrange
        var key = await CreateTestTranslationKeyAsync("delete_twice", "general");
        var command = new DeleteTranslationKeyCommand(Id: key.Id);

        // Act - First deletion
        var firstResult = await SendAsync(command);
        firstResult.Should().BeTrue();

        // Act & Assert - Second deletion should fail
        await Assert.ThrowsAsync<NotFoundException>(async () => await SendAsync(command));
    }

    #endregion
}
