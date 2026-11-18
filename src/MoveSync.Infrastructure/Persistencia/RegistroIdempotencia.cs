namespace MoveSync.Infrastructure.Persistencia;

/// <summary>
/// Guarda a resposta de uma requisição feita com o cabeçalho Idempotency-Key.
/// Se o cliente repetir a requisição (mesma chave + mesmo corpo), a API devolve a resposta
/// gravada em vez de executar a operação de novo.
/// </summary>
public sealed class RegistroIdempotencia
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Quem fez + método + rota. Impede que a chave de um usuário colida com a de outro.</summary>
    public string Escopo { get; set; } = string.Empty;

    public string Chave { get; set; } = string.Empty;

    /// <summary>SHA-256 do método, caminho e corpo — detecta reuso da chave com conteúdo diferente.</summary>
    public string HashRequisicao { get; set; } = string.Empty;

    /// <summary>False enquanto a primeira requisição ainda está sendo processada.</summary>
    public bool Concluido { get; set; }

    public int StatusCode { get; set; }

    public string? ContentType { get; set; }

    public string? Location { get; set; }

    public string? CorpoResposta { get; set; }

    public DateTime CriadoEm { get; set; }

    public DateTime ExpiraEm { get; set; }
}
