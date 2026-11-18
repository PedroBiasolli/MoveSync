using System.Security.Claims;
using MoveSync.Api.Infraestrutura;
using MoveSync.Application.Contratos;
using MoveSync.Application.Servicos;

namespace MoveSync.Api.Endpoints;

public static class AutenticacaoEndpoints
{
    public static IEndpointRouteBuilder MapearAutenticacao(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/auth").WithTags("Autenticação");

        grupo.MapPost("/registro", async (RegistrarUsuarioRequest requisicao, AutenticacaoService servico, CancellationToken ct) =>
            {
                var usuario = await servico.RegistrarAsync(requisicao, ct);
                return TypedResults.Created("/api/auth/me", usuario);
            })
            .AllowAnonymous()
            .RequireRateLimiting(PoliticasLimite.Autenticacao)
            .WithName("RegistrarCidadao")
            .WithSummary("Cadastra um cidadão")
            .WithDescription("Cria uma conta com perfil **Cidadao**. Contas de gestor não podem ser criadas por aqui (são provisionadas pela prefeitura).")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        grupo.MapPost("/login", async (LoginRequest requisicao, AutenticacaoService servico, CancellationToken ct) =>
                TypedResults.Ok(await servico.LoginAsync(requisicao, ct)))
            .AllowAnonymous()
            .RequireRateLimiting(PoliticasLimite.Autenticacao)
            .WithName("Login")
            .WithSummary("Login de cidadão ou gestor")
            .WithDescription("Retorna um JWT (válido por 2 h) com as claims sub, name, email e role.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        grupo.MapPost("/sensores/token", async (TokenSensorRequest requisicao, AutenticacaoService servico, CancellationToken ct) =>
                TypedResults.Ok(await servico.LoginSensorAsync(requisicao, ct)))
            .AllowAnonymous()
            .RequireRateLimiting(PoliticasLimite.Autenticacao)
            .WithName("TokenSensor")
            .WithSummary("Autenticação de dispositivo IoT")
            .WithDescription("O ESP32/câmera troca código + segredo por um JWT de perfil **Sensor** (válido por 24 h) para enviar telemetria.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        grupo.MapGet("/me", async (ClaimsPrincipal usuario, AutenticacaoService servico, CancellationToken ct) =>
                TypedResults.Ok(await servico.ObterPerfilAsync(usuario.ObterId(), ct)))
            .RequireAuthorization(Politicas.Pessoa)
            .RequireRateLimiting(PoliticasLimite.Usuario)
            .WithName("MeuPerfil")
            .WithSummary("Dados do usuário logado")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }
}
