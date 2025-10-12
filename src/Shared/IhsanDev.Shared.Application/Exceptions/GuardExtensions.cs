using IhsanDev.Shared.Application.Exceptions;

namespace IhsanDev.Shared.Application.Extensions;

public static class GuardExtensions
{
    /// <summary>
    /// Throws NotFoundException if value is null
    /// </summary>
    public static T ThrowIfNull<T>(this T? value, string message) where T : class
    {
        if (value is null)
            throw new NotFoundException(message);

        return value;
    }

    /// <summary>
    /// Throws BadRequestException if string is null, empty, or whitespace
    /// </summary>
    public static string ThrowIfEmpty(this string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new BadRequestException(message);

        return value;
    }

    /// <summary>
    /// Throws BadRequestException if collection is null or empty
    /// </summary>
    public static IEnumerable<T> ThrowIfEmpty<T>(this IEnumerable<T>? collection, string message)
    {
        if (collection is null || !collection.Any())
            throw new BadRequestException(message);

        return collection;
    }

    /// <summary>
    /// Throws BadRequestException if array is null or empty
    /// </summary>
    public static T[] ThrowIfEmpty<T>(this T[]? array, string message)
    {
        if (array is null || array.Length == 0)
            throw new BadRequestException(message);

        return array;
    }

    /// <summary>
    /// Throws BadRequestException if list is null or empty
    /// </summary>
    public static IList<T> ThrowIfEmpty<T>(this IList<T>? list, string message)
    {
        if (list is null || list.Count == 0)
            throw new BadRequestException(message);

        return list;
    }

    /// <summary>
    /// Throws BadRequestException if condition is false
    /// </summary>
    public static void ThrowIfFalse(this bool condition, string message)
    {
        if (!condition)
            throw new BadRequestException(message);
    }

    /// <summary>
    /// Throws ConflictException if condition is true
    /// </summary>
    public static void ThrowIfTrue(this bool condition, string message)
    {
        if (condition)
            throw new ConflictException(message);
    }
}


// Before
// var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
// if (user is null)
//     throw new NotFoundException($"User with ID {userId} not found");

// // After ✅
// var user = (await _userRepository.GetByIdAsync(userId, cancellationToken))
//     .ThrowIfNull($"User with ID {userId} not found");

// // Before
// if (await _userRepository.EmailExistsAsync(email, cancellationToken))
//     throw new ConflictException("Email is already registered");

// // After ✅
// (await _userRepository.EmailExistsAsync(email, cancellationToken))
//     .ThrowIfTrue("Email is already registered");