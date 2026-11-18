using MoveSync.Domain.Comum;
using MoveSync.Domain.Enums;

namespace MoveSync.Domain.Entidades;

/// <summary>
/// Dispositivo IoT (ESP32 com laço indutivo ou câmera com visão computacional)
/// instalado em uma via. Autentica com código + segredo e recebe um JWT de perfil "Sensor".
/// </summary>
public sealed class Sensor
{
    private Sensor()
    {
    }

    public Sensor(string codigo, Guid cruzamentoId, Guid viaId, TipoSensor tipo, string segredoHash, DateTime agoraUtc)
    {
        Id = Guid.NewGuid();
        Codigo = Cruzamento.NormalizarCodigo(codigo);
        CruzamentoId = cruzamentoId;
        ViaId = viaId;
        Tipo = Enum.IsDefined(tipo)
            ? tipo
            : throw new RegraNegocioException("TIPO_SENSOR_INVALIDO", "Tipo de sensor inválido.");
        SegredoHash = segredoHash;
        Ativo = true;
        CriadoEm = Guarda.Utc(agoraUtc);
    }

    public Guid Id { get; private set; }

    public string Codigo { get; private set; } = string.Empty;

    public Guid CruzamentoId { get; private set; }

    public Guid ViaId { get; private set; }

    public TipoSensor Tipo { get; private set; }

    public string SegredoHash { get; private set; } = string.Empty;

    public bool Ativo { get; private set; }

    public DateTime CriadoEm { get; private set; }

    public DateTime? UltimaLeituraEm { get; private set; }

    /// <summary>Sensor ativo sem leitura dentro da janela de tolerância é considerado fora do ar.</summary>
    public bool EstaOffline(DateTime agoraUtc, TimeSpan tolerancia) =>
        Ativo && (UltimaLeituraEm is null || agoraUtc - UltimaLeituraEm.Value > tolerancia);

    public void RegistrarComunicacao(DateTime agoraUtc) => UltimaLeituraEm = Guarda.Utc(agoraUtc);

    public void DefinirAtivo(bool ativo) => Ativo = ativo;
}
