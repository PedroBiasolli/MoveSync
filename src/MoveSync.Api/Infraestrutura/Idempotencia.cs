using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using MoveSync.Infrastructure.Idempotencia;

namespace MoveSync.Api.Infraestrutura;

/// <summary>Marca um endpoint como protegido por Idempotency-Key.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class ExigeIdempotenciaAttribute : Attribute;

public static class ExtensoesIdempotencia
{
    public const string Cabecalho = "Idempotency-Key";
    public const string CabecalhoRepeticao = "Idempotency-Replayed";

    /// <summary>Uso: app.MapPost(...).ComIdempotencia();</summary>
    public static RouteHandlerBuilder ComIdempotencia(this RouteHandlerBuilder builder) =>
        builder
            .WithMetadata(new ExigeIdempotenciaAttribute())
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
}

/// <summary>
/// Idempotência genérica (padrão usado por APIs de pagamento, como Stripe):
///  1. O cliente envia um cabeçalho Idempotency-Key único por operação (ex.: um UUID gerado no app).
///  2. Na primeira vez, a API "reserva" a chave, executa o endpoint e grava status + corpo da resposta.
///  3. Se o app repetir a requisição (timeout, duplo toque no botão, rede 3G instável), a API devolve
///     a MESMA resposta gravada, com o cabeçalho Idempotency-Replayed: true, sem executar de novo.
///  4. Mesma chave com corpo diferente → 422; chave ainda em processamento → 409.
/// A chave é isolada por usuário + método + rota, então chaves iguais de usuários diferentes não colidem.
/// </summary>
public sealed class IdempotenciaMiddleware(RequestDelegate next, ILogger<IdempotenciaMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext contexto, IArmazenamentoIdempotencia armazenamento, IProblemDetailsService problemas)
    {
        var endpoint = contexto.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<ExigeIdempotenciaAttribute>() is null)
        {
            await next(contexto);
            return;
        }

        var chave = contexto.Request.Headers[ExtensoesIdempotencia.Cabecalho].ToString().Trim();
        if (chave.Length is < 8 or > 100)
        {
            await EscreverProblemaAsync(
                contexto,
                problemas,
                StatusCodes.Status400BadRequest,
                "IDEMPOTENCY_KEY_OBRIGATORIA",
                "Cabeçalho Idempotency-Key ausente ou inválido.",
                "Envie o cabeçalho Idempotency-Key com um valor único por operação (8 a 100 caracteres, ex.: um UUID).");
            return;
        }

        var usuario = contexto.User.FindFirst(NomesClaims.Sub)?.Value ?? "anonimo";
        var rota = (endpoint as RouteEndpoint)?.RoutePattern.RawText ?? contexto.Request.Path.ToString();
        var escopo = $"{usuario}|{contexto.Request.Method}|{rota}";
        var hash = await CalcularHashAsync(contexto.Request);

        var reserva = await armazenamento.ReservarAsync(escopo, chave, hash, contexto.RequestAborted);
        switch (reserva.Situacao)
        {
            case SituacaoReserva.Repetida:
                await ReproduzirAsync(contexto, reserva.Registro!);
                return;
            case SituacaoReserva.EmProcessamento:
                await EscreverProblemaAsync(
                    contexto,
                    problemas,
                    StatusCodes.Status409Conflict,
                    "IDEMPOTENCY_KEY_EM_PROCESSAMENTO",
                    "Requisição em processamento.",
                    "Uma requisição com esta Idempotency-Key ainda está sendo processada. Aguarde e tente novamente.");
                return;
            case SituacaoReserva.ConteudoDiferente:
                await EscreverProblemaAsync(
                    contexto,
                    problemas,
                    StatusCodes.Status422UnprocessableEntity,
                    "IDEMPOTENCY_KEY_REUTILIZADA",
                    "Idempotency-Key reutilizada com conteúdo diferente.",
                    "Esta chave já foi usada em uma requisição com outro conteúdo. Gere uma nova chave para uma nova operação.");
                return;
        }

        var registro = reserva.Registro!;
        var corpoOriginal = contexto.Response.Body;
        await using var buffer = new MemoryStream();
        contexto.Response.Body = buffer;

        try
        {
            await next(contexto);
        }
        catch
        {
            // Erro na execução: a chave é liberada para que o cliente possa tentar de novo.
            contexto.Response.Body = corpoOriginal;
            await armazenamento.LiberarAsync(registro.Id, CancellationToken.None);
            throw;
        }

        contexto.Response.Body = corpoOriginal;
        var status = contexto.Response.StatusCode;
        buffer.Position = 0;

        if (status is >= 200 and < 500 and not StatusCodes.Status429TooManyRequests)
        {
            var corpo = Encoding.UTF8.GetString(buffer.ToArray());
            var location = contexto.Response.Headers.Location.ToString();
            await armazenamento.ConcluirAsync(
                registro.Id,
                status,
                contexto.Response.ContentType,
                string.IsNullOrEmpty(location) ? null : location,
                corpo,
                CancellationToken.None);
        }
        else
        {
            await armazenamento.LiberarAsync(registro.Id, CancellationToken.None);
        }

        await buffer.CopyToAsync(corpoOriginal, contexto.RequestAborted);
    }

    private async Task ReproduzirAsync(HttpContext contexto, Infrastructure.Persistencia.RegistroIdempotencia registro)
    {
        logger.LogInformation("Requisição repetida com Idempotency-Key no escopo {Escopo}: devolvendo resposta gravada.", registro.Escopo);
        contexto.Response.StatusCode = registro.StatusCode;
        contexto.Response.Headers[ExtensoesIdempotencia.CabecalhoRepeticao] = "true";
        if (!string.IsNullOrEmpty(registro.Location))
        {
            contexto.Response.Headers.Location = registro.Location;
        }

        if (!string.IsNullOrEmpty(registro.ContentType))
        {
            contexto.Response.ContentType = registro.ContentType;
        }

        if (!string.IsNullOrEmpty(registro.CorpoResposta))
        {
            await contexto.Response.WriteAsync(registro.CorpoResposta, Encoding.UTF8, contexto.RequestAborted);
        }
    }

    private static async Task<string> CalcularHashAsync(HttpRequest requisicao)
    {
        requisicao.EnableBuffering();
        string corpo;
        using (var leitor = new StreamReader(requisicao.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
        {
            corpo = await leitor.ReadToEndAsync(requisicao.HttpContext.RequestAborted);
        }

        requisicao.Body.Position = 0;
        var conteudo = $"{requisicao.Method}\n{requisicao.Path}{requisicao.QueryString}\n{corpo}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(conteudo)));
    }

    private static async Task EscreverProblemaAsync(
        HttpContext contexto,
        IProblemDetailsService problemas,
        int status,
        string codigo,
        string titulo,
        string detalhe)
    {
        contexto.Response.StatusCode = status;
        var problema = new ProblemDetails { Status = status, Title = titulo, Detail = detalhe };
        problema.Extensions["codigo"] = codigo;
        await problemas.WriteAsync(new ProblemDetailsContext { HttpContext = contexto, ProblemDetails = problema });
    }
}
