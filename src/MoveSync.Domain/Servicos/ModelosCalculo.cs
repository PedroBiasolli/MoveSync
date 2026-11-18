using MoveSync.Domain.Enums;

namespace MoveSync.Domain.Servicos;

/// <summary>Demanda medida em uma via dentro da janela de dados.</summary>
public sealed record DemandaVia(
    Guid ViaId,
    string NomeVia,
    int NumeroFaixas,
    int VelocidadeMaximaKmh,
    int VeiculosNaJanela,
    int FilaAtual,
    bool PossuiDadosRecentes);

/// <summary>Tudo o que o motor precisa para gerar um plano semafórico.</summary>
public sealed record EntradaCalculoPlano(
    ModoOperacao ModoConfigurado,
    double LarguraTravessiaMetros,
    IReadOnlyList<DemandaVia> Vias,
    bool TravessiaAcessivelAtiva = false,
    Guid? ViaEmergenciaId = null);

/// <summary>Tempos de uma fase (via) no plano calculado.</summary>
public sealed record FaseCalculada(
    Guid ViaId,
    string NomeVia,
    int VerdeSegundos,
    int AmareloSegundos,
    int VermelhoGeralSegundos,
    double FluxoVeiculosHora,
    double TaxaOcupacao);

/// <summary>Resultado do motor: ciclo, fases, fase de pedestres e a justificativa em linguagem simples.</summary>
public sealed record PlanoCalculado(
    ModoPlano Modo,
    int CicloSegundos,
    int TempoTravessiaPedestreSegundos,
    bool TravessiaAcessivel,
    IReadOnlyList<FaseCalculada> Fases,
    string Justificativa);

/// <summary>Estimativa de espera que o cidadão vê no app.</summary>
public sealed record EstimativaEspera(
    int EsperaMediaSegundos,
    int EsperaMaximaSegundos,
    double GrauSaturacao,
    NivelCongestionamento Nivel);

/// <summary>Indicadores de um plano para comparação (tempo fixo × adaptativo).</summary>
public sealed record IndicadoresPlano(
    int CicloSegundos,
    double AtrasoMedioSegundosPorVeiculo,
    double CapacidadeVeiculosHora,
    double VeiculosAtendidosPorCiclo);

/// <summary>Comparação de desempenho: é assim que medimos o impacto da solução.</summary>
public sealed record ResultadoComparacao(
    IndicadoresPlano TempoFixo,
    IndicadoresPlano Adaptativo,
    double DemandaTotalVeiculosHora,
    double ReducaoAtrasoPercentual,
    double TempoParadoEvitadoHorasPorHora,
    double Co2EvitadoKgPorHora);
