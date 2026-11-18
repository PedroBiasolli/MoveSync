using MoveSync.Domain.Comum;

namespace MoveSync.Domain.Entidades;

/// <summary>
/// Telemetria enviada por um sensor: veículos contados no intervalo e tamanho da fila.
/// A combinação (SensorId, LeituraExternaId) é única: é a chave natural de idempotência,
/// então reenviar a mesma leitura (ex.: ESP32 reconectando) nunca duplica dados.
/// </summary>
public sealed class LeituraSensor
{
    public static readonly TimeSpan ToleranciaFuturo = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan IdadeMaxima = TimeSpan.FromHours(24);

    private LeituraSensor()
    {
    }

    public LeituraSensor(
        Sensor sensor,
        string leituraExternaId,
        DateTime coletadaEmUtc,
        int veiculosContados,
        int tamanhoFila,
        double? velocidadeMediaKmh,
        DateTime agoraUtc)
    {
        ArgumentNullException.ThrowIfNull(sensor);
        var coletadaEm = Guarda.Utc(coletadaEmUtc);
        var agora = Guarda.Utc(agoraUtc);

        if (coletadaEm > agora + ToleranciaFuturo)
        {
            throw new RegraNegocioException(
                "LEITURA_NO_FUTURO",
                "A data da leitura está no futuro. Verifique o relógio (NTP) do sensor.");
        }

        if (agora - coletadaEm > IdadeMaxima)
        {
            throw new RegraNegocioException(
                "LEITURA_EXPIRADA",
                "Leituras com mais de 24 horas não são aceitas.");
        }

        Id = Guid.NewGuid();
        SensorId = sensor.Id;
        ViaId = sensor.ViaId;
        CruzamentoId = sensor.CruzamentoId;
        LeituraExternaId = Guarda.Texto(leituraExternaId, nameof(LeituraExternaId), 1, 64);
        ColetadaEm = coletadaEm;
        RecebidaEm = agora;
        VeiculosContados = Guarda.Intervalo(veiculosContados, nameof(VeiculosContados), 0, 500);
        TamanhoFila = Guarda.Intervalo(tamanhoFila, nameof(TamanhoFila), 0, 200);
        if (velocidadeMediaKmh is { } velocidade)
        {
            VelocidadeMediaKmh = Guarda.Intervalo(velocidade, nameof(VelocidadeMediaKmh), 0, 200);
        }
    }

    public Guid Id { get; private set; }

    public Guid SensorId { get; private set; }

    public Guid ViaId { get; private set; }

    public Guid CruzamentoId { get; private set; }

    public string LeituraExternaId { get; private set; } = string.Empty;

    public DateTime ColetadaEm { get; private set; }

    public DateTime RecebidaEm { get; private set; }

    public int VeiculosContados { get; private set; }

    public int TamanhoFila { get; private set; }

    public double? VelocidadeMediaKmh { get; private set; }
}
