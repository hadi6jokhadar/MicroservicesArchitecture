using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

namespace IhsanDev.Shared.Application.Common.Models;

public class PaginatedList<T>
{
    public List<T> Items { get; set; }
    public int PageNumber { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    // Parameterless constructor for JSON deserialization
    public PaginatedList()
    {
        Items = new List<T>();
    }

    // Constructor with count and pageSize (for CreateAsync factory method)
    [JsonConstructor]
    public PaginatedList(List<T> items, int totalCount, int pageNumber, int totalPages)
    {
        Items = items;
        PageNumber = pageNumber;
        TotalPages = totalPages;
        TotalCount = totalCount;
    }

    public static async Task<PaginatedList<T>> CreateAsync(
        IQueryable<T> source, 
        int pageNumber, 
        int pageSize, 
        CancellationToken cancellationToken = default)
    {
        var count = await source.CountAsync(cancellationToken);
        var items = await source
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(count / (double)pageSize);
        return new PaginatedList<T>(items, count, pageNumber, totalPages);
    }
}