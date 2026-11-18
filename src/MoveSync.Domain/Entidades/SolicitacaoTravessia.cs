using MoveSync.Domain.Comum;
using MoveSync.Domain.Enums;

namespace MoveSync.Domain.Entidades;

/// <summary>
/// "Botoeira virtual" acessível: o cidadão (pessoa com deficiência, idosa, gestante...)
/// pede pelo app mais tempo para atravessar. Enquanto a solicitação estiver vigente,
/// a fase de pedestres é calculada com velocidade de caminhada reduzida.
/// </summary>
public sealed class SolicitacaoTravessia
{
    public static readonly TimeSpan Validade = TimeSpan.FromMinutes(5);

    private SolicitacaoTravessia()
    {
    }

    public SolicitacaoTravessia(Guid cruzamentoId, Guid usuarioId, MotivoTravessiaAcessivel motivo, DateTime agoraUtc)
    {
        if (!Enum.IsDefined(motivo))
        {
            throw new RegraNegocioException("MOTIVO_INVALIDO", "Motivo da travessia acessível inválido.");
        }

        Id = Guid.NewGuid();
        CruzamentoId = cruzamentoId;
        UsuarioId = usuarioId;
        Motivo = motivo;
        SolicitadaEm = Guarda.Utc(agoraUtc);
        ExpiraEm = SolicitadaEm + Validade;
    }

    public Guid Id { get; private set; }

    public Guid CruzamentoId { get; private set; }

    public Guid UsuarioId { get; private set; }

    public MotivoTravessiaAcessivel Motivo { get; private set; }

    public DateTime SolicitadaEm { get; private set; }

    public DateTime ExpiraEm { get; private set; }

    public bool EstaVigente(DateTime agoraUtc) => agoraUtc < ExpiraEm;
}
