using MoveSync.Domain.Comum;
using MoveSync.Domain.Enums;
using MoveSync.Domain.Servicos;

namespace MoveSync.Domain.Entidades;

/// <summary>
/// Plano semafórico persistido. Só um plano fica "vigente" por cruzamento; os anteriores
/// formam o histórico (auditoria de por que o semáforo mudou).
/// Um novo plano só é gravado quando o cálculo muda de forma relevante, evitando lixo no banco.
/// </summary>
public sealed class PlanoSemaforico
{
    private readonly List<FasePlano> _fases = [];

    private PlanoSemaforico()
    {
    }

    public PlanoSemaforico(Guid cruzamentoId, PlanoCalculado calculo, DateTime agoraUtc)
    {
        ArgumentNullException.ThrowIfNull(calculo);
        Id = Guid.NewGuid();
        CruzamentoId = cruzamentoId;
        Modo = calculo.Modo;
        CicloSegundos = calculo.CicloSegundos;
        TempoTravessiaPedestreSegundos = calculo.TempoTravessiaPedestreSegundos;
        TravessiaAcessivel = calculo.TravessiaAcessivel;
        Justificativa = calculo.Justificativa.Length > 2000 ? calculo.Justificativa[..2000] : calculo.Justificativa;
        GeradoEm = Guarda.Utc(agoraUtc);
        VerificadoEm = GeradoEm;
        Vigente = true;

        var ordem = 1;
        foreach (var fase in calculo.Fases)
        {
            _fases.Add(new FasePlano(Id, ordem++, fase));
        }
    }

    public Guid Id { get; private set; }

    public Guid CruzamentoId { get; private set; }

    public ModoPlano Modo { get; private set; }

    public int CicloSegundos { get; private set; }

    public int TempoTravessiaPedestreSegundos { get; private set; }

    public bool TravessiaAcessivel { get; private set; }

    public string Justificativa { get; private set; } = string.Empty;

    public DateTime GeradoEm { get; private set; }

    /// <summary>Última vez em que o motor confirmou que este plano continua adequado.</summary>
    public DateTime VerificadoEm { get; private set; }

    public DateTime? EncerradoEm { get; private set; }

    public bool Vigente { get; private set; }

    public IReadOnlyCollection<FasePlano> Fases => _fases.AsReadOnly();

    public bool EquivalenteA(PlanoCalculado calculo, int toleranciaSegundos)
    {
        ArgumentNullException.ThrowIfNull(calculo);
        if (calculo.Modo != Modo
            || calculo.TravessiaAcessivel != TravessiaAcessivel
            || calculo.TempoTravessiaPedestreSegundos != TempoTravessiaPedestreSegundos
            || calculo.Fases.Count != _fases.Count)
        {
            return false;
        }

        foreach (var nova in calculo.Fases)
        {
            var atual = _fases.FirstOrDefault(f => f.ViaId == nova.ViaId);
            if (atual is null || Math.Abs(atual.VerdeSegundos - nova.VerdeSegundos) > toleranciaSegundos)
            {
                return false;
            }
        }

        return true;
    }

    public void MarcarVerificado(DateTime agoraUtc) => VerificadoEm = Guarda.Utc(agoraUtc);

    public void Encerrar(DateTime agoraUtc)
    {
        Vigente = false;
        EncerradoEm = Guarda.Utc(agoraUtc);
    }
}

/// <summary>Tempos de uma via dentro de um plano.</summary>
public sealed class FasePlano
{
    private FasePlano()
    {
    }

    internal FasePlano(Guid planoId, int ordem, FaseCalculada fase)
    {
        Id = Guid.NewGuid();
        PlanoId = planoId;
        Ordem = ordem;
        ViaId = fase.ViaId;
        NomeVia = fase.NomeVia;
        VerdeSegundos = fase.VerdeSegundos;
        AmareloSegundos = fase.AmareloSegundos;
        VermelhoGeralSegundos = fase.VermelhoGeralSegundos;
        FluxoVeiculosHora = fase.FluxoVeiculosHora;
        TaxaOcupacao = fase.TaxaOcupacao;
    }

    public Guid Id { get; private set; }

    public Guid PlanoId { get; private set; }

    public int Ordem { get; private set; }

    public Guid ViaId { get; private set; }

    public string NomeVia { get; private set; } = string.Empty;

    public int VerdeSegundos { get; private set; }

    public int AmareloSegundos { get; private set; }

    public int VermelhoGeralSegundos { get; private set; }

    public double FluxoVeiculosHora { get; private set; }

    public double TaxaOcupacao { get; private set; }
}
