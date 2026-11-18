using Microsoft.AspNetCore.Identity;
using MoveSync.Application.Abstracoes;

namespace MoveSync.Infrastructure.Seguranca;

/// <summary>
/// Usa o PasswordHasher do ASP.NET Core Identity: PBKDF2 com HMAC-SHA512, salt aleatório
/// e 100 mil iterações. O hash já carrega a versão do algoritmo, então pode evoluir sem quebrar senhas antigas.
/// </summary>
public sealed class HashSenhaPbkdf2 : IHashSenha
{
    private static readonly PasswordHasher<object> Hasher = new();
    private static readonly object Contexto = new();

    public string Gerar(string senha)
    {
        ArgumentException.ThrowIfNullOrEmpty(senha);
        return Hasher.HashPassword(Contexto, senha);
    }

    public bool Verificar(string hash, string senha)
    {
        if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(senha))
        {
            return false;
        }

        try
        {
            return Hasher.VerifyHashedPassword(Contexto, hash, senha) != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
