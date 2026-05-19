using IhsanDev.Shared.Application.Common.Interfaces;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using Microsoft.Extensions.Logging;
using Category.Application.DTOs;

namespace Category.Application.Helpers;

public class CategoryFileManagerHelper
{
    private readonly IFileManagerServiceClient _fileManagerClient;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<CategoryFileManagerHelper> _logger;

    public CategoryFileManagerHelper(
        IFileManagerServiceClient fileManagerClient,
        ITenantContext tenantContext,
        ILogger<CategoryFileManagerHelper> logger)
    {
        _fileManagerClient = fileManagerClient;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>Enriches a single CategoryDto with its icon and image file metadata.</summary>
    public async Task EnrichCategoryWithFilesAsync(CategoryDto dto, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId;

        var fileIds = new List<int>();
        if (dto.IconFileId.HasValue) fileIds.Add(dto.IconFileId.Value);
        if (dto.ImageFileId.HasValue && dto.ImageFileId != dto.IconFileId) fileIds.Add(dto.ImageFileId.Value);

        if (fileIds.Count == 0) return;

        try
        {
            var filesDict = await _fileManagerClient.GetFilesByIdsAsync(fileIds, tenantId, cancellationToken);

            if (dto.IconFileId.HasValue && filesDict.TryGetValue(dto.IconFileId.Value, out var iconFile))
                dto.IconFile = iconFile;
            else if (dto.IconFileId.HasValue)
                _logger.LogWarning("IconFile {FileId} not found for Category {CategoryId}", dto.IconFileId.Value, dto.Id);

            if (dto.ImageFileId.HasValue && filesDict.TryGetValue(dto.ImageFileId.Value, out var imageFile))
                dto.ImageFile = imageFile;
            else if (dto.ImageFileId.HasValue)
                _logger.LogWarning("ImageFile {FileId} not found for Category {CategoryId}", dto.ImageFileId.Value, dto.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch files for Category {CategoryId}", dto.Id);
        }
    }

    /// <summary>Enriches a flat list of CategoryDtos with file metadata using a single batch request.</summary>
    public async Task EnrichCategoriesWithFilesAsync(IEnumerable<CategoryDto> dtos, CancellationToken cancellationToken = default)
    {
        var dtoList = dtos.ToList();
        var tenantId = _tenantContext.TenantId;

        var fileIds = dtoList
            .SelectMany(d => new[] { d.IconFileId, d.ImageFileId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (fileIds.Count == 0) return;

        try
        {
            var filesDict = await _fileManagerClient.GetFilesByIdsAsync(fileIds, tenantId, cancellationToken);

            foreach (var dto in dtoList)
            {
                if (dto.IconFileId.HasValue && filesDict.TryGetValue(dto.IconFileId.Value, out var iconFile))
                    dto.IconFile = iconFile;

                if (dto.ImageFileId.HasValue && filesDict.TryGetValue(dto.ImageFileId.Value, out var imageFile))
                    dto.ImageFile = imageFile;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to batch fetch files for {Count} categories", dtoList.Count);
        }
    }

    /// <summary>Enriches all DTOs in a tree (recursive) with file metadata.</summary>
    public async Task EnrichTreeWithFilesAsync(IEnumerable<CategoryDto> roots, CancellationToken cancellationToken = default)
    {
        var allDtos = FlattenTree(roots).ToList();
        await EnrichCategoriesWithFilesAsync(allDtos, cancellationToken);
    }

    private static IEnumerable<CategoryDto> FlattenTree(IEnumerable<CategoryDto> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in FlattenTree(node.Children))
                yield return child;
        }
    }
}
