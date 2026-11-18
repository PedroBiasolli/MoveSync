using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MoveSync.Infrastructure.Saude;

namespace MoveSync.Api.Infraestrutura;

/// <summary>
/// Health checks:
///  • /health/live  → a API está de pé? (usado pelo Azure para reiniciar o container se travar)
///  • /health/ready → está pronta para atender? (banco de dados acessível)
///  • /health       → visão completa: API, banco, sensores IoT e serviço externo (núcleo Java)
/// Status HTTP: Healthy/Degraded = 200, Unhealthy = 503.
/// </summary>
public static class ConfiguracaoSaude
{
    private const string TagLive = "live";
    private const string TagReady = "ready";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static IServiceCollection AdicionarVerificacoesSaude(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck("api", () => HealthCheckResult.Healthy("API em execução."), tags: [TagLive])
            .AddCheck<BancoDadosHealthCheck>("banco-de-dados", HealthStatus.Unhealthy, [TagReady])
            .AddCheck<SensoresHealthCheck>("sensores-iot", HealthStatus.Degraded, ["iot"])
            .AddCheck<NucleoJavaHealthCheck>("servico-externo-nucleo-java", HealthStatus.Degraded, ["externo"]);
        return services;
    }

    public static IEndpointRouteBuilder MapearVerificacoesSaude(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = EscreverRespostaAsync })
            .WithTags("Saúde").AllowAnonymous();
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = registro => registro.Tags.Contains(TagLive),
            ResponseWriter = EscreverRespostaAsync
        }).AllowAnonymous();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registro => registro.Tags.Contains(TagReady),
            ResponseWriter = EscreverRespostaAsync
        }).AllowAnonymous();
        return app;
    }

    private static Task EscreverRespostaAsync(HttpContext contexto, HealthReport relatorio)
    {
        contexto.Response.ContentType = "application/json; charset=utf-8";
        var resposta = new
        {
            status = relatorio.Status.ToString(),
            duracaoMs = Math.Round(relatorio.TotalDuration.TotalMilliseconds, 1),
            verificadoEm = DateTime.UtcNow,
            verificacoes = relatorio.Entries.Select(e => new
            {
                nome = e.Key,
                status = e.Value.Status.ToString(),
                descricao = e.Value.Description,
                duracaoMs = Math.Round(e.Value.Duration.TotalMilliseconds, 1),
                erro = e.Value.Exception?.Message,
                dados = e.Value.Data
            })
        };
        return contexto.Response.WriteAsync(JsonSerializer.Serialize(resposta, Json));
    }
}
