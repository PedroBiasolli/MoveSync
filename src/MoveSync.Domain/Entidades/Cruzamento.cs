using MoveSync.Domain.Comum;
using MoveSync.Domain.Enums;

namespace MoveSync.Domain.Entidades;

/// <summary>
/// Cruzamento semaforizado. É a raiz de agregado das vias: toda alteração em vias
/// passa por aqui, garantindo as regras (nome único, limite de vias etc.).
/// </summary>
public sealed class Cruzamento
{
    public const int MaximoVias = 6;

    private readonly List<Via> _vias = [];

    private Cruzamento()
    {
    }

    public Cruzamento(
        string codigo,
        string nome,
        string endereco,
        double latitude,
        double longitude,
        double larguraTravessiaMetros,
        DateTime agoraUtc)
    {
        Id = Guid.NewGuid();
        Codigo = NormalizarCodigo(codigo);
        Modo = ModoOperacao.Adaptativo;
        Ativo = true;
        CriadoEm = Guarda.Utc(agoraUtc);
        AplicarDados(nome, endereco, latitude, longitude, larguraTravessiaMetros);
        AtualizadoEm = CriadoEm;
    }

    public Guid Id { get; private set; }

    /// <summary>Código curto e único usado em campo (ex.: OSA-001).</summary>
    public string Codigo { get; private set; } = string.Empty;

    public string Nome { get; private set; } = string.Empty;

    public string Endereco { get; private set; } = string.Empty;

    public double Latitude { get; private set; }

    public double Longitude { get; private set; }

    /// <summary>Largura da faixa de pedestres, usada para calcular o tempo de travessia.</summary>
    public double LarguraTravessiaMetros { get; private set; }

    public ModoOperacao Modo { get; private set; }

    public bool Ativo { get; private set; }

    public DateTime CriadoEm { get; private set; }

    public DateTime AtualizadoEm { get; private set; }

    public IReadOnlyCollection<Via> Vias => _vias.AsReadOnly();

    public IEnumerable<Via> ViasAtivas => _vias.Where(v => v.Ativa);

    public static string NormalizarCodigo(string? codigo)
    {
        var valor = Guarda.Texto(codigo, nameof(Codigo), 3, 20).ToUpperInvariant();
        if (!valor.All(c => char.IsLetterOrDigit(c) || c == '-'))
        {
            throw new RegraNegocioException("CODIGO_INVALIDO", "O código deve conter apenas letras, números e hífen.");
        }

        return valor;
    }

    public void Atualizar(
        string nome,
        string endereco,
        double latitude,
        double longitude,
        double larguraTravessiaMetros,
        bool ativo,
        DateTime agoraUtc)
    {
        AplicarDados(nome, endereco, latitude, longitude, larguraTravessiaMetros);
        Ativo = ativo;
        AtualizadoEm = Guarda.Utc(agoraUtc);
    }

    public void DefinirModo(ModoOperacao modo, DateTime agoraUtc)
    {
        if (!Enum.IsDefined(modo))
        {
            throw new RegraNegocioException("MODO_INVALIDO", "Modo de operação inválido.");
        }

        Modo = modo;
        AtualizadoEm = Guarda.Utc(agoraUtc);
    }

    public Via AdicionarVia(string nome, string sentido, int numeroFaixas, int velocidadeMaximaKmh, DateTime agoraUtc)
    {
        if (_vias.Count >= MaximoVias)
        {
            throw new RegraNegocioException(
                "LIMITE_VIAS",
                $"Um cruzamento pode ter no máximo {MaximoVias} vias.",
                TipoViolacao.Conflito);
        }

        GarantirNomeUnico(nome, ignorarViaId: null);
        var via = new Via(Id, nome, sentido, numeroFaixas, velocidadeMaximaKmh);
        _vias.Add(via);
        AtualizadoEm = Guarda.Utc(agoraUtc);
        return via;
    }

    public Via AtualizarVia(
        Guid viaId,
        string nome,
        string sentido,
        int numeroFaixas,
        int velocidadeMaximaKmh,
        bool ativa,
        DateTime agoraUtc)
    {
        var via = ObterVia(viaId);
        GarantirNomeUnico(nome, viaId);
        via.Atualizar(nome, sentido, numeroFaixas, velocidadeMaximaKmh, ativa);
        AtualizadoEm = Guarda.Utc(agoraUtc);
        return via;
    }

    public Via RemoverVia(Guid viaId, DateTime agoraUtc)
    {
        var via = ObterVia(viaId);
        _vias.Remove(via);
        AtualizadoEm = Guarda.Utc(agoraUtc);
        return via;
    }

    public Via ObterVia(Guid viaId) =>
        _vias.FirstOrDefault(v => v.Id == viaId)
        ?? throw new RegraNegocioException(
            "VIA_NAO_PERTENCE_AO_CRUZAMENTO",
            "A via informada não pertence a este cruzamento.");

    private void AplicarDados(string nome, string endereco, double latitude, double longitude, double larguraTravessia)
    {
        Nome = Guarda.Texto(nome, nameof(Nome), 3, 120);
        Endereco = Guarda.Texto(endereco, nameof(Endereco), 5, 200);
        Latitude = Guarda.Intervalo(latitude, nameof(Latitude), -90, 90);
        Longitude = Guarda.Intervalo(longitude, nameof(Longitude), -180, 180);
        LarguraTravessiaMetros = Guarda.Intervalo(larguraTravessia, nameof(LarguraTravessiaMetros), 3, 40);
    }

    private void GarantirNomeUnico(string nome, Guid? ignorarViaId)
    {
        var nomeNormalizado = (nome ?? string.Empty).Trim();
        var duplicada = _vias.Any(v =>
            v.Id != ignorarViaId &&
            string.Equals(v.Nome, nomeNormalizado, StringComparison.OrdinalIgnoreCase));
        if (duplicada)
        {
            throw new RegraNegocioException(
                "VIA_DUPLICADA",
                $"Já existe uma via chamada '{nomeNormalizado}' neste cruzamento.",
                TipoViolacao.Conflito);
        }
    }
}
