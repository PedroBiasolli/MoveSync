using MoveSync.Domain.Comum;
using MoveSync.Domain.Enums;

namespace MoveSync.Domain.Entidades;

/// <summary>Pessoa que acessa o sistema: cidadão (app) ou gestor de trânsito (painel).</summary>
public sealed class Usuario
{
    private Usuario()
    {
    }

    public Usuario(string nome, string email, string senhaHash, PerfilUsuario perfil, DateTime agoraUtc)
    {
        Id = Guid.NewGuid();
        Nome = Guarda.Texto(nome, nameof(Nome), 3, 120);
        Email = NormalizarEmail(email);
        if (Email.Length is < 5 or > 160 || !Email.Contains('@'))
        {
            throw new RegraNegocioException("EMAIL_INVALIDO", "Informe um e-mail válido.");
        }

        SenhaHash = string.IsNullOrWhiteSpace(senhaHash)
            ? throw new RegraNegocioException("SENHA_INVALIDA", "O hash da senha é obrigatório.")
            : senhaHash;
        Perfil = perfil;
        Ativo = true;
        CriadoEm = Guarda.Utc(agoraUtc);
    }

    public Guid Id { get; private set; }

    public string Nome { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string SenhaHash { get; private set; } = string.Empty;

    public PerfilUsuario Perfil { get; private set; }

    public bool Ativo { get; private set; }

    public DateTime CriadoEm { get; private set; }

    public static string NormalizarEmail(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();
}
