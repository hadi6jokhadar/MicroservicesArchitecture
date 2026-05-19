using FluentValidation;
using IhsanDev.Shared.Infrastructure.Filters;

namespace Category.API.Filters;

/// <summary>
/// Category service uses the shared ValidationFilter from infrastructure.
/// </summary>
public class ValidationFilter<T> : SharedValidationFilter<T> where T : class
{
    public ValidationFilter(IValidator<T> validator) : base(validator) { }
}
