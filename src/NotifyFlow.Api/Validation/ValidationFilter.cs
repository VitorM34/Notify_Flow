namespace NotifyFlow.Api.Validation;

public sealed class ValidationFilter<TRequest> : IEndpointFilter
    where TRequest : IValidatableRequest
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var request = context.GetArgument<TRequest>(0);
        var errors = request.Validate().ToArray();

        if (errors.Length > 0)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(TRequest)] = errors
            });
        }

        return await next(context);
    }
}
