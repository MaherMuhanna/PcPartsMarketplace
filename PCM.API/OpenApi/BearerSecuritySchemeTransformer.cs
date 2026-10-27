using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace PCM.API.OpenApi;

/// <summary>
/// Declares the JWT bearer scheme in the OpenAPI document, so Scalar shows an
/// "Authentication" box: paste an accessToken from POST /auth/login once and every
/// request sends "Authorization: Bearer ...". Anonymous endpoints simply ignore the header.
/// </summary>
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public const string SchemeName = "Bearer";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Access token from POST /api/v1/auth/login (valid 60 minutes)."
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeName, document)] = []
        });

        return Task.CompletedTask;
    }
}
