using Category.Domain.Entities;
using Category.Infrastructure.Persistence;
using IhsanDev.Shared.Kernel.Dto;
using IhsanDev.Shared.Testing.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Category.API.Tests.Infrastructure;

/// <summary>
/// Base class for Category API integration tests.
/// Provides helpers for seeding categories and sending MediatR commands/queries.
/// </summary>
public abstract class IntegrationTestBase :
    IhsanDev.Shared.Testing.Infrastructure.IntegrationTestBase<CategoryDbContext, Program>,
    IClassFixture<CustomWebApplicationFactory>
{
    protected IntegrationTestBase(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    /// <summary>
    /// Seed a root-level category and return the saved entity.
    /// </summary>
    protected async Task<CategoryEntity> CreateTestCategoryAsync(
        string? slug = null,
        Dictionary<string, string>? nameTranslations = null,
        int? parentId = null)
    {
        return await ExecuteDbContextAsync(async context =>
        {
            var translations = nameTranslations ?? new Dictionary<string, string>
            {
                ["en"] = "Test Category",
                ["ar"] = "فئة اختبار"
            };

            var entity = CategoryEntity.Create(
                slug: slug ?? $"test-cat-{Guid.NewGuid():N}",
                uri: slug ?? $"test-cat-{Guid.NewGuid():N}",
                nameTranslations: LocalizedMapping.From(translations),
                parentId: parentId
            );

            // Compute the materialized path before INSERT — mirrors what CreateCategoryCommandHandler does.
            string? parentPath = null;
            if (parentId.HasValue)
            {
                var parent = await context.Categories.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == parentId.Value);
                parentPath = parent?.Path;
                if (parent != null)
                    entity.SetHierarchy(parentId, parent.Path, parent.Depth);
            }
            entity.RecalculatePath(parentPath);

            context.Categories.Add(entity);
            await context.SaveChangesAsync();
            return entity;
        });
    }

    /// <summary>
    /// Returns a unique slug suitable for creation tests.
    /// </summary>
    protected static string UniqueSlug(string prefix = "cat") =>
        $"{prefix}-{Guid.NewGuid():N}".Substring(0, 20);
}
