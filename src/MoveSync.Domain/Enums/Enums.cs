namespace MoveSync.Domain.Enums;

/// <summary>Papéis de quem faz login com e-mail e senha.</summary>
public enum PerfilUsuario
{
    Cidadao = 1,
    Gestor = 2
}

/// <summary>Modo de operação configurado pelo gestor para o cruzamento.</summary>
public enum ModoOperacao
{
    /// <summary>Tempos calculados a partir da demanda medida pelos sensores.</summary>
    Adaptativo = 1,

    /// <summary>Tempos fixos (como um semáforo convencional).</summary>
    TempoFixo = 2
}

/// <summary>Modo efetivamente aplicado em um plano semafórico gerado.</summary>
public enum ModoPlano
{
    Adaptativo = 1,
    TempoFixo = 2,

    /// <summary>Fallback seguro: sensores sem dados recentes, aplica tempo fixo.</summary>
    Contingencia = 3,

    /// <summary>Corredor verde para veículo de emergência.</summary>
    Emergencia = 4
}

public enum TipoSensor
{
    LacoIndutivo = 1,
    Camera = 2
}

public enum NivelCongestionamento
{
    Indeterminado = 0,
    Livre = 1,
    Moderado = 2,
    Intenso = 3
}

public enum TipoOcorrencia
{
    SemaforoApagado = 1,
    SemaforoPiscando = 2,
    TempoTravessiaInsuficiente = 3,
    TravessiaInsegura = 4,
    SensorDanificado = 5,
    Outro = 6
}

public enum StatusOcorrencia
{
    Aberta = 1,
    EmAnalise = 2,
    Resolvida = 3,
    Rejeitada = 4
}

public enum PrioridadeOcorrencia
{
    Baixa = 1,
    Media = 2,
    Alta = 3,
    Critica = 4
}

public enum MotivoTravessiaAcessivel
{
    MobilidadeReduzida = 1,
    DeficienciaVisual = 2,
    PessoaIdosa = 3,
    Gestante = 4,
    AcompanhandoCrianca = 5
}

public enum TipoVeiculoEmergencia
{
    Ambulancia = 1,
    Bombeiros = 2,
    Policia = 3,
    DefesaCivil = 4
}
