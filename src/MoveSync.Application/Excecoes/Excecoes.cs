namespace MoveSync.Application.Excecoes;

/// <summary>Recurso inexistente → HTTP 404.</summary>
public sealed class RecursoNaoEncontradoException(string recurso, object id)
    : Exception($"{recurso} '{id}' não foi encontrado(a).")
{
    public string Recurso { get; } = recurso;
}

/// <summary>Credenciais inválidas → HTTP 401 (mensagem genérica de propósito, para não revelar se o e-mail existe).</summary>
public sealed class CredenciaisInvalidasException()
    : Exception("E-mail/código ou senha inválidos.");

/// <summary>
/// Violação de unicidade detectada pelo banco (ex.: duas requisições simultâneas com a mesma leitura).
/// A infraestrutura traduz a exceção do provedor (Oracle/SQLite) para esta.
/// </summary>
public sealed class ConflitoPersistenciaException(string mensagem, Exception? interna = null)
    : Exception(mensagem, interna);
