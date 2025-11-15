using System.ComponentModel.DataAnnotations;

namespace IhsanDev.Shared.Kernel.Dto.Identity;

public abstract class BaseDto
{
    [Key]
    public int Id { get; set; }

    public bool IsArchived { get; set; } = false;

    public bool Status { get; set; } = true;

    public string Created { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);

    public string? CreatedBy { get; set; }

    public string? LastModified { get; set; }

    public string? LastModifiedBy { get; set; }
}