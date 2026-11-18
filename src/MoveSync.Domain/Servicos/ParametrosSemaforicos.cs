namespace MoveSync.Domain.Servicos;

/// <summary>
/// Parâmetros de engenharia de tráfego e de segurança usados pelo motor de cálculo.
/// Os valores padrão podem ser sobrescritos no appsettings (seção "Semaforos").
///
/// Regra de ouro do projeto: SEGURANÇA VEM ANTES DE EFICIÊNCIA.
/// Os limites mínimos (verde mínimo, amarelo, vermelho geral e travessia de pedestres)
/// nunca são reduzidos pelo algoritmo, nem mesmo em emergência.
/// </summary>
public sealed record ParametrosSemaforicos
{
    /// <summary>Verde mínimo de segurança por via (s).</summary>
    public int VerdeMinimoSegundos { get; init; } = 15;

    /// <summary>Verde máximo para que nenhuma via espere demais (s).</summary>
    public int VerdeMaximoSegundos { get; init; } = 90;

    /// <summary>Verde aplicado no modo de tempo fixo e no modo de contingência (s).</summary>
    public int VerdeFixoSegundos { get; init; } = 30;

    /// <summary>Vermelho geral (todas as vias fechadas) entre fases (s).</summary>
    public int VermelhoGeralSegundos { get; init; } = 2;

    public int CicloMinimoSegundos { get; init; } = 60;

    public int CicloMaximoSegundos { get; init; } = 150;

    /// <summary>Fluxo de saturação: veículos/hora que uma faixa escoa com verde contínuo.</summary>
    public double FluxoSaturacaoPorFaixa { get; init; } = 1800;

    /// <summary>Taxa de ocupação total a partir da qual o ciclo vai ao máximo.</summary>
    public double TaxaOcupacaoCritica { get; init; } = 0.95;

    /// <summary>Velocidade de caminhada de referência (m/s).</summary>
    public double VelocidadePedestrePadrao { get; init; } = 1.2;

    /// <summary>Velocidade de caminhada para travessia acessível (m/s).</summary>
    public double VelocidadePedestreAcessivel { get; init; } = 0.8;

    /// <summary>Tempo de reação/entrada do pedestre na faixa (s).</summary>
    public int TempoEntradaPedestreSegundos { get; init; } = 4;

    /// <summary>Janela de dados considerada "recente" para o cálculo (min).</summary>
    public int JanelaDadosMinutos { get; init; } = 5;

    /// <summary>Peso da fila acumulada na demanda (heurística: metade da fila escoada por ciclo de referência).</summary>
    public double PesoFila { get; init; } = 0.5;

    /// <summary>Ciclo de referência usado para converter fila em demanda horária (s).</summary>
    public int CicloReferenciaFilaSegundos { get; init; } = 90;

    /// <summary>Diferença mínima (s) em algum verde para registrar um novo plano.</summary>
    public int ToleranciaMudancaPlanoSegundos { get; init; } = 2;

    /// <summary>Emissão estimada de um veículo parado em marcha lenta (g CO2/s): ~0,8 L/h × 2,3 kg CO2/L.</summary>
    public double EmissaoMarchaLentaGramasPorSegundo { get; init; } = 0.5;

    /// <summary>
    /// Tempo de amarelo conforme a velocidade regulamentada da via
    /// (referência: Manual Brasileiro de Sinalização de Trânsito – Vol. V, Sinalização Semafórica).
    /// </summary>
    public static int TempoAmareloSegundos(int velocidadeMaximaKmh) =>
        velocidadeMaximaKmh switch
        {
            <= 40 => 3,
            <= 60 => 4,
            _ => 5
        };
}
