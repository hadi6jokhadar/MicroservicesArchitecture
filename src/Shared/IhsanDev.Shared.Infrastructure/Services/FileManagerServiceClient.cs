using System.Net.Http.Json;
using IhsanDev.Shared.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace IhsanDev.Shared.Infrastructure.Services;

/// <summary>
/// Client for fast service-to-service communication with FileManager Service
/// Uses internal endpoint that bypasses rate limiting and most middleware
/// </summary>
public class FileManagerServiceClient : IFileManagerServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FileManagerServiceClient> _logger;

    public FileManagerServiceClient(
        HttpClient httpClient,
        ILogger<FileManagerServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<FileManagerDto?> GetFileByIdAsync(
        int fileId, 
        string? tenantId = null, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Use internal endpoint for fast service-to-service communication
            var endpoint = $"/api/filemanager/internal/files/{fileId}";
            
            // Add tenantId query parameter if provided
            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                endpoint += $"?tenantId={Uri.EscapeDataString(tenantId)}";
            }

            var response = await _httpClient.GetAsync(endpoint, cancellationToken);

            // Read response body
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to fetch file {FileId} from FileManager - Status: {StatusCode}",
                    fileId,
                    response.StatusCode);
                return null;
            }

            if (string.IsNullOrWhiteSpace(responseBody))
            {
                _logger.LogWarning(
                    "File {FileId} returned empty response from FileManager service",
                    fileId);
                return null;
            }

            var fileDto = System.Text.Json.JsonSerializer.Deserialize<FileManagerDto>(
                responseBody,
                new System.Text.Json.JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

            if (fileDto == null)
            {
                _logger.LogWarning(
                    "File {FileId} not found in FileManager service",
                    fileId);
                return null;
            }

            return fileDto;
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogWarning(ex, "FileManager circuit open; skipping file {FileId} fetch", fileId);
            return null;
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogWarning(ex, "FileManager timeout fetching file {FileId}", fileId);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "HTTP error occurred while fetching file {FileId} from FileManager service. Message: {Message}",
                fileId,
                ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error occurred while fetching file {FileId} from FileManager service. Type: {ExceptionType}, Message: {Message}",
                fileId,
                ex.GetType().Name,
                ex.Message);
            return null;
        }
    }

    public async Task<Dictionary<int, FileManagerDto>> GetFilesByIdsAsync(
        IEnumerable<int> fileIds, 
        string? tenantId = null, 
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<int, FileManagerDto>();
        var fileIdsList = fileIds.ToList();

        if (!fileIdsList.Any())
        {
            return result;
        }

        try
        {
            var endpoint = "/api/filemanager/internal/files/batch";
            
            // Build query parameters
            var queryParams = string.Join("&", fileIdsList.Select(id => $"fileIds={id}"));
            
            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                queryParams += $"&tenantId={Uri.EscapeDataString(tenantId)}";
            }

            var fullEndpoint = $"{endpoint}?{queryParams}";

            var response = await _httpClient.GetAsync(fullEndpoint, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to fetch {Count} files in batch - Status: {StatusCode}",
                    fileIdsList.Count,
                    response.StatusCode);
                return result;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                _logger.LogWarning("Batch file fetch returned empty response");
                return result;
            }

            var files = System.Text.Json.JsonSerializer.Deserialize<List<FileManagerDto>>(
                responseBody,
                new System.Text.Json.JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

            if (files != null)
            {
                foreach (var file in files)
                {
                    result[file.Id] = file;
                }
            }

            return result;
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogWarning(ex, "FileManager circuit open; skipping batch fetch for {Count} files", fileIdsList.Count);
            return result;
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogWarning(ex, "FileManager timeout on batch fetch for {Count} files", fileIdsList.Count);
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "HTTP error occurred while batch fetching {Count} files. Message: {Message}",
                fileIdsList.Count,
                ex.Message);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error occurred while batch fetching {Count} files. Type: {ExceptionType}, Message: {Message}",
                fileIdsList.Count,
                ex.GetType().Name,
                ex.Message);
            return result;
        }
    }

    public async Task<bool> ChangeTempStatusAsync(
        int fileId, 
        string usageArea,
        string rowId,
        bool isNew,
        string? tenantId = null, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = $"/api/filemanager/internal/files/{fileId}/temp-status?usageArea={Uri.EscapeDataString(usageArea)}&rowId={Uri.EscapeDataString(rowId)}&isNew={isNew.ToString().ToLower()}";
            
            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                endpoint += $"&tenantId={Uri.EscapeDataString(tenantId)}";
            }

            var response = await _httpClient.PatchAsync(endpoint, null, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to change temp status for file {FileId} - Status: {StatusCode}",
                    fileId,
                    response.StatusCode);
                return false;
            }

            return true;
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogWarning(ex, "FileManager circuit open; skipping temp-status change for file {FileId}", fileId);
            return false;
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogWarning(ex, "FileManager timeout changing temp-status for file {FileId}", fileId);
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "HTTP error occurred while changing temp status for file {FileId}. Message: {Message}",
                fileId,
                ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error occurred while changing temp status for file {FileId}. Type: {ExceptionType}, Message: {Message}",
                fileId,
                ex.GetType().Name,
                ex.Message);
            return false;
        }
    }
}
