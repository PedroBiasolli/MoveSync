using MoveSync.Domain.Comum;
using MoveSync.Domain.Enums;

namespace MoveSync.Domain.Entidades;

/// <summary>
/// Corredor verde para ambulância, bombeiros etc. Enquanto vigente, a via indicada
/// recebe o verde máximo e as demais o verde mínimo de segurança.
/// </summary>
public sealed class AcionamentoEmergencia
{
    public const int DuracaoMinimaSegundos = 30;
    public const int DuracaoMaximaSegundos = 600;

    private AcionamentoEmergencia()
    {
    }

    public AcionamentoEmergencia(
        Guid cruzamentoId,
        Guid viaId,
        Guid solicitanteId,
        TipoVeiculoEmergencia tipoVeiculo,
        int duracaoSegundos,
        DateTime agoraUtc)
    {
        if (!Enum.IsDefined(tipoVeiculo))
        {
            throw new RegraNegocioException("TIPO_VEICULO_INVALIDO", "Tipo de veículo de emergência inválido.");
        }

        Guarda.Intervalo(duracaoSegundos, "DuracaoSegundos", DuracaoMinimaSegundos, DuracaoMaximaSegundos);
        Id = Guid.NewGuid();
        CruzamentoId = cruzamentoId;
        ViaId = viaId;
        SolicitanteId = solicitanteId;
        TipoVeiculo = tipoVeiculo;
        AcionadoEm = Guarda.Utc(agoraUtc);
        ExpiraEm = AcionadoEm.AddSeconds(duracaoSegundos);
    }

    public Guid Id { get; private set; }

    public Guid CruzamentoId { get; private set; }

    public Guid ViaId { get; private set; }

    public Guid SolicitanteId { get; private set; }

    public TipoVeiculoEmergencia TipoVeiculo { get; private set; }

    public DateTime AcionadoEm { get; private set; }

    public DateTime ExpiraEm { get; private set; }

    public DateTime? EncerradoEm { get; private set; }

    public bool EstaAtivo(DateTime agoraUtc) => EncerradoEm is null && agoraUtc < ExpiraEm;

    public void Encerrar(DateTime agoraUtc)
    {
        if (!EstaAtivo(agoraUtc))
        {
            throw new RegraNegocioException(
                "EMERGENCIA_JA_ENCERRADA",
                "Este acionamento de emergência já foi encerrado ou expirou.",
                TipoViolacao.Conflito);
        }

        EncerradoEm = Guarda.Utc(agoraUtc);
    }
}
