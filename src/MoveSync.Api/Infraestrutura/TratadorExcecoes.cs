using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MoveSync.Application.Excecoes;
using MoveSync.Domain.Comum;

namespace MoveSync.Api.Infraestrutura;

/// <summary>
/// Converte exceções em respostas HTTP padronizadas (RFC 9457 – Problem Details).
/// Assim os endpoints ficam limpos: o serviço lança a exceção e aqui decidimos o código HTTP.
///   RegraNegocio (Validacao)  → 422  | RegraNegocio (Conflito) → 409 | RegraNegocio (AcessoNegado) → 403
///   RecursoNaoEncontrado      → 404  | CredenciaisInvalidas    → 401 | JSON malformado          → 400
///   Qualquer outra            → 500 (sem detalhes internos para o cliente; detalhes só no log)
/// </summary>
public sealed class TratadorExcecoesGlobal(IProblemDetailsService problemas, ILogger<TratadorExcecoesGlobal> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception excecao, CancellationToken ct)
    {
        var (status, titulo, codigo, detalhe) = excecao switch
        {
            RegraNegocioException { Tipo: TipoViolacao.Conflito } ex =>
                (StatusCodes.Status409Conflict, "Conflito com o estado atual do recurso.", ex.Codigo, ex.Message),
            RegraNegocioException { Tipo: TipoViolacao.AcessoNegado } ex =>
                (StatusCodes.Status403Forbidden, "Operação não permitida.", ex.Codigo, ex.Message),
            RegraNegocioException ex =>
                (StatusCodes.Status422UnprocessableEntity, "Regra de negócio violada.", ex.Codigo, ex.Message),
            RecursoNaoEncontradoException ex =>
                (StatusCodes.Status404NotFound, "Recurso não encontrado.", "NAO_ENCONTRADO", ex.Message),
            CredenciaisInvalidasException ex =>
                (StatusCodes.Status401Unauthorized, "Não autenticado.", "CREDENCIAIS_INVALIDAS", ex.Message),
            ConflitoPersistenciaException ex =>
                (StatusCodes.Status409Conflict, "Conflito de dados.", "REGISTRO_DUPLICADO", ex.Message),
            BadHttpRequestException ex =>
                (ex.StatusCode, "Requisição inválida.", "REQUISICAO_INVALIDA", "O corpo ou os parâmetros da requisição estão em formato inválido. Verifique o JSON enviado."),
            _ =>
                (StatusCodes.Status500InternalServerError, "Erro interno.", "ERRO_INTERNO", "Ocorreu um erro inesperado. Tente novamente mais tarde.")
        };

        if (status >= 500)
        {
            logger.LogError(excecao, "Erro não tratado em {Metodo} {Caminho}", contexto.Request.Method, contexto.Request.Path);
        }
        else
        {
            logger.LogInformation("Requisição recusada ({Status} {Codigo}): {Mensagem}", status, codigo, excecao.Message);
        }

        contexto.Response.StatusCode = status;
        var problema = new ProblemDetails
        {
            Status = status,
            Title = titulo,
            Detail = detalhe,
            Instance = $"{contexto.Request.Method} {contexto.Request.Path}"
        };
        problema.Extensions["codigo"] = codigo;

        return await problemas.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = contexto,
            ProblemDetails = problema,
            Exception = excecao
        });
    }
}
