using System.Globalization;
using System.Text;
using MoveSync.Domain.Comum;
using MoveSync.Domain.Enums;

namespace MoveSync.Domain.Servicos;

/// <summary>
/// Coração da MoveSync: transforma a demanda medida pelos sensores em tempos de semáforo.
///
/// Ordem de decisão (da mais prioritária para a menos):
///  1. Emergência ativa  → corredor verde (via da emergência no verde máximo, demais no mínimo).
///  2. Gestor fixou "TempoFixo" → todas as vias com o verde fixo.
///  3. Falta de dados recentes em alguma via → CONTINGÊNCIA (tempo fixo seguro).
///  4. Caso normal → ADAPTATIVO pelo método de Webster.
///
/// Método de Webster (1958), usado em engenharia de tráfego:
///  • y = q / s   (taxa de ocupação da via: fluxo que chega ÷ fluxo que a via consegue escoar)
///  • Y = Σ y     (ocupação total do cruzamento)
///  • L = Σ (amarelo + vermelho geral) + fase de pedestres   (tempo "perdido" para os veículos)
///  • Ciclo ótimo C = (1,5·L + 5) / (1 − Y), limitado entre ciclo mínimo e máximo
///  • Verde de cada via = (C − L) · y / Y, limitado entre verde mínimo e máximo
/// </summary>
public sealed class CalculadoraPlanoSemaforico
{
    private static readonly CultureInfo Formato = CultureInfo.InvariantCulture;
    private readonly ParametrosSemaforicos _p;

    public CalculadoraPlanoSemaforico(ParametrosSemaforicos parametros)
    {
        _p = parametros ?? throw new ArgumentNullException(nameof(parametros));
    }

    public ParametrosSemaforicos Parametros => _p;

    public PlanoCalculado Calcular(EntradaCalculoPlano entrada)
    {
        ValidarEntrada(entrada);
        var tempoPedestre = CalcularTempoTravessia(entrada.LarguraTravessiaMetros, entrada.TravessiaAcessivelAtiva);

        if (entrada.ViaEmergenciaId is { } viaEmergenciaId)
        {
            return CalcularEmergencia(entrada, viaEmergenciaId, tempoPedestre);
        }

        if (entrada.ModoConfigurado == ModoOperacao.TempoFixo)
        {
            return CalcularTempoFixo(
                entrada,
                ModoPlano.TempoFixo,
                $"Cruzamento configurado pelo gestor em tempo fixo: {_p.VerdeFixoSegundos}s de verde por via.");
        }

        var semDados = entrada.Vias.Where(v => !v.PossuiDadosRecentes).Select(v => v.NomeVia).ToList();
        if (semDados.Count > 0)
        {
            return CalcularTempoFixo(
                entrada,
                ModoPlano.Contingencia,
                $"Modo de contingência: sem leituras nos últimos {_p.JanelaDadosMinutos} min para " +
                $"{string.Join(", ", semDados)}. Aplicado tempo fixo seguro de {_p.VerdeFixoSegundos}s " +
                "por via até os sensores voltarem a enviar dados.");
        }

        return CalcularAdaptativo(entrada, tempoPedestre);
    }

    /// <summary>Plano de tempo fixo (usado também como referência na comparação de desempenho).</summary>
    public PlanoCalculado CalcularTempoFixo(EntradaCalculoPlano entrada, ModoPlano modo, string motivo)
    {
        ValidarEntrada(entrada);
        var tempoPedestre = CalcularTempoTravessia(entrada.LarguraTravessiaMetros, entrada.TravessiaAcessivelAtiva);
        var fases = entrada.Vias
            .Select(v => CriarFase(v, _p.VerdeFixoSegundos))
            .ToList();
        return Montar(modo, fases, tempoPedestre, entrada.TravessiaAcessivelAtiva, motivo);
    }

    /// <summary>Demanda horária da via: fluxo medido + parcela da fila acumulada.</summary>
    public double CalcularDemandaVeiculosHora(DemandaVia via)
    {
        var fluxoMedido = via.VeiculosNaJanela * 60.0 / _p.JanelaDadosMinutos;
        var demandaFila = _p.PesoFila * via.FilaAtual * 3600.0 / _p.CicloReferenciaFilaSegundos;
        return fluxoMedido + demandaFila;
    }

    public double CalcularFluxoSaturacao(DemandaVia via) => _p.FluxoSaturacaoPorFaixa * via.NumeroFaixas;

    /// <summary>Tempo da fase de pedestres = largura ÷ velocidade de caminhada + tempo de entrada.</summary>
    public int CalcularTempoTravessia(double larguraMetros, bool acessivel)
    {
        var velocidade = acessivel ? _p.VelocidadePedestreAcessivel : _p.VelocidadePedestrePadrao;
        return (int)Math.Ceiling(larguraMetros / velocidade) + _p.TempoEntradaPedestreSegundos;
    }

    private PlanoCalculado CalcularAdaptativo(EntradaCalculoPlano entrada, int tempoPedestre)
    {
        var vias = entrada.Vias;
        var demandas = vias.Select(CalcularDemandaVeiculosHora).ToArray();
        var ocupacoes = vias.Select((v, i) => demandas[i] / CalcularFluxoSaturacao(v)).ToArray();
        var ocupacaoTotal = ocupacoes.Sum();

        if (ocupacaoTotal <= 0.0001)
        {
            var fasesMinimas = vias.Select(v => CriarFase(v, _p.VerdeMinimoSegundos)).ToList();
            return Montar(
                ModoPlano.Adaptativo,
                fasesMinimas,
                tempoPedestre,
                entrada.TravessiaAcessivelAtiva,
                "Nenhum veículo detectado na janela: aplicado o verde mínimo de segurança em todas as vias, " +
                "o que reduz a espera de pedestres e de quem chegar.");
        }

        var tempoPerdido = vias.Sum(v => ParametrosSemaforicos.TempoAmareloSegundos(v.VelocidadeMaximaKmh))
            + (vias.Count * _p.VermelhoGeralSegundos)
            + tempoPedestre;

        int ciclo;
        if (ocupacaoTotal >= _p.TaxaOcupacaoCritica)
        {
            ciclo = _p.CicloMaximoSegundos;
        }
        else
        {
            var cicloWebster = ((1.5 * tempoPerdido) + 5) / (1 - ocupacaoTotal);
            ciclo = Math.Clamp((int)Math.Round(cicloWebster, MidpointRounding.AwayFromZero), _p.CicloMinimoSegundos, _p.CicloMaximoSegundos);
        }

        var verdeDisponivel = Math.Max(ciclo - tempoPerdido, vias.Count * _p.VerdeMinimoSegundos);

        var fases = new List<FaseCalculada>(vias.Count);
        for (var i = 0; i < vias.Count; i++)
        {
            var verdeIdeal = verdeDisponivel * ocupacoes[i] / ocupacaoTotal;
            var verde = Math.Clamp(
                (int)Math.Round(verdeIdeal, MidpointRounding.AwayFromZero),
                _p.VerdeMinimoSegundos,
                _p.VerdeMaximoSegundos);
            fases.Add(CriarFase(vias[i], verde, demandas[i], ocupacoes[i]));
        }

        var justificativa = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"Plano adaptativo (método de Webster). Ocupação total do cruzamento Y={ocupacaoTotal.ToString("0.00", Formato)}")
            .Append(ocupacaoTotal >= _p.TaxaOcupacaoCritica ? " (crítica: ciclo no máximo). " : ". ");

        var demandaTotal = demandas.Sum();
        foreach (var fase in fases.OrderByDescending(f => f.FluxoVeiculosHora))
        {
            var participacao = demandaTotal <= 0 ? 0 : fase.FluxoVeiculosHora / demandaTotal * 100;
            justificativa.Append(
                CultureInfo.InvariantCulture,
                $"{fase.NomeVia}: {Math.Round(fase.FluxoVeiculosHora)} veíc/h ({Math.Round(participacao)}% da demanda) → {fase.VerdeSegundos}s de verde. ");
        }

        var limitadas = fases.Where(f => f.VerdeSegundos == _p.VerdeMaximoSegundos).Select(f => f.NomeVia).ToList();
        if (limitadas.Count > 0)
        {
            justificativa.Append(
                CultureInfo.InvariantCulture,
                $"Verde limitado ao máximo de {_p.VerdeMaximoSegundos}s em {string.Join(", ", limitadas)} para que as outras vias não esperem demais. ");
        }

        return Montar(ModoPlano.Adaptativo, fases, tempoPedestre, entrada.TravessiaAcessivelAtiva, justificativa.ToString().TrimEnd());
    }

    private PlanoCalculado CalcularEmergencia(EntradaCalculoPlano entrada, Guid viaEmergenciaId, int tempoPedestre)
    {
        var viaEmergencia = entrada.Vias.FirstOrDefault(v => v.ViaId == viaEmergenciaId)
            ?? throw new RegraNegocioException(
                "VIA_EMERGENCIA_INVALIDA",
                "A via da emergência não pertence às vias ativas do cruzamento.");

        var fases = entrada.Vias
            .Select(v => CriarFase(v, v.ViaId == viaEmergenciaId ? _p.VerdeMaximoSegundos : _p.VerdeMinimoSegundos))
            .ToList();

        return Montar(
            ModoPlano.Emergencia,
            fases,
            tempoPedestre,
            entrada.TravessiaAcessivelAtiva,
            $"Corredor de emergência: {viaEmergencia.NomeVia} recebe {_p.VerdeMaximoSegundos}s de verde e as demais vias o " +
            $"mínimo de segurança ({_p.VerdeMinimoSegundos}s). Amarelo, vermelho geral e travessia de pedestres foram mantidos.");
    }

    private FaseCalculada CriarFase(DemandaVia via, int verde, double? demanda = null, double? ocupacao = null)
    {
        var fluxo = demanda ?? CalcularDemandaVeiculosHora(via);
        return new FaseCalculada(
            via.ViaId,
            via.NomeVia,
            verde,
            ParametrosSemaforicos.TempoAmareloSegundos(via.VelocidadeMaximaKmh),
            _p.VermelhoGeralSegundos,
            Math.Round(fluxo, 1),
            Math.Round(ocupacao ?? (fluxo / CalcularFluxoSaturacao(via)), 3));
    }

    private PlanoCalculado Montar(
        ModoPlano modo,
        IReadOnlyList<FaseCalculada> fases,
        int tempoPedestre,
        bool travessiaAcessivel,
        string motivo)
    {
        var ciclo = fases.Sum(f => f.VerdeSegundos + f.AmareloSegundos + f.VermelhoGeralSegundos) + tempoPedestre;
        var justificativa = new StringBuilder(motivo);
        justificativa.Append(CultureInfo.InvariantCulture, $" Ciclo total de {ciclo}s, com fase exclusiva de pedestres de {tempoPedestre}s");
        justificativa.Append(travessiaAcessivel
            ? $" (travessia acessível ativa: calculada a {_p.VelocidadePedestreAcessivel.ToString("0.0", Formato)} m/s)."
            : ".");
        return new PlanoCalculado(modo, ciclo, tempoPedestre, travessiaAcessivel, fases, justificativa.ToString());
    }

    private static void ValidarEntrada(EntradaCalculoPlano entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        if (entrada.Vias is null || entrada.Vias.Count < 2)
        {
            throw new RegraNegocioException(
                "VIAS_INSUFICIENTES",
                "O cruzamento precisa de ao menos 2 vias ativas para gerar um plano semafórico.");
        }

        if (entrada.Vias.Select(v => v.ViaId).Distinct().Count() != entrada.Vias.Count)
        {
            throw new RegraNegocioException("VIAS_DUPLICADAS", "Há vias repetidas na entrada do cálculo.");
        }
    }
}
