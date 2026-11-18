using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MoveSync.Api.Infraestrutura;
using MoveSync.Application.Abstracoes;
using MoveSync.Application.Contratos;
using MoveSync.Application.Servicos;
using MoveSync.Domain.Enums;

namespace MoveSync.Api.Endpoints;

/// <summary>Sensores, telemetria IoT, emergências e painel do gestor.</summary>
public static class OperacaoEndpoints
{
    public static IEndpointRouteBuilder MapearSensores(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/sensores")
            .WithTags("Sensores")
            .RequireAuthorization(Politicas.SomenteGestor)
            .RequireRateLimiting(PoliticasLimite.Usuario);

        grupo.MapPatch("/{id:guid}/ativo", async (Guid id, AlterarAtivoSensorRequest requisicao, SensorService servico, CancellationToken ct) =>
                TypedResults.Ok(await servico.AlterarAtivoAsync(id, requisicao.Ativo!.Value, ct)))
            .WithName("AlterarAtivoSensor")
            .WithSummary("Ativa ou desativa um sensor (sensor inativo não envia telemetria)")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapDelete("/{id:guid}", async (Guid id, SensorService servico, CancellationToken ct) =>
            {
                await servico.RemoverAsync(id, ct);
                return TypedResults.NoContent();
            })
            .WithName("RemoverSensor")
            .WithSummary("Remove um sensor e suas leituras")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    public static IEndpointRouteBuilder MapearTelemetria(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/telemetria").WithTags("Telemetria IoT");

        grupo.MapPost("/leituras", async Task<Results<Created<LeituraResponse>, Ok<LeituraResponse>>> (
                RegistrarLeituraRequest requisicao,
                ClaimsPrincipal sensor,
                HttpContext http,
                TelemetriaService servico,
                CancellationToken ct) =>
            {
                var resultado = await servico.RegistrarAsync(sensor.ObterId(), requisicao, ct);
                if (resultado.Duplicada)
                {
                    // Idempotência pela chave natural: reenvio devolve a leitura original, sem duplicar.
                    http.Response.Headers[ExtensoesIdempotencia.CabecalhoRepeticao] = "true";
                    return TypedResults.Ok(resultado.Leitura);
                }

                return TypedResults.Created($"/api/telemetria/vias/{resultado.Leitura.ViaId}/leituras", resultado.Leitura);
            })
            .RequireAuthorization(Politicas.SomenteSensor)
            .RequireRateLimiting(PoliticasLimite.Telemetria)
            .WithName("RegistrarLeitura")
            .WithSummary("Recebe uma leitura de um sensor (perfil Sensor)")
            .WithDescription("Idempotente pelo campo `leituraId`: a 1ª vez retorna 201; reenvios retornam 200 com a mesma leitura e o cabeçalho `Idempotency-Replayed: true`. " +
                             "Limite por sensor: um dispositivo com defeito recebe 429 sem afetar os demais.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        grupo.MapPost("/leituras/lote", async (
                RegistrarLoteLeiturasRequest requisicao,
                ClaimsPrincipal sensor,
                TelemetriaService servico,
                CancellationToken ct) =>
                TypedResults.Ok(await servico.RegistrarLoteAsync(sensor.ObterId(), requisicao, ct)))
            .RequireAuthorization(Politicas.SomenteSensor)
            .RequireRateLimiting(PoliticasLimite.Telemetria)
            .WithName("RegistrarLoteLeituras")
            .WithSummary("Recebe até 100 leituras acumuladas (sensor que ficou sem internet)")
            .WithDescription("Leituras repetidas são ignoradas e inválidas são rejeitadas individualmente, sem derrubar o lote.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        grupo.MapGet("/vias/{viaId:guid}/leituras", async (
                Guid viaId,
                [AsParameters] ParametrosPaginacao paginacao,
                TelemetriaService servico,
                CancellationToken ct) =>
                TypedResults.Ok(await servico.ListarPorViaAsync(viaId, paginacao, ct)))
            .RequireAuthorization(Politicas.SomenteGestor)
            .RequireRateLimiting(PoliticasLimite.Usuario)
            .WithName("ListarLeiturasVia")
            .WithSummary("Leituras de uma via, mais recentes primeiro")
            .ProducesValidationProblem();

        return app;
    }

    public static IEndpointRouteBuilder MapearOcorrencias(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/ocorrencias")
            .WithTags("Ocorrências do cidadão")
            .RequireRateLimiting(PoliticasLimite.Usuario);

        grupo.MapPost("", async (
                CriarOcorrenciaRequest requisicao,
                [FromHeader(Name = ExtensoesIdempotencia.Cabecalho)] string idempotencyKey,
                ClaimsPrincipal usuario,
                OcorrenciaService servico,
                CancellationToken ct) =>
            {
                var criada = await servico.CriarAsync(usuario.ObterId(), requisicao, ct);
                return TypedResults.Created($"/api/ocorrencias/{criada.Id}", criada);
            })
            .RequireAuthorization(Politicas.SomenteCidadao)
            .ComIdempotencia()
            .WithName("CriarOcorrencia")
            .WithSummary("Cidadão relata um problema no cruzamento")
            .WithDescription("A prioridade é definida pelo sistema: semáforo apagado = Crítica. Exige `Idempotency-Key` para que um duplo toque no app não crie dois relatos.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        grupo.MapGet("/minhas", async (
                StatusOcorrencia? status,
                [AsParameters] ParametrosPaginacao paginacao,
                ClaimsPrincipal usuario,
                OcorrenciaService servico,
                CancellationToken ct) =>
                TypedResults.Ok(await servico.ListarDoAutorAsync(usuario.ObterId(), status, paginacao, ct)))
            .RequireAuthorization(Politicas.SomenteCidadao)
            .WithName("MinhasOcorrencias")
            .WithSummary("Ocorrências do cidadão logado")
            .ProducesValidationProblem();

        grupo.MapGet("", async (
                Guid? cruzamentoId,
                StatusOcorrencia? status,
                PrioridadeOcorrencia? prioridade,
                [AsParameters] ParametrosPaginacao paginacao,
                OcorrenciaService servico,
                CancellationToken ct) =>
                TypedResults.Ok(await servico.ListarAsync(new FiltroOcorrencias(null, cruzamentoId, status, prioridade), paginacao, ct)))
            .RequireAuthorization(Politicas.SomenteGestor)
            .WithName("ListarOcorrencias")
            .WithSummary("Todas as ocorrências (gestor), com filtros")
            .ProducesValidationProblem();

        grupo.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal usuario, OcorrenciaService servico, CancellationToken ct) =>
                TypedResults.Ok(await servico.ObterAsync(id, usuario.ObterId(), usuario.EhGestor(), ct)))
            .RequireAuthorization(Politicas.Pessoa)
            .WithName("ObterOcorrencia")
            .WithSummary("Detalha uma ocorrência (o cidadão só vê as próprias)")
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapPut("/{id:guid}", async (Guid id, AtualizarOcorrenciaRequest requisicao, ClaimsPrincipal usuario, OcorrenciaService servico, CancellationToken ct) =>
                TypedResults.Ok(await servico.AtualizarAsync(id, usuario.ObterId(), requisicao, ct)))
            .RequireAuthorization(Politicas.SomenteCidadao)
            .WithName("AtualizarOcorrencia")
            .WithSummary("Cidadão edita a própria ocorrência (somente enquanto Aberta)")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        grupo.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal usuario, OcorrenciaService servico, CancellationToken ct) =>
            {
                await servico.RemoverAsync(id, usuario.ObterId(), ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization(Politicas.SomenteCidadao)
            .WithName("RemoverOcorrencia")
            .WithSummary("Cidadão exclui a própria ocorrência (somente enquanto Aberta)")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        grupo.MapPatch("/{id:guid}/status", async (Guid id, AlterarStatusOcorrenciaRequest requisicao, OcorrenciaService servico, CancellationToken ct) =>
                TypedResults.Ok(await servico.AlterarStatusAsync(id, requisicao, ct)))
            .RequireAuthorization(Politicas.SomenteGestor)
            .WithName("AlterarStatusOcorrencia")
            .WithSummary("Gestor atualiza o status (EmAnalise, Resolvida, Rejeitada)")
            .WithDescription("Resolver ou rejeitar exige resposta ao cidadão. Transições inválidas retornam 409.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }

    public static IEndpointRouteBuilder MapearEmergenciasEPainel(this IEndpointRouteBuilder app)
    {
        var emergencias = app.MapGroup("/api/emergencias")
            .WithTags("Emergências")
            .RequireAuthorization(Politicas.SomenteGestor)
            .RequireRateLimiting(PoliticasLimite.Usuario);

        emergencias.MapGet("/ativas", async (PrioridadeService servico, CancellationToken ct) =>
                TypedResults.Ok(await servico.ListarEmergenciasAtivasAsync(ct)))
            .WithName("EmergenciasAtivas")
            .WithSummary("Corredores de emergência em andamento");

        emergencias.MapGet("/{id:guid}", async (Guid id, PrioridadeService servico, CancellationToken ct) =>
                TypedResults.Ok(await servico.ObterEmergenciaAsync(id, ct)))
            .WithName("ObterEmergencia")
            .WithSummary("Detalha um acionamento de emergência")
            .ProducesProblem(StatusCodes.Status404NotFound);

        emergencias.MapPost("/{id:guid}/encerrar", async (Guid id, PrioridadeService servico, CancellationToken ct) =>
                TypedResults.Ok(await servico.EncerrarEmergenciaAsync(id, ct)))
            .WithName("EncerrarEmergencia")
            .WithSummary("Encerra o corredor de emergência e volta ao plano normal")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapGet("/api/painel/resumo", async (PainelService servico, CancellationToken ct) =>
                TypedResults.Ok(await servico.ObterResumoAsync(ct)))
            .WithTags("Painel do gestor")
            .RequireAuthorization(Politicas.SomenteGestor)
            .RequireRateLimiting(PoliticasLimite.Usuario)
            .WithName("ResumoPainel")
            .WithSummary("Resumo operacional: cruzamentos, modos, sensores offline, ocorrências e emergências");

        app.MapGet("/api/painel/impacto", async ([AsParameters] ParametrosImpacto simulacao, ImpactoService servico, CancellationToken ct) =>
                TypedResults.Ok(await servico.GerarAsync(simulacao, ct)))
            .WithTags("Painel do gestor")
            .RequireAuthorization(Politicas.SomenteGestor)
            .RequireRateLimiting(PoliticasLimite.Usuario)
            .WithName("RelatorioImpacto")
            .WithSummary("Relatório de impacto e benefício/custo para a prefeitura")
            .WithDescription("Soma o ganho medido (tempo fixo × adaptativo) de todos os cruzamentos ativos e projeta o mês: horas de espera e CO₂ evitados, " +
                             "valor do tempo devolvido à população, custo da assinatura, relação benefício/custo e payback social da implantação. " +
                             "Parâmetros opcionais simulam cenários (horas por dia, valor da hora, preço da assinatura).")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }
}
