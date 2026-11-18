using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MoveSync.Api.Infraestrutura;
using MoveSync.Application.Contratos;
using MoveSync.Application.Servicos;

namespace MoveSync.Api.Endpoints;

public static class CruzamentoEndpoints
{
    public static IEndpointRouteBuilder MapearCruzamentos(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/cruzamentos").WithTags("Cruzamentos");

        // ---------- Consultas públicas (benefício direto ao cidadão, sem login) ----------

        grupo.MapGet("", async (
                string? busca,
                bool? ativo,
                [AsParameters] ParametrosPaginacao paginacao,
                CruzamentoService servico,
                CancellationToken ct) =>
                TypedResults.Ok(await servico.ListarAsync(busca, ativo, paginacao, ct)))
            .AllowAnonymous()
            .RequireRateLimiting(PoliticasLimite.Publico)
            .WithName("ListarCruzamentos")
            .WithSummary("Lista cruzamentos (paginado)")
            .WithDescription("Filtro opcional por texto (nome, código ou endereço) e por situação (ativo).")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        grupo.MapGet("/{id:guid}", async (Guid id, CruzamentoService servico, CancellationToken ct) =>
                TypedResults.Ok(await servico.ObterAsync(id, ct)))
            .AllowAnonymous()
            .RequireRateLimiting(PoliticasLimite.Publico)
            .WithName("ObterCruzamento")
            .WithSummary("Detalha um cruzamento e suas vias")
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapGet("/{id:guid}/status", async (Guid id, PlanoSemaforicoService servico, CancellationToken ct) =>
                TypedResults.Ok(await servico.ObterStatusAsync(id, ct)))
            .AllowAnonymous()
            .RequireRateLimiting(PoliticasLimite.Publico)
            .WithName("StatusCruzamento")
            .WithSummary("Situação do cruzamento e tempo estimado de espera")
            .WithDescription("Funcionalidade principal para o cidadão: modo do semáforo, espera média/máxima por via, nível de congestionamento e tempo de travessia do pedestre.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        // ---------- Gestão (perfil Gestor) ----------

        grupo.MapPost("", async (CriarCruzamentoRequest requisicao, CruzamentoService servico, CancellationToken ct) =>
            {
                var criado = await servico.CriarAsync(requisicao, ct);
                return TypedResults.Created($"/api/cruzamentos/{criado.Id}", criado);
            })
            .RequireAuthorization(Politicas.SomenteGestor)
            .RequireRateLimiting(PoliticasLimite.Usuario)
            .WithName("CriarCruzamento")
            .WithSummary("Cadastra um cruzamento com suas vias (2 a 6)")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        grupo.MapPut("/{id:guid}", async (Guid id, AtualizarCruzamentoRequest requisicao, CruzamentoService servico, CancellationToken ct) =>
                TypedResults.Ok(await servico.AtualizarAsync(id, requisicao, ct)))
            .RequireAuthorization(Politicas.SomenteGestor)
            .RequireRateLimiting(PoliticasLimite.Usuario)
            .WithName("AtualizarCruzamento")
            .WithSummary("Atualiza dados do cruzamento (inclusive ativar/desativar)")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapDelete("/{id:guid}", async (Guid id, CruzamentoService servico, CancellationToken ct) =>
            {
                await servico.RemoverAsync(id, ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization(Politicas.SomenteGestor)
            .RequireRateLimiting(PoliticasLimite.Usuario)
            .WithName("RemoverCruzamento")
            .WithSummary("Exclui um cruzamento")
            .WithDescription("Bloqueado (409) se houver ocorrências de cidadãos vinculadas — nesse caso, desative o cruzamento.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        grupo.MapPatch("/{id:guid}/modo", async (Guid id, AlterarModoRequest requisicao, CruzamentoService servico, CancellationToken ct) =>
                TypedResults.Ok(await servico.AlterarModoAsync(id, requisicao, ct)))
            .RequireAuthorization(Politicas.SomenteGestor)
            .RequireRateLimiting(PoliticasLimite.Usuario)
            .WithName("AlterarModoCruzamento")
            .WithSummary("Alterna entre Adaptativo e TempoFixo (recalcula o plano na hora)")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapPost("/{id:guid}/vias", async (Guid id, CriarViaRequest requisicao, CruzamentoService servico, CancellationToken ct) =>
            {
                var via = await servico.AdicionarViaAsync(id, requisicao, ct);
                return TypedResults.Created($"/api/cruzamentos/{id}", via);
            })
            .RequireAuthorization(Politicas.SomenteGestor)
            .RequireRateLimiting(PoliticasLimite.Usuario)
            .WithName("AdicionarVia")
            .WithSummary("Adiciona uma via ao cruzamento")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        grupo.MapPut("/{id:guid}/vias/{viaId:guid}", async (Guid id, Guid viaId, AtualizarViaRequest requisicao, CruzamentoService servico, CancellationToken ct) =>
                TypedResults.Ok(await servico.AtualizarViaAsync(id, viaId, requisicao, ct)))
            .RequireAuthorization(Politicas.SomenteGestor)
            .RequireRateLimiting(PoliticasLimite.Usuario)
            .WithName("AtualizarVia")
            .WithSummary("Atualiza uma via")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        grupo.MapDelete("/{id:guid}/vias/{viaId:guid}", async (Guid id, Guid viaId, CruzamentoService servico, CancellationToken ct) =>
            {
                await servico.RemoverViaAsync(id, viaId, ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization(Politicas.SomenteGestor)
            .RequireRateLimiting(PoliticasLimite.Usuario)
            .WithName("RemoverVia")
            .WithSummary("Remove uma via (mínimo de 2 vias ativas; sem sensor instalado)")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        // ---------- Motor semafórico ----------

        grupo.MapGet("/{id:guid}/plano-vigente", async (Guid id, PlanoSemaforicoService servico, CancellationToken ct) =>
                TypedResults.Ok(await servico.ObterVigenteAsync(id, ct)))
            .RequireAuthorization(Politicas.Pessoa)
            .RequireRateLimiting(PoliticasLimite.Usuario)
            .WithName("PlanoVigente")
            .WithSummary("Plano semafórico em vigor, com a justificativa da decisão")
            .WithDescription("Transparência: qualquer pessoa logada vê por que o semáforo priorizou cada via.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapGet("/{id:guid}/planos", async (Guid id, [AsParameters] ParametrosPaginacao paginacao, PlanoSemaforicoService servico, CancellationToken ct) =>
                TypedResults.Ok(await servico.ListarHistoricoAsync(id, paginacao, ct)))
            .RequireAuthorization(Politicas.SomenteGestor)
            .RequireRateLimiting(PoliticasLimite.Usuario)
            .WithName("HistoricoPlanos")
            .WithSummary("Histórico de planos do cruzamento (auditoria)")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapPost("/{id:guid}/planos/recalcular", async (Guid id, PlanoSemaforicoService servico, CancellationToken ct) =>
                TypedResults.Ok(await servico.RecalcularAsync(id, ct)))
            .RequireAuthorization(Politicas.SomenteGestor)
            .RequireRateLimiting(PoliticasLimite.Usuario)
            .WithName("RecalcularPlano")
            .WithSummary("Força o recálculo imediato do plano")
            .WithDescription("Operação naturalmente idempotente: com os mesmos dados de entrada o plano não muda e nenhum registro novo é criado.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        grupo.MapGet("/{id:guid}/indicadores", async (Guid id, PlanoSemaforicoService servico, CancellationToken ct) =>
                TypedResults.Ok(await servico.ObterIndicadoresAsync(id, ct)))
            .RequireAuthorization(Politicas.SomenteGestor)
            .RequireRateLimiting(PoliticasLimite.Usuario)
            .WithName("IndicadoresImpacto")
            .WithSummary("Mede o impacto: tempo fixo × adaptativo")
            .WithDescription("Atraso médio por veículo (fórmula de Webster), capacidade, horas de espera e CO2 evitados por hora.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        // ---------- Sensores do cruzamento ----------

        grupo.MapGet("/{id:guid}/sensores", async (Guid id, SensorService servico, CancellationToken ct) =>
                TypedResults.Ok(await servico.ListarPorCruzamentoAsync(id, ct)))
            .RequireAuthorization(Politicas.SomenteGestor)
            .RequireRateLimiting(PoliticasLimite.Usuario)
            .WithTags("Sensores")
            .WithName("ListarSensores")
            .WithSummary("Lista os sensores do cruzamento (online/offline)")
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapPost("/{id:guid}/sensores", async (Guid id, CriarSensorRequest requisicao, SensorService servico, CancellationToken ct) =>
            {
                var criado = await servico.CriarAsync(id, requisicao, ct);
                return TypedResults.Created($"/api/cruzamentos/{id}/sensores", criado);
            })
            .RequireAuthorization(Politicas.SomenteGestor)
            .RequireRateLimiting(PoliticasLimite.Usuario)
            .WithTags("Sensores")
            .WithName("CriarSensor")
            .WithSummary("Cadastra um sensor em uma via (o segredo aparece só nesta resposta)")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        // ---------- Prioridades (idempotentes) ----------

        grupo.MapPost("/{id:guid}/travessia-acessivel", async Task<Results<Created<SolicitacaoTravessiaResponse>, Ok<SolicitacaoTravessiaResponse>>> (
                Guid id,
                SolicitarTravessiaRequest requisicao,
                [FromHeader(Name = ExtensoesIdempotencia.Cabecalho)] string idempotencyKey,
                ClaimsPrincipal usuario,
                PrioridadeService servico,
                CancellationToken ct) =>
            {
                var resultado = await servico.SolicitarTravessiaAsync(id, usuario.ObterId(), requisicao, ct);
                return resultado.Criada
                    ? TypedResults.Created($"/api/cruzamentos/{id}/status", resultado.Solicitacao)
                    : TypedResults.Ok(resultado.Solicitacao);
            })
            .RequireAuthorization(Politicas.SomenteCidadao)
            .RequireRateLimiting(PoliticasLimite.Usuario)
            .ComIdempotencia()
            .WithTags("Acessibilidade")
            .WithName("SolicitarTravessiaAcessivel")
            .WithSummary("Botoeira virtual: pede mais tempo para atravessar")
            .WithDescription("Para pessoas com deficiência, idosas, gestantes etc. A fase de pedestres passa a ser calculada a 0,8 m/s por 5 minutos. " +
                             "Retorna 201 na criação e 200 se o cidadão já tiver uma solicitação vigente.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapPost("/{id:guid}/emergencias", async (
                Guid id,
                AcionarEmergenciaRequest requisicao,
                [FromHeader(Name = ExtensoesIdempotencia.Cabecalho)] string idempotencyKey,
                ClaimsPrincipal usuario,
                PrioridadeService servico,
                CancellationToken ct) =>
            {
                var acionamento = await servico.AcionarEmergenciaAsync(id, usuario.ObterId(), requisicao, ct);
                return TypedResults.Created($"/api/emergencias/{acionamento.Id}", acionamento);
            })
            .RequireAuthorization(Politicas.SomenteGestor)
            .RequireRateLimiting(PoliticasLimite.Usuario)
            .ComIdempotencia()
            .WithTags("Emergências")
            .WithName("AcionarEmergencia")
            .WithSummary("Aciona corredor verde para veículo de emergência")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
