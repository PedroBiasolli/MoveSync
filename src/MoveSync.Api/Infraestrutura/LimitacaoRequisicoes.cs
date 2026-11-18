using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace MoveSync.Api.Infraestrutura;

/// <summary>Nomes das políticas de rate limiting aplicadas nos endpoints.</summary>
public static class PoliticasLimite
{
    /// <summary>Login/cadastro: protege contra força bruta. Janela fixa por IP.</summary>
    public const string Autenticacao = "autenticacao";

    /// <summary>Consultas públicas (status do cruzamento): janela deslizante por IP.</summary>
    public const string Publico = "publico";

    /// <summary>Usuário logado: token bucket por usuário (permite rajadas curtas).</summary>
    public const string Usuario = "usuario";

    /// <summary>Telemetria: janela fixa POR SENSOR — um ESP32 com defeito não derruba a API para os outros.</summary>
    public const string Telemetria = "telemetria";
}

public sealed class OpcoesLimite
{
    public int Limite { get; set; }

    public int JanelaSegundos { get; set; } = 60;
}

/// <summary>Seção "LimiteRequisicoes" do appsettings (permite ajustar sem recompilar).</summary>
public sealed class OpcoesLimitacaoRequisicoes
{
    public const string Secao = "LimiteRequisicoes";

    public OpcoesLimite Autenticacao { get; set; } = new() { Limite = 10, JanelaSegundos = 60 };

    public OpcoesLimite Publico { get; set; } = new() { Limite = 60, JanelaSegundos = 60 };

    public OpcoesLimite Usuario { get; set; } = new() { Limite = 120, JanelaSegundos = 60 };

    public OpcoesLimite Telemetria { get; set; } = new() { Limite = 30, JanelaSegundos = 60 };
}

public static class ConfiguracaoLimitacaoRequisicoes
{
    public static IServiceCollection AdicionarLimitacaoRequisicoes(this IServiceCollection services, IConfiguration configuration)
    {
        // Vínculo "vivo" com a seção: os limites são lidos na requisição, então variáveis de ambiente
        // e os testes de integração conseguem ajustá-los.
        services.Configure<OpcoesLimitacaoRequisicoes>(configuration.GetSection(OpcoesLimitacaoRequisicoes.Secao));

        services.AddRateLimiter(limitador =>
        {
            limitador.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limitador.OnRejected = ResponderLimiteExcedidoAsync;

            limitador.AddPolicy(PoliticasLimite.Autenticacao, http =>
                RateLimitPartition.GetFixedWindowLimiter(
                    $"auth:{ChaveIp(http)}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = Opcoes(http).Autenticacao.Limite,
                        Window = TimeSpan.FromSeconds(Opcoes(http).Autenticacao.JanelaSegundos),
                        QueueLimit = 0
                    }));

            limitador.AddPolicy(PoliticasLimite.Publico, http =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    $"pub:{ChaveIp(http)}",
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = Opcoes(http).Publico.Limite,
                        Window = TimeSpan.FromSeconds(Opcoes(http).Publico.JanelaSegundos),
                        SegmentsPerWindow = 6,
                        QueueLimit = 0
                    }));

            limitador.AddPolicy(PoliticasLimite.Usuario, http =>
                RateLimitPartition.GetTokenBucketLimiter(
                    $"usr:{http.User.FindFirstValue(NomesClaims.Sub) ?? ChaveIp(http)}",
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = Opcoes(http).Usuario.Limite,
                        TokensPerPeriod = Opcoes(http).Usuario.Limite,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(Opcoes(http).Usuario.JanelaSegundos),
                        AutoReplenishment = true,
                        QueueLimit = 0
                    }));

            limitador.AddPolicy(PoliticasLimite.Telemetria, http =>
                RateLimitPartition.GetFixedWindowLimiter(
                    $"tel:{http.User.FindFirstValue(NomesClaims.Sub) ?? ChaveIp(http)}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = Opcoes(http).Telemetria.Limite,
                        Window = TimeSpan.FromSeconds(Opcoes(http).Telemetria.JanelaSegundos),
                        QueueLimit = 0
                    }));
        });

        return services;
    }

    private static OpcoesLimitacaoRequisicoes Opcoes(HttpContext http) =>
        http.RequestServices.GetRequiredService<IOptions<OpcoesLimitacaoRequisicoes>>().Value;

    private static string ChaveIp(HttpContext http) =>
        http.Connection.RemoteIpAddress?.ToString() ?? "ip-desconhecido";

    /// <summary>Resposta 429 padronizada (ProblemDetails) com o cabeçalho Retry-After.</summary>
    private static async ValueTask ResponderLimiteExcedidoAsync(OnRejectedContext contexto, CancellationToken ct)
    {
        var http = contexto.HttpContext;
        var segundos = 60;
        if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            segundos = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
        }

        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        http.Response.Headers.RetryAfter = segundos.ToString(CultureInfo.InvariantCulture);

        var problema = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Limite de requisições excedido.",
            Detail = $"Muitas requisições em pouco tempo. Tente novamente em {segundos} segundo(s).",
            Type = "https://datatracker.ietf.org/doc/html/rfc6585#section-4"
        };
        problema.Extensions["codigo"] = "LIMITE_REQUISICOES";
        problema.Extensions["tenteNovamenteEmSegundos"] = segundos;

        var servico = http.RequestServices.GetRequiredService<IProblemDetailsService>();
        await servico.WriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = problema });
    }
}
