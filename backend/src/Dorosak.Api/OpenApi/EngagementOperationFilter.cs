using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Dorosak.Api.OpenApi;

internal sealed class EngagementOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.ActionDescriptor.RouteValues["controller"] != "Engagement")
        {
            return;
        }

        OpenApiParameter? idempotencyKey = operation.Parameters?.OfType<OpenApiParameter>().FirstOrDefault(parameter =>
            string.Equals(parameter.Name, "Idempotency-Key", StringComparison.OrdinalIgnoreCase));
        if (idempotencyKey is not null)
        {
            idempotencyKey.Required = true;
        }
    }
}
