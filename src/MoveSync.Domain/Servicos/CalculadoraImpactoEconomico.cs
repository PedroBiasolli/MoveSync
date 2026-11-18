using MoveSync.Domain.Comum;

namespace MoveSync.Domain.Servicos;

/// <summary>
/// Premissas do modelo de negócio (seção "ModeloNegocio" do appsettings).
/// São HIPÓTESES de referência, explicitadas no relatório para que a prefeitura possa ajustá-las.
/// </summary>
public sealed record PremissasEconomicas
{
    /// <summary>
    /// Valor de 1 hora do cidadão (R$). Referência: rendimento médio de R$ 3.722/mês
    /// (IBGE, PNAD Contínua, 1º tri/2026) ÷ 176 h trabalhadas por mês ≈ R$ 21,15.
    /// </summary>
    public decimal ValorHoraCidadaoReais { get; init; } = 21.15m;

    /// <summary>Horas por dia em que a demanda medida é considerada representativa (picos e entrepicos).</summary>
    public int HorasReferenciaPorDia { get; init; } = 10;

    /// <summary>Dias úteis por mês usados na projeção.</summary>
    public int DiasPorMes { get; init; } = 22;

    /// <summary>Assinatura SaaS por cruzamento/mês (hipótese do plano de negócio).</summary>
    public decimal AssinaturaMensalPorCruzamentoReais { get; init; } = 590m;

    /// <summary>Implantação por cruzamento: kit ESP32/câmera + instalação + integração (hipótese do plano de negócio).</summary>
    public decimal ImplantacaoPorCruzamentoReais { get; init; } = 4_800m;

    public void Validar()
    {
        if (ValorHoraCidadaoReais <= 0 || AssinaturaMensalPorCruzamentoReais < 0 || ImplantacaoPorCruzamentoReais < 0)
        {
            throw new RegraNegocioException("PREMISSA_INVALIDA", "Valores monetários das premissas devem ser positivos.");
        }

        Guarda.Intervalo(HorasReferenciaPorDia, nameof(HorasReferenciaPorDia), 1, 24);
        Guarda.Intervalo(DiasPorMes, nameof(DiasPorMes), 1, 31);
    }
}

/// <summary>Ganho medido em um cruzamento com dados suficientes (vem da comparação tempo fixo × adaptativo).</summary>
public sealed record GanhoCruzamento(Guid CruzamentoId, double HorasEvitadasPorHora, double Co2EvitadoKgPorHora);

/// <summary>Resultado consolidado para a prefeitura.</summary>
public sealed record ResumoImpactoEconomico(
    double HorasEvitadasPorDia,
    double HorasEvitadasPorMes,
    double Co2EvitadoToneladasPorMes,
    decimal ValorTempoEconomizadoMesReais,
    decimal AssinaturaMensalReais,
    decimal ImplantacaoTotalReais,
    double? RelacaoBeneficioCusto,
    double? PaybackSocialImplantacaoMeses);

/// <summary>
/// Converte o ganho operacional (horas paradas e CO₂ evitados) em linguagem de gestor público:
/// benefício social mensal em R$, custo do contrato e relação benefício/custo (B/C).
///
/// • Benefício mensal = horas evitadas/h × horas de referência/dia × dias/mês × valor da hora do cidadão
/// • Custo mensal     = cruzamentos contratados × assinatura mensal
/// • B/C              = benefício mensal ÷ custo mensal (acima de 1 = cada R$ 1 pago devolve mais de R$ 1 à cidade)
/// • Payback social   = implantação total ÷ (benefício mensal − custo mensal), apenas se o saldo for positivo
///
/// É um benefício SOCIAL (tempo e emissões da população), não receita de caixa da prefeitura —
/// é assim que projetos públicos de mobilidade costumam ser avaliados.
/// </summary>
public sealed class CalculadoraImpactoEconomico
{
    public ResumoImpactoEconomico Calcular(IReadOnlyCollection<GanhoCruzamento> ganhos, int cruzamentosContratados, PremissasEconomicas premissas)
    {
        ArgumentNullException.ThrowIfNull(ganhos);
        ArgumentNullException.ThrowIfNull(premissas);
        premissas.Validar();
        if (cruzamentosContratados < 0)
        {
            throw new RegraNegocioException("CAMPO_INVALIDO", "A quantidade de cruzamentos contratados não pode ser negativa.");
        }

        var horasPorHora = ganhos.Sum(g => Math.Max(g.HorasEvitadasPorHora, 0));
        var co2PorHora = ganhos.Sum(g => Math.Max(g.Co2EvitadoKgPorHora, 0));

        var horasPorDia = horasPorHora * premissas.HorasReferenciaPorDia;
        var horasPorMes = horasPorDia * premissas.DiasPorMes;
        var co2ToneladasMes = co2PorHora * premissas.HorasReferenciaPorDia * premissas.DiasPorMes / 1000;

        var beneficio = decimal.Round((decimal)horasPorMes * premissas.ValorHoraCidadaoReais, 2);
        var custo = cruzamentosContratados * premissas.AssinaturaMensalPorCruzamentoReais;
        var implantacao = cruzamentosContratados * premissas.ImplantacaoPorCruzamentoReais;

        double? relacao = custo > 0 && ganhos.Count > 0 ? Math.Round((double)(beneficio / custo), 2) : null;
        var saldo = beneficio - custo;
        double? payback = ganhos.Count > 0 && saldo > 0 && implantacao > 0 ? Math.Round((double)(implantacao / saldo), 1) : null;

        return new ResumoImpactoEconomico(
            Math.Round(horasPorDia, 1),
            Math.Round(horasPorMes, 0),
            Math.Round(co2ToneladasMes, 2),
            beneficio,
            custo,
            implantacao,
            relacao,
            payback);
    }
}
