using MoveSync.Domain.Enums;

namespace MoveSync.Domain.Servicos;

/// <summary>
/// Converte um plano em informação útil para o cidadão (espera estimada, nível de congestionamento)
/// e mede o impacto da solução comparando o plano adaptativo com o tempo fixo convencional.
/// </summary>
public sealed class AnalisadorDesempenho
{
    private const double SaturacaoMaximaFormula = 0.95;
    private readonly CalculadoraPlanoSemaforico _calculadora;

    public AnalisadorDesempenho(CalculadoraPlanoSemaforico calculadora)
    {
        _calculadora = calculadora ?? throw new ArgumentNullException(nameof(calculadora));
    }

    /// <summary>
    /// Espera de quem chega no vermelho, supondo chegadas uniformes:
    /// média = (C − g)² / (2C) e pior caso = C − g.
    /// Nível pelo grau de saturação x = q / (s · g / C): &lt; 0,6 livre, &lt; 0,85 moderado, senão intenso.
    /// </summary>
    public static EstimativaEspera EstimarEspera(int cicloSegundos, int verdeSegundos, double fluxoVeiculosHora, double fluxoSaturacao, bool possuiDados)
    {
        if (cicloSegundos <= 0 || verdeSegundos <= 0 || verdeSegundos > cicloSegundos)
        {
            throw new ArgumentOutOfRangeException(nameof(verdeSegundos), "Verde deve ser positivo e menor ou igual ao ciclo.");
        }

        var vermelho = cicloSegundos - verdeSegundos;
        var esperaMedia = (int)Math.Round(vermelho * vermelho / (2.0 * cicloSegundos), MidpointRounding.AwayFromZero);

        if (!possuiDados || fluxoSaturacao <= 0)
        {
            return new EstimativaEspera(esperaMedia, vermelho, 0, NivelCongestionamento.Indeterminado);
        }

        var capacidade = fluxoSaturacao * verdeSegundos / cicloSegundos;
        var grau = Math.Round(fluxoVeiculosHora / capacidade, 2);
        var nivel = grau switch
        {
            < 0.6 => NivelCongestionamento.Livre,
            < 0.85 => NivelCongestionamento.Moderado,
            _ => NivelCongestionamento.Intenso
        };

        return new EstimativaEspera(esperaMedia, vermelho, grau, nivel);
    }

    /// <summary>Espera média do pedestre até a próxima fase de pedestres.</summary>
    public static int EstimarEsperaPedestre(int cicloSegundos, int tempoTravessiaSegundos)
    {
        var aguardando = Math.Max(cicloSegundos - tempoTravessiaSegundos, 0);
        return (int)Math.Round(aguardando * aguardando / (2.0 * cicloSegundos), MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Compara, para a MESMA demanda, o plano de tempo fixo com o plano adaptativo.
    /// Atraso por veículo pela fórmula de Webster: d = 0,9 · [ C(1−λ)² / (2(1−λx)) + x² / (2q(1−x)) ],
    /// onde λ = g/C, x = grau de saturação (limitado a 0,95) e q = fluxo em veíc/s.
    /// </summary>
    public ResultadoComparacao Comparar(EntradaCalculoPlano entrada)
    {
        var entradaAdaptativa = entrada with { ModoConfigurado = ModoOperacao.Adaptativo, ViaEmergenciaId = null };
        var adaptativo = _calculadora.Calcular(entradaAdaptativa);
        var fixo = _calculadora.CalcularTempoFixo(entradaAdaptativa, ModoPlano.TempoFixo, "Referência: tempo fixo convencional.");

        var indicadoresFixo = Indicadores(entrada.Vias, fixo);
        var indicadoresAdaptativo = Indicadores(entrada.Vias, adaptativo);
        var demandaTotal = entrada.Vias.Sum(_calculadora.CalcularDemandaVeiculosHora);

        var reducao = indicadoresFixo.AtrasoMedioSegundosPorVeiculo <= 0
            ? 0
            : (indicadoresFixo.AtrasoMedioSegundosPorVeiculo - indicadoresAdaptativo.AtrasoMedioSegundosPorVeiculo)
              / indicadoresFixo.AtrasoMedioSegundosPorVeiculo * 100;

        var segundosEvitadosPorHora = Math.Max(
            (indicadoresFixo.AtrasoMedioSegundosPorVeiculo - indicadoresAdaptativo.AtrasoMedioSegundosPorVeiculo) * demandaTotal,
            0);
        var co2Kg = segundosEvitadosPorHora * _calculadora.Parametros.EmissaoMarchaLentaGramasPorSegundo / 1000;

        return new ResultadoComparacao(
            indicadoresFixo,
            indicadoresAdaptativo,
            Math.Round(demandaTotal, 1),
            Math.Round(reducao, 1),
            Math.Round(segundosEvitadosPorHora / 3600, 2),
            Math.Round(co2Kg, 2));
    }

    public static double AtrasoWebster(int ciclo, int verde, double fluxoVeiculosHora, double fluxoSaturacao)
    {
        if (fluxoVeiculosHora <= 0)
        {
            return 0;
        }

        var lambda = (double)verde / ciclo;
        var capacidade = fluxoSaturacao * lambda;
        var x = Math.Min(fluxoVeiculosHora / capacidade, SaturacaoMaximaFormula);
        var q = fluxoVeiculosHora / 3600.0;

        var atrasoUniforme = ciclo * Math.Pow(1 - lambda, 2) / (2 * (1 - (lambda * x)));
        var atrasoAleatorio = x * x / (2 * q * (1 - x));
        return 0.9 * (atrasoUniforme + atrasoAleatorio);
    }

    private IndicadoresPlano Indicadores(IReadOnlyList<DemandaVia> vias, PlanoCalculado plano)
    {
        double somaAtrasoPonderado = 0;
        double somaFluxo = 0;
        double capacidade = 0;

        foreach (var via in vias)
        {
            var fase = plano.Fases.First(f => f.ViaId == via.ViaId);
            var fluxo = _calculadora.CalcularDemandaVeiculosHora(via);
            var saturacao = _calculadora.CalcularFluxoSaturacao(via);
            somaAtrasoPonderado += AtrasoWebster(plano.CicloSegundos, fase.VerdeSegundos, fluxo, saturacao) * fluxo;
            somaFluxo += fluxo;
            capacidade += saturacao * fase.VerdeSegundos / plano.CicloSegundos;
        }

        var atrasoMedio = somaFluxo <= 0 ? 0 : somaAtrasoPonderado / somaFluxo;
        var veiculosPorCiclo = capacidade * plano.CicloSegundos / 3600;
        return new IndicadoresPlano(
            plano.CicloSegundos,
            Math.Round(atrasoMedio, 1),
            Math.Round(capacidade, 0),
            Math.Round(veiculosPorCiclo, 1));
    }
}
