namespace MoveSync.Domain.Comum;

/// <summary>
/// Classifica a violação de regra para que a API escolha o código HTTP correto
/// (422 para validação de negócio, 409 para conflito de estado, 403 para acesso negado).
/// </summary>
public enum TipoViolacao
{
    Validacao = 1,
    Conflito = 2,
    AcessoNegado = 3
}

/// <summary>
/// Exceção lançada pelo domínio quando uma regra de negócio é violada.
/// O código (ex.: "OCORRENCIA_NAO_EDITAVEL") é estável e pode ser usado pelo app mobile.
/// </summary>
public sealed class RegraNegocioException : Exception
{
    public RegraNegocioException(string codigo, string mensagem, TipoViolacao tipo = TipoViolacao.Validacao)
        : base(mensagem)
    {
        Codigo = codigo;
        Tipo = tipo;
    }

    public string Codigo { get; }

    public TipoViolacao Tipo { get; }
}
