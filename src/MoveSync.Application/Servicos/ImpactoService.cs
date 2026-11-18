using System.Globalization;
using MoveSync.Application.Abstracoes;
using MoveSync.Application.Contratos;
using MoveSync.Domain.Servicos;

namespace MoveSync.Application.Servicos;

/// <summary>
/// Relatório de impacto e benefício/custo para a prefeitura — a ponte entre a engenharia de tráfego e o modelo de negócio.
/// Para cada cruzamento ativo compara tempo fixo × adaptativo com a demanda real dos últimos minutos,
/// soma os ganhos e projeta o mês com premissas explícitas (que a prefeitura pode simular pela query string).
/// </summary>
public sealed class ImpactoService(
    ICruzamentoRepository cruzamentos,
    PlanoSemaforicoService planoService,
    CalculadoraImpactoEconomico calculadora,
    PremissasEconomicas premissasPadrao,
    ParametrosSemaforicos parametros,
    TimeProvider relogio)
{
    public async Task<RelatorioImpactoResponse> GerarAsync(ParametrosImpacto simulacao, CancellationToken ct)
    {
        var premissas = premissasPadrao with
        {
            HorasReferenciaPorDia = simulacao.HorasReferenciaPorDia ?? premissasPadrao.HorasReferenciaPorDia,
            ValorHoraCidadaoReais = simulacao.ValorHoraReais ?? premissasPadrao.ValorHoraCidadaoReais,
            AssinaturaMensalPorCruzamentoReais = simulacao.AssinaturaMensalPorCruzamentoReais ?? premissasPadrao.AssinaturaMensalPorCruzamentoReais
        };

        var ativos = await cruzamentos.ListarAtivosAsync(ct);
        var porCruzamento = new List<ImpactoCruzamentoResponse>(ativos.Count);
        var ganhos = new List<GanhoCruzamento>();

        foreach (var cruzamento in ativos)
        {
            var indicadores = await planoService.ObterIndicadoresAsync(cruzamento.Id, ct);
            var comparacao = indicadores.Comparacao;
            porCruzamento.Add(new ImpactoCruzamentoResponse(
                cruzamento.Id,
                cruzamento.Codigo,
                cruzamento.Nome,
                indicadores.DadosSuficientes,
                comparacao?.ReducaoAtrasoPercentual,
                comparacao?.TempoParadoEvitadoHorasPorHora,
                comparacao?.Co2EvitadoKgPorHora));

            if (comparacao is not null)
            {
                ganhos.Add(new GanhoCruzamento(cruzamento.Id, comparacao.TempoParadoEvitadoHorasPorHora, comparacao.Co2EvitadoKgPorHora));
            }
        }

        var resumo = calculadora.Calcular(ganhos, ativos.Count, premissas);

        return new RelatorioImpactoResponse(
            relogio.GetUtcNow().UtcDateTime,
            parametros.JanelaDadosMinutos,
            ativos.Count,
            ganhos.Count,
            new PremissasImpactoResponse(
                premissas.ValorHoraCidadaoReais,
                premissas.HorasReferenciaPorDia,
                premissas.DiasPorMes,
                premissas.AssinaturaMensalPorCruzamentoReais,
                premissas.ImplantacaoPorCruzamentoReais,
                parametros.EmissaoMarchaLentaGramasPorSegundo),
            resumo,
            porCruzamento,
            Interpretar(resumo, premissas, ganhos.Count, ativos.Count));
    }

    private static string Interpretar(ResumoImpactoEconomico r, PremissasEconomicas p, int comDados, int contratados)
    {
        var c = CultureInfo.GetCultureInfo("pt-BR");
        if (contratados == 0)
        {
            return "Nenhum cruzamento ativo cadastrado.";
        }

        if (comDados == 0)
        {
            return "Ainda não há telemetria recente em todas as vias de nenhum cruzamento. Envie leituras dos sensores para medir o impacto.";
        }

        var texto = string.Create(c,
            $"Com a demanda medida agora em {comDados} de {contratados} cruzamento(s), supondo que ela se repita por {p.HorasReferenciaPorDia} h/dia em {p.DiasPorMes} dias/mês, a projeção é de {r.HorasEvitadasPorMes:N0} horas a menos de espera " +
            $"e {r.Co2EvitadoToneladasPorMes:N2} t de CO₂ evitadas por mês, o equivalente a {r.ValorTempoEconomizadoMesReais:C} em tempo devolvido à população, " +
            $"contra {r.AssinaturaMensalReais:C} de assinatura mensal.");

        texto += r.RelacaoBeneficioCusto switch
        {
            null => " Sem custo de assinatura configurado, a relação benefício/custo não se aplica.",
            >= 1 => string.Create(c, $" Relação benefício/custo de {r.RelacaoBeneficioCusto:N2}: cada R$ 1,00 pago devolve {r.RelacaoBeneficioCusto:C} à cidade.") +
                    (r.PaybackSocialImplantacaoMeses is { } meses ? string.Create(c, $" A implantação se paga socialmente em cerca de {meses:N1} mês(es).") : string.Empty),
            _ => string.Create(c, $" Relação benefício/custo de {r.RelacaoBeneficioCusto:N2}: com a demanda atual o ganho ainda não cobre a assinatura; o benefício cresce nos horários de pico e em cruzamentos com demanda desequilibrada.")
        };

        return texto + " Benefício social (tempo da população), não receita da prefeitura; ajuste as premissas pela query string para simular cenários.";
    }
}
