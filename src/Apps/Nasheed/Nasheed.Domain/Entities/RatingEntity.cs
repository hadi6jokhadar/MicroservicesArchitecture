namespace Nasheed.Domain.Entities;

/// <summary>
/// Join table — does not inherit BaseEntity.
/// Composite unique key: UserId + SongId.
/// </summary>
public class RatingEntity
{
    public int UserId { get; private set; }
    public int SongId { get; private set; }

    /// <summary>Rating value from 1 to 5.</summary>
    public int Value { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // Navigation
    public SongEntity? Song { get; private set; }

    private RatingEntity() { }

    public static RatingEntity Create(int userId, int songId, int value)
    {
        if (value < 1 || value > 5)
            throw new ArgumentOutOfRangeException(nameof(value), "Rating value must be between 1 and 5.");

        return new RatingEntity
        {
            UserId = userId,
            SongId = songId,
            Value = value,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(int value)
    {
        if (value < 1 || value > 5)
            throw new ArgumentOutOfRangeException(nameof(value), "Rating value must be between 1 and 5.");

        Value = value;
        UpdatedAt = DateTime.UtcNow;
    }
}
