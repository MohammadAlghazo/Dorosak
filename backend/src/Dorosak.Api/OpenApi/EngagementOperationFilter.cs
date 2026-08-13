using Microsoft.OpenApi;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Dorosak.Api.OpenApi;

internal sealed class RequiredIdempotencyHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        OpenApiParameter? idempotencyKey = operation.Parameters?.OfType<OpenApiParameter>().FirstOrDefault(parameter =>
            string.Equals(parameter.Name, "Idempotency-Key", StringComparison.OrdinalIgnoreCase));
        if (idempotencyKey is not null)
        {
            idempotencyKey.Required = true;
        }
    }
}
