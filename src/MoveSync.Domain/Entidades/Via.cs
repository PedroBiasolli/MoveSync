using MoveSync.Domain.Comum;

namespace MoveSync.Domain.Entidades;

/// <summary>
/// Uma aproximação do cruzamento (ex.: "Av. dos Autonomistas sentido Centro").
/// Cada via recebe um tempo de verde próprio no plano semafórico.
/// </summary>
public sealed class Via
{
    private Via()
    {
    }

    internal Via(Guid cruzamentoId, string nome, string sentido, int numeroFaixas, int velocidadeMaximaKmh)
    {
        Id = Guid.NewGuid();
        CruzamentoId = cruzamentoId;
        Ativa = true;
        Aplicar(nome, sentido, numeroFaixas, velocidadeMaximaKmh);
    }

    public Guid Id { get; private set; }

    public Guid CruzamentoId { get; private set; }

    public string Nome { get; private set; } = string.Empty;

    public string Sentido { get; private set; } = string.Empty;

    public int NumeroFaixas { get; private set; }

    /// <summary>Velocidade regulamentada; define o tempo de amarelo (3, 4 ou 5 s).</summary>
    public int VelocidadeMaximaKmh { get; private set; }

    public bool Ativa { get; private set; }

    internal void Atualizar(string nome, string sentido, int numeroFaixas, int velocidadeMaximaKmh, bool ativa)
    {
        Aplicar(nome, sentido, numeroFaixas, velocidadeMaximaKmh);
        Ativa = ativa;
    }

    private void Aplicar(string nome, string sentido, int numeroFaixas, int velocidadeMaximaKmh)
    {
        Nome = Guarda.Texto(nome, nameof(Nome), 2, 100);
        Sentido = Guarda.Texto(sentido, nameof(Sentido), 2, 60);
        NumeroFaixas = Guarda.Intervalo(numeroFaixas, nameof(NumeroFaixas), 1, 6);
        VelocidadeMaximaKmh = Guarda.Intervalo(velocidadeMaximaKmh, nameof(VelocidadeMaximaKmh), 20, 80);
    }
}
