using FluentValidation;
using IhsanDev.Shared.Infrastructure.Filters;

namespace Notification.API.Filters;

/// <summary>
/// Notification service uses the shared ValidationFilter from infrastructure
/// This ensures consistent error responses across all microservices
/// </summary>
public class ValidationFilter<T> : SharedValidationFilter<T> where T : class
{
    public ValidationFilter(IValidator<T> validator) : base(validator)
    {
    }
}
