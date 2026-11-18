using MoveSync.Application.Abstracoes;
using MoveSync.Application.Contratos;
using MoveSync.Application.Excecoes;
using MoveSync.Domain.Comum;
using MoveSync.Domain.Entidades;
using MoveSync.Domain.Servicos;

namespace MoveSync.Application.Servicos;

public sealed record ResultadoSolicitacaoTravessia(SolicitacaoTravessiaResponse Solicitacao, bool Criada);

/// <summary>
/// Prioridades que mudam o semáforo na hora:
///  • Travessia acessível (cidadão) → amplia a fase de pedestres.
///  • Emergência (gestor/central) → corredor verde para a via do veículo.
/// </summary>
public sealed class PrioridadeService(
    ICruzamentoRepository cruzamentos,
    ISolicitacaoTravessiaRepository travessias,
    IEmergenciaRepository emergencias,
    IUnitOfWork unitOfWork,
    PlanoSemaforicoService planoService,
    CalculadoraPlanoSemaforico calculadora,
    INotificadorNucleo notificador,
    TimeProvider relogio)
{
    public async Task<ResultadoSolicitacaoTravessia> SolicitarTravessiaAsync(
        Guid cruzamentoId,
        Guid usuarioId,
        SolicitarTravessiaRequest requisicao,
        CancellationToken ct)
    {
        var cruzamento = await ObterCruzamentoAtivoAsync(cruzamentoId, ct);
        var agora = Agora();

        // Regra de negócio idempotente: se o cidadão já tem uma solicitação vigente, devolvemos a mesma.
        var vigente = await travessias.ObterVigenteDoUsuarioAsync(cruzamentoId, usuarioId, agora, ct);
        if (vigente is not null)
        {
            return new ResultadoSolicitacaoTravessia(ParaResposta(vigente, cruzamento, "Você já tem uma travessia acessível ativa neste cruzamento."), Criada: false);
        }

        var solicitacao = new SolicitacaoTravessia(cruzamentoId, usuarioId, requisicao.Motivo!.Value, agora);
        await travessias.AdicionarAsync(solicitacao, ct);
        await unitOfWork.SalvarAsync(ct);
        await planoService.RecalcularAsync(cruzamentoId, ct);

        return new ResultadoSolicitacaoTravessia(
            ParaResposta(solicitacao, cruzamento, "Travessia acessível ativada: aguarde o sinal de pedestres, o tempo para atravessar foi ampliado."),
            Criada: true);
    }

    public async Task<EmergenciaResponse> AcionarEmergenciaAsync(
        Guid cruzamentoId,
        Guid solicitanteId,
        AcionarEmergenciaRequest requisicao,
        CancellationToken ct)
    {
        var cruzamento = await ObterCruzamentoAtivoAsync(cruzamentoId, ct);
        var via = cruzamento.ObterVia(requisicao.ViaId!.Value);
        if (!via.Ativa)
        {
            throw new RegraNegocioException("VIA_INATIVA", "A via informada está desativada.");
        }

        var agora = Agora();
        if (await emergencias.ObterAtivaAsync(cruzamentoId, agora, ct) is not null)
        {
            throw new RegraNegocioException(
                "EMERGENCIA_JA_ATIVA",
                "Já existe um corredor de emergência ativo neste cruzamento. Encerre-o antes de acionar outro.",
                TipoViolacao.Conflito);
        }

        var acionamento = new AcionamentoEmergencia(
            cruzamentoId,
            via.Id,
            solicitanteId,
            requisicao.TipoVeiculo!.Value,
            requisicao.DuracaoSegundos,
            agora);
        await emergencias.AdicionarAsync(acionamento, ct);
        await unitOfWork.SalvarAsync(ct);

        var plano = await planoService.RecalcularAsync(cruzamentoId, ct);
        await notificador.NotificarAsync(
            new EventoIntegracao("EMERGENCIA_ACIONADA", cruzamentoId, $"{acionamento.TipoVeiculo} pela via {via.Nome}", agora),
            ct);

        return ParaResposta(acionamento, agora, plano);
    }

    public async Task<EmergenciaResponse> EncerrarEmergenciaAsync(Guid acionamentoId, CancellationToken ct)
    {
        var acionamento = await emergencias.ObterPorIdAsync(acionamentoId, ct)
            ?? throw new RecursoNaoEncontradoException("Acionamento de emergência", acionamentoId);
        var agora = Agora();
        acionamento.Encerrar(agora);
        await unitOfWork.SalvarAsync(ct);

        PlanoResponse? plano = null;
        var cruzamento = await cruzamentos.ObterPorIdAsync(acionamento.CruzamentoId, ct);
        if (cruzamento is { Ativo: true })
        {
            plano = await planoService.RecalcularAsync(acionamento.CruzamentoId, ct);
        }

        return ParaResposta(acionamento, agora, plano);
    }

    public async Task<EmergenciaResponse> ObterEmergenciaAsync(Guid acionamentoId, CancellationToken ct)
    {
        var acionamento = await emergencias.ObterPorIdAsync(acionamentoId, ct)
            ?? throw new RecursoNaoEncontradoException("Acionamento de emergência", acionamentoId);
        return ParaResposta(acionamento, Agora(), null);
    }

    public async Task<IReadOnlyList<EmergenciaResponse>> ListarEmergenciasAtivasAsync(CancellationToken ct)
    {
        var agora = Agora();
        var ativas = await emergencias.ListarAtivasAsync(agora, ct);
        return ativas.Select(a => ParaResposta(a, agora, null)).ToList();
    }

    private SolicitacaoTravessiaResponse ParaResposta(SolicitacaoTravessia s, Cruzamento c, string mensagem) =>
        new(
            s.Id,
            s.CruzamentoId,
            s.Motivo,
            s.SolicitadaEm,
            s.ExpiraEm,
            calculadora.CalcularTempoTravessia(c.LarguraTravessiaMetros, acessivel: true),
            calculadora.CalcularTempoTravessia(c.LarguraTravessiaMetros, acessivel: false),
            mensagem);

    private static EmergenciaResponse ParaResposta(AcionamentoEmergencia a, DateTime agora, PlanoResponse? plano) =>
        new(a.Id, a.CruzamentoId, a.ViaId, a.TipoVeiculo, a.AcionadoEm, a.ExpiraEm, a.EncerradoEm, a.EstaAtivo(agora), plano);

    private async Task<Cruzamento> ObterCruzamentoAtivoAsync(Guid cruzamentoId, CancellationToken ct)
    {
        var cruzamento = await cruzamentos.ObterPorIdAsync(cruzamentoId, ct)
            ?? throw new RecursoNaoEncontradoException("Cruzamento", cruzamentoId);
        if (!cruzamento.Ativo)
        {
            throw new RegraNegocioException("CRUZAMENTO_INATIVO", "O cruzamento está desativado.");
        }

        return cruzamento;
    }

    private DateTime Agora() => relogio.GetUtcNow().UtcDateTime;
}
