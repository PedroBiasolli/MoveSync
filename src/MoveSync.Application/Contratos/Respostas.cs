using MoveSync.Domain.Entidades;
using MoveSync.Domain.Enums;
using MoveSync.Domain.Servicos;

namespace MoveSync.Application.Contratos;

// -----------------------------------------------------------------------------
// Contratos de SAÍDA. A API nunca devolve entidades diretamente: assim não vazamos
// campos sensíveis (ex.: hash de senha) e o contrato com o app mobile fica estável.
// -----------------------------------------------------------------------------

public sealed record PaginaResponse<T>(IReadOnlyList<T> Itens, int Pagina, int TamanhoPagina, int TotalItens, int TotalPaginas)
{
    public static PaginaResponse<T> Criar(IReadOnlyList<T> itens, int pagina, int tamanhoPagina, int total) =>
        new(itens, pagina, tamanhoPagina, total, total == 0 ? 0 : (int)Math.Ceiling(total / (double)tamanhoPagina));
}

public sealed record UsuarioResponse(Guid Id, string Nome, string Email, PerfilUsuario Perfil, DateTime CriadoEm)
{
    public static UsuarioResponse De(Usuario u) => new(u.Id, u.Nome, u.Email, u.Perfil, u.CriadoEm);
}

public sealed record TokenResponse(string AccessToken, string TokenType, DateTime ExpiraEm, string Perfil, UsuarioResponse? Usuario);

public sealed record ViaResponse(
    Guid Id,
    string Nome,
    string Sentido,
    int NumeroFaixas,
    int VelocidadeMaximaKmh,
    int AmareloSegundos,
    bool Ativa)
{
    public static ViaResponse De(Via v) => new(
        v.Id,
        v.Nome,
        v.Sentido,
        v.NumeroFaixas,
        v.VelocidadeMaximaKmh,
        ParametrosSemaforicos.TempoAmareloSegundos(v.VelocidadeMaximaKmh),
        v.Ativa);
}

public sealed record CruzamentoResponse(
    Guid Id,
    string Codigo,
    string Nome,
    string Endereco,
    double Latitude,
    double Longitude,
    double LarguraTravessiaMetros,
    ModoOperacao Modo,
    bool Ativo,
    DateTime CriadoEm,
    DateTime AtualizadoEm,
    IReadOnlyList<ViaResponse> Vias)
{
    public static CruzamentoResponse De(Cruzamento c) => new(
        c.Id,
        c.Codigo,
        c.Nome,
        c.Endereco,
        c.Latitude,
        c.Longitude,
        c.LarguraTravessiaMetros,
        c.Modo,
        c.Ativo,
        c.CriadoEm,
        c.AtualizadoEm,
        c.Vias.OrderBy(v => v.Nome).Select(ViaResponse.De).ToList());
}

public sealed record SensorResponse(
    Guid Id,
    string Codigo,
    Guid CruzamentoId,
    Guid ViaId,
    TipoSensor Tipo,
    bool Ativo,
    bool Online,
    DateTime CriadoEm,
    DateTime? UltimaLeituraEm)
{
    public static SensorResponse De(Sensor s, DateTime agoraUtc, TimeSpan tolerancia) => new(
        s.Id,
        s.Codigo,
        s.CruzamentoId,
        s.ViaId,
        s.Tipo,
        s.Ativo,
        s.Ativo && !s.EstaOffline(agoraUtc, tolerancia),
        s.CriadoEm,
        s.UltimaLeituraEm);
}

public sealed record SensorCriadoResponse(SensorResponse Sensor, string Segredo, string Aviso);

public sealed record LeituraResponse(
    Guid Id,
    Guid SensorId,
    Guid ViaId,
    string LeituraId,
    DateTime ColetadaEm,
    DateTime RecebidaEm,
    int VeiculosContados,
    int TamanhoFila,
    double? VelocidadeMediaKmh)
{
    public static LeituraResponse De(LeituraSensor l) => new(
        l.Id,
        l.SensorId,
        l.ViaId,
        l.LeituraExternaId,
        l.ColetadaEm,
        l.RecebidaEm,
        l.VeiculosContados,
        l.TamanhoFila,
        l.VelocidadeMediaKmh);
}

public sealed record RegistroLeituraResultado(LeituraResponse Leitura, bool Duplicada);

public sealed record LeituraRejeitada(string LeituraId, string Motivo);

public sealed record LoteLeiturasResponse(int Recebidas, int Registradas, int Duplicadas, IReadOnlyList<LeituraRejeitada> Rejeitadas);

public sealed record FaseResponse(
    int Ordem,
    Guid ViaId,
    string NomeVia,
    int VerdeSegundos,
    int AmareloSegundos,
    int VermelhoGeralSegundos,
    double FluxoVeiculosHora,
    double TaxaOcupacao);

public sealed record PlanoResponse(
    Guid Id,
    Guid CruzamentoId,
    ModoPlano Modo,
    int CicloSegundos,
    int TempoTravessiaPedestreSegundos,
    bool TravessiaAcessivel,
    string Justificativa,
    DateTime GeradoEm,
    DateTime VerificadoEm,
    DateTime? EncerradoEm,
    bool Vigente,
    IReadOnlyList<FaseResponse> Fases)
{
    public static PlanoResponse De(PlanoSemaforico p) => new(
        p.Id,
        p.CruzamentoId,
        p.Modo,
        p.CicloSegundos,
        p.TempoTravessiaPedestreSegundos,
        p.TravessiaAcessivel,
        p.Justificativa,
        p.GeradoEm,
        p.VerificadoEm,
        p.EncerradoEm,
        p.Vigente,
        p.Fases.OrderBy(f => f.Ordem)
            .Select(f => new FaseResponse(
                f.Ordem,
                f.ViaId,
                f.NomeVia,
                f.VerdeSegundos,
                f.AmareloSegundos,
                f.VermelhoGeralSegundos,
                f.FluxoVeiculosHora,
                f.TaxaOcupacao))
            .ToList());
}

public sealed record StatusViaResponse(
    Guid ViaId,
    string Nome,
    string Sentido,
    int VerdeSegundos,
    int EsperaMediaSegundos,
    int EsperaMaximaSegundos,
    NivelCongestionamento Nivel,
    int? FilaAtual);

public sealed record StatusTravessiaResponse(int TempoTravessiaSegundos, int EsperaMediaSegundos, bool AcessivelAtiva);

/// <summary>O que o cidadão vê no app: situação do cruzamento e quanto tempo vai esperar.</summary>
public sealed record StatusCruzamentoResponse(
    Guid CruzamentoId,
    string Codigo,
    string Nome,
    string Endereco,
    ModoPlano Modo,
    string Situacao,
    int CicloSegundos,
    DateTime? PlanoGeradoEm,
    bool PlanoProvisorio,
    StatusTravessiaResponse Travessia,
    IReadOnlyList<StatusViaResponse> Vias,
    IReadOnlyList<string> Avisos);

public sealed record IndicadoresResponse(
    Guid CruzamentoId,
    int JanelaMinutos,
    bool DadosSuficientes,
    string Interpretacao,
    ResultadoComparacao? Comparacao);

public sealed record OcorrenciaResponse(
    Guid Id,
    Guid CruzamentoId,
    string? NomeCruzamento,
    Guid AutorId,
    TipoOcorrencia Tipo,
    string Descricao,
    PrioridadeOcorrencia Prioridade,
    StatusOcorrencia Status,
    string? RespostaGestor,
    DateTime CriadaEm,
    DateTime AtualizadaEm)
{
    public static OcorrenciaResponse De(Ocorrencia o, string? nomeCruzamento) => new(
        o.Id,
        o.CruzamentoId,
        nomeCruzamento,
        o.AutorId,
        o.Tipo,
        o.Descricao,
        o.Prioridade,
        o.Status,
        o.RespostaGestor,
        o.CriadaEm,
        o.AtualizadaEm);
}

public sealed record SolicitacaoTravessiaResponse(
    Guid Id,
    Guid CruzamentoId,
    MotivoTravessiaAcessivel Motivo,
    DateTime SolicitadaEm,
    DateTime ExpiraEm,
    int TempoTravessiaSegundos,
    int TempoTravessiaPadraoSegundos,
    string Mensagem);

public sealed record EmergenciaResponse(
    Guid Id,
    Guid CruzamentoId,
    Guid ViaId,
    TipoVeiculoEmergencia TipoVeiculo,
    DateTime AcionadoEm,
    DateTime ExpiraEm,
    DateTime? EncerradoEm,
    bool Ativa,
    PlanoResponse? PlanoAplicado);

public sealed record PainelResumoResponse(
    int CruzamentosAtivos,
    int CruzamentosInativos,
    IReadOnlyDictionary<ModoPlano, int> PlanosVigentesPorModo,
    int SensoresAtivos,
    int SensoresOffline,
    IReadOnlyList<string> CodigosSensoresOffline,
    int LeiturasUltimaHora,
    IReadOnlyDictionary<PrioridadeOcorrencia, int> OcorrenciasPendentesPorPrioridade,
    int EmergenciasAtivas,
    DateTime GeradoEm);

public sealed record PremissasImpactoResponse(
    decimal ValorHoraCidadaoReais,
    int HorasReferenciaPorDia,
    int DiasPorMes,
    decimal AssinaturaMensalPorCruzamentoReais,
    decimal ImplantacaoPorCruzamentoReais,
    double EmissaoMarchaLentaGramasPorSegundo);

public sealed record ImpactoCruzamentoResponse(
    Guid CruzamentoId,
    string Codigo,
    string Nome,
    bool DadosSuficientes,
    double? ReducaoAtrasoPercentual,
    double? HorasEvitadasPorHora,
    double? Co2EvitadoKgPorHora);

/// <summary>Relatório que a prefeitura (cliente pagante) usa para justificar o contrato e a expansão.</summary>
public sealed record RelatorioImpactoResponse(
    DateTime GeradoEm,
    int JanelaMinutos,
    int CruzamentosContratados,
    int CruzamentosComDados,
    PremissasImpactoResponse Premissas,
    ResumoImpactoEconomico Resumo,
    IReadOnlyList<ImpactoCruzamentoResponse> Cruzamentos,
    string Interpretacao);
