using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace MoveSync.Api.Infraestrutura;

/// <summary>Informações gerais do documento OpenAPI e esquema de segurança Bearer (JWT).</summary>
internal sealed class DocumentoOpenApiTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "MoveSync API",
            Version = "v1",
            Description =
                "Backend .NET da MoveSync (FIAP Global Solution 2026/2 – CitizenTech): semáforos inteligentes que ajustam " +
                "os tempos conforme o fluxo medido por sensores IoT, com prioridade para emergências, travessia acessível para " +
                "pedestres e canal de ocorrências do cidadão.\n\n" +
                "**Como testar:** faça login em `POST /api/auth/login`, copie o `accessToken` e clique em **Authorize**. " +
                "Endpoints que alteram dados importantes exigem o cabeçalho `Idempotency-Key` (ex.: um UUID).",
            Contact = new OpenApiContact { Name = "Equipe MoveSync – FIAP 2TDSPO" }
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Cole apenas o token (sem a palavra Bearer)."
        };

        return Task.CompletedTask;
    }
}

/// <summary>Coloca o "cadeado" do Swagger apenas nos endpoints que exigem autenticação.</summary>
internal sealed class SegurancaOperacaoTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var metadados = context.Description.ActionDescriptor.EndpointMetadata;
        var exigeAutenticacao = metadados.OfType<IAuthorizeData>().Any() && !metadados.OfType<IAllowAnonymous>().Any();
        if (!exigeAutenticacao)
        {
            return Task.CompletedTask;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
        });

        return Task.CompletedTask;
    }
}
