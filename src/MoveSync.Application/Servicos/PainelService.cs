using MoveSync.Application.Abstracoes;
using MoveSync.Application.Contratos;
using MoveSync.Domain.Servicos;

namespace MoveSync.Application.Servicos;

/// <summary>Visão consolidada para o gestor de trânsito (tela inicial do painel).</summary>
public sealed class PainelService(
    ICruzamentoRepository cruzamentos,
    ISensorRepository sensores,
    ILeituraRepository leituras,
    IPlanoRepository planos,
    IOcorrenciaRepository ocorrencias,
    IEmergenciaRepository emergencias,
    ParametrosSemaforicos parametros,
    TimeProvider relogio)
{
    public async Task<PainelResumoResponse> ObterResumoAsync(CancellationToken ct)
    {
        var agora = relogio.GetUtcNow().UtcDateTime;
        var tolerancia = TimeSpan.FromMinutes(parametros.JanelaDadosMinutos);

        var ativos = await cruzamentos.ContarAsync(true, ct);
        var inativos = await cruzamentos.ContarAsync(false, ct);
        var todosSensores = await sensores.ListarAsync(null, ct);
        var sensoresAtivos = todosSensores.Where(s => s.Ativo).ToList();
        var offline = sensoresAtivos.Where(s => s.EstaOffline(agora, tolerancia)).Select(s => s.Codigo).OrderBy(c => c).ToList();

        return new PainelResumoResponse(
            ativos,
            inativos,
            await planos.ContarVigentesPorModoAsync(ct),
            sensoresAtivos.Count,
            offline.Count,
            offline,
            await leituras.ContarDesdeAsync(agora.AddHours(-1), ct),
            await ocorrencias.ContarPendentesPorPrioridadeAsync(ct),
            (await emergencias.ListarAtivasAsync(agora, ct)).Count,
            agora);
    }
}
