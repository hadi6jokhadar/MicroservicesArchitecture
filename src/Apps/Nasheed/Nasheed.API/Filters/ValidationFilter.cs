using FluentValidation;
using IhsanDev.Shared.Infrastructure.Filters;

namespace Nasheed.API.Filters;

public class ValidationFilter<T> : SharedValidationFilter<T> where T : class
{
    public ValidationFilter(IValidator<T> validator) : base(validator) { }
}
