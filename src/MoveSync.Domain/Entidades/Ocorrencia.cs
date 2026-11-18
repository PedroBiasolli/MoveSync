using MoveSync.Domain.Comum;
using MoveSync.Domain.Enums;

namespace MoveSync.Domain.Entidades;

/// <summary>
/// Relato do cidadão sobre um problema no cruzamento (semáforo apagado, travessia insegura...).
/// A prioridade é definida pelo sistema (não pelo cidadão) e o status segue uma máquina de estados.
/// </summary>
public sealed class Ocorrencia
{
    private Ocorrencia()
    {
    }

    public Ocorrencia(Guid cruzamentoId, Guid autorId, TipoOcorrencia tipo, string descricao, DateTime agoraUtc)
    {
        Id = Guid.NewGuid();
        CruzamentoId = cruzamentoId;
        AutorId = autorId;
        Status = StatusOcorrencia.Aberta;
        CriadaEm = Guarda.Utc(agoraUtc);
        AtualizadaEm = CriadaEm;
        AplicarConteudo(tipo, descricao);
    }

    public Guid Id { get; private set; }

    public Guid CruzamentoId { get; private set; }

    public Guid AutorId { get; private set; }

    public TipoOcorrencia Tipo { get; private set; }

    public string Descricao { get; private set; } = string.Empty;

    public PrioridadeOcorrencia Prioridade { get; private set; }

    public StatusOcorrencia Status { get; private set; }

    public string? RespostaGestor { get; private set; }

    public DateTime CriadaEm { get; private set; }

    public DateTime AtualizadaEm { get; private set; }

    public bool EstaFinalizada => Status is StatusOcorrencia.Resolvida or StatusOcorrencia.Rejeitada;

    /// <summary>
    /// Regra de priorização: o que coloca a vida em risco vem primeiro.
    /// Semáforo apagado = crítico; piscando ou travessia insegura = alta.
    /// </summary>
    public static PrioridadeOcorrencia ClassificarPrioridade(TipoOcorrencia tipo) =>
        tipo switch
        {
            TipoOcorrencia.SemaforoApagado => PrioridadeOcorrencia.Critica,
            TipoOcorrencia.SemaforoPiscando => PrioridadeOcorrencia.Alta,
            TipoOcorrencia.TravessiaInsegura => PrioridadeOcorrencia.Alta,
            TipoOcorrencia.TempoTravessiaInsuficiente => PrioridadeOcorrencia.Media,
            TipoOcorrencia.SensorDanificado => PrioridadeOcorrencia.Media,
            TipoOcorrencia.Outro => PrioridadeOcorrencia.Baixa,
            _ => throw new RegraNegocioException("TIPO_OCORRENCIA_INVALIDO", "Tipo de ocorrência inválido.")
        };

    /// <summary>O cidadão só edita a própria ocorrência e apenas enquanto ela está "Aberta".</summary>
    public void AtualizarPeloAutor(Guid usuarioId, TipoOcorrencia tipo, string descricao, DateTime agoraUtc)
    {
        GarantirAutorEAberta(usuarioId, "editar");
        AplicarConteudo(tipo, descricao);
        AtualizadaEm = Guarda.Utc(agoraUtc);
    }

    /// <summary>Valida se o cidadão pode excluir; lança exceção se não puder.</summary>
    public void GarantirExclusaoPeloAutor(Guid usuarioId) => GarantirAutorEAberta(usuarioId, "excluir");

    /// <summary>
    /// Transições permitidas ao gestor:
    /// Aberta → EmAnalise | Resolvida | Rejeitada; EmAnalise → Resolvida | Rejeitada.
    /// Resolver ou rejeitar exige uma resposta ao cidadão.
    /// </summary>
    public void AlterarStatus(StatusOcorrencia novoStatus, string? resposta, DateTime agoraUtc)
    {
        if (!TransicaoPermitida(Status, novoStatus))
        {
            throw new RegraNegocioException(
                "TRANSICAO_STATUS_INVALIDA",
                $"Não é possível mudar a ocorrência de '{Status}' para '{novoStatus}'.",
                TipoViolacao.Conflito);
        }

        var respostaTratada = string.IsNullOrWhiteSpace(resposta) ? null : resposta.Trim();
        if ((novoStatus is StatusOcorrencia.Resolvida or StatusOcorrencia.Rejeitada) && respostaTratada is null)
        {
            throw new RegraNegocioException(
                "RESPOSTA_OBRIGATORIA",
                "Informe uma resposta ao cidadão para resolver ou rejeitar a ocorrência.");
        }

        if (respostaTratada is not null)
        {
            RespostaGestor = Guarda.Texto(respostaTratada, nameof(RespostaGestor), 5, 500);
        }

        Status = novoStatus;
        AtualizadaEm = Guarda.Utc(agoraUtc);
    }

    public static bool TransicaoPermitida(StatusOcorrencia atual, StatusOcorrencia novo) =>
        (atual, novo) switch
        {
            (StatusOcorrencia.Aberta, StatusOcorrencia.EmAnalise) => true,
            (StatusOcorrencia.Aberta, StatusOcorrencia.Resolvida) => true,
            (StatusOcorrencia.Aberta, StatusOcorrencia.Rejeitada) => true,
            (StatusOcorrencia.EmAnalise, StatusOcorrencia.Resolvida) => true,
            (StatusOcorrencia.EmAnalise, StatusOcorrencia.Rejeitada) => true,
            _ => false
        };

    private void AplicarConteudo(TipoOcorrencia tipo, string descricao)
    {
        Prioridade = ClassificarPrioridade(tipo);
        Tipo = tipo;
        Descricao = Guarda.Texto(descricao, nameof(Descricao), 10, 500);
    }

    private void GarantirAutorEAberta(Guid usuarioId, string acao)
    {
        if (usuarioId != AutorId)
        {
            throw new RegraNegocioException(
                "OCORRENCIA_DE_OUTRO_USUARIO",
                $"Você só pode {acao} ocorrências registradas por você.",
                TipoViolacao.AcessoNegado);
        }

        if (Status != StatusOcorrencia.Aberta)
        {
            throw new RegraNegocioException(
                "OCORRENCIA_NAO_EDITAVEL",
                $"Não é possível {acao} uma ocorrência com status '{Status}'.",
                TipoViolacao.Conflito);
        }
    }
}
