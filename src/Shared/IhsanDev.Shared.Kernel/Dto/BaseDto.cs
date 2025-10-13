using System.ComponentModel.DataAnnotations;

namespace IhsanDev.Shared.Kernel.Dto.Identity;

public abstract class BaseDto
{
    [Key]
    public int Id { get; set; }

    public bool IsArchived { get; set; } = false;

    public bool Status { get; set; } = true;

    public DateTime Created { get; set; } = DateTime.UtcNow;

    public string? CreatedBy { get; set; }

    public DateTime? LastModified { get; set; }

    public string? LastModifiedBy { get; set; }
}