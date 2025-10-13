using FluentValidation;

namespace Identity.API.Filters;

/// <summary>
/// Generic validation filter for minimal API endpoints
/// </summary>
public class ValidationFilter<T> : IEndpointFilter where T : class
{
    private readonly IValidator<T> _validator;

    public ValidationFilter(IValidator<T> validator)
    {
        _validator = validator;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // Find the parameter of type T in the endpoint arguments
        var argumentToValidate = context.Arguments.OfType<T>().FirstOrDefault();
        
        if (argumentToValidate is not null)
        {
            var validationResult = await _validator.ValidateAsync(argumentToValidate);
            
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }
        }

        return await next(context);
    }
}