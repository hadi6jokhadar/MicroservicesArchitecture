using FluentValidation;
using IhsanDev.Shared.Infrastructure.Filters;

namespace Backup.API.Filters;

/// <summary>
/// Backup service uses the shared ValidationFilter from infrastructure — same pattern as
/// Tenant.API.Filters.ValidationFilter — for consistent error responses across all microservices.
/// </summary>
public class ValidationFilter<T> : SharedValidationFilter<T> where T : class
{
    public ValidationFilter(IValidator<T> validator) : base(validator)
    {
    }
}
