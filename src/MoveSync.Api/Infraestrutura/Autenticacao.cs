using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MoveSync.Application.Abstracoes;
using MoveSync.Domain.Entidades;

namespace MoveSync.Api.Infraestrutura;

/// <summary>Os três perfis do sistema. Cidadão e Gestor são pessoas; Sensor é um dispositivo IoT.</summary>
public static class Perfis
{
    public const string Cidadao = "Cidadao";
    public const string Gestor = "Gestor";
    public const string Sensor = "Sensor";
}

/// <summary>Políticas de autorização usadas nos endpoints (RequireAuthorization).</summary>
public static class Politicas
{
    public const string SomenteCidadao = "SomenteCidadao";
    public const string SomenteGestor = "SomenteGestor";
    public const string SomenteSensor = "SomenteSensor";

    /// <summary>Qualquer pessoa logada (cidadão ou gestor). Sensores não entram.</summary>
    public const string Pessoa = "Pessoa";
}

/// <summary>Seção "Jwt" do appsettings. A chave NUNCA fica no código: vem de variável de ambiente (Jwt__Chave) ou user-secrets.</summary>
public sealed class OpcoesJwt
{
    public const string Secao = "Jwt";

    public string Emissor { get; set; } = "MoveSync.Api";

    public string Audiencia { get; set; } = "MoveSync.Clientes";

    /// <summary>Chave HMAC-SHA256 com no mínimo 32 caracteres.</summary>
    public string Chave { get; set; } = string.Empty;

    public int ExpiracaoMinutosUsuario { get; set; } = 120;

    public int ExpiracaoHorasSensor { get; set; } = 24;
}

/// <summary>Claims gravadas no token (nomes curtos padrão JWT).</summary>
public static class NomesClaims
{
    public const string Sub = "sub";
    public const string Nome = "name";
    public const string Email = "email";
    public const string Perfil = "role";
    public const string CruzamentoId = "cruzamento_id";
    public const string ViaId = "via_id";
}

/// <summary>Gera o JWT assinado com HMAC-SHA256.</summary>
public sealed class GeradorTokenJwt(OpcoesJwt opcoes, TimeProvider relogio) : IGeradorToken
{
    private readonly JsonWebTokenHandler _handler = new();
    private readonly SigningCredentials _credenciais = new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opcoes.Chave)),
        SecurityAlgorithms.HmacSha256);

    public TokenGerado GerarParaUsuario(Usuario usuario) =>
        Gerar(
            [
                new Claim(NomesClaims.Sub, usuario.Id.ToString()),
                new Claim(NomesClaims.Nome, usuario.Nome),
                new Claim(NomesClaims.Email, usuario.Email),
                new Claim(NomesClaims.Perfil, usuario.Perfil.ToString())
            ],
            TimeSpan.FromMinutes(opcoes.ExpiracaoMinutosUsuario));

    public TokenGerado GerarParaSensor(Sensor sensor) =>
        Gerar(
            [
                new Claim(NomesClaims.Sub, sensor.Id.ToString()),
                new Claim(NomesClaims.Nome, sensor.Codigo),
                new Claim(NomesClaims.Perfil, Perfis.Sensor),
                new Claim(NomesClaims.CruzamentoId, sensor.CruzamentoId.ToString()),
                new Claim(NomesClaims.ViaId, sensor.ViaId.ToString())
            ],
            TimeSpan.FromHours(opcoes.ExpiracaoHorasSensor));

    private TokenGerado Gerar(List<Claim> claims, TimeSpan validade)
    {
        var agora = relogio.GetUtcNow().UtcDateTime;
        var expiraEm = agora + validade;
        claims.Add(new Claim("jti", Guid.NewGuid().ToString()));

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = opcoes.Emissor,
            Audience = opcoes.Audiencia,
            IssuedAt = agora,
            NotBefore = agora,
            Expires = expiraEm,
            SigningCredentials = _credenciais
        });

        return new TokenGerado(token, expiraEm);
    }
}

public static class ConfiguracaoAutenticacao
{
    public static IServiceCollection AdicionarAutenticacaoJwt(this IServiceCollection services)
    {
        // Opções lidas na primeira resolução (depois do Build), e não na montagem: a chave vinda de variável
        // de ambiente ou dos testes de integração sempre vale. Singleton: a chave de desenvolvimento é gerada uma vez.
        services.AddSingleton(sp => CriarOpcoes(
            sp.GetRequiredService<IConfiguration>(),
            sp.GetRequiredService<IHostEnvironment>()));
        services.AddSingleton<IGeradorToken, GeradorTokenJwt>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<OpcoesJwt>((jwt, opcoes) =>
            {
                // Mantém os nomes curtos das claims ("sub", "role") em vez de mapear para URIs longas.
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = opcoes.Emissor,
                    ValidateAudience = true,
                    ValidAudience = opcoes.Audiencia,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opcoes.Chave)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = NomesClaims.Nome,
                    RoleClaimType = NomesClaims.Perfil
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Politicas.SomenteCidadao, p => p.RequireRole(Perfis.Cidadao))
            .AddPolicy(Politicas.SomenteGestor, p => p.RequireRole(Perfis.Gestor))
            .AddPolicy(Politicas.SomenteSensor, p => p.RequireRole(Perfis.Sensor))
            .AddPolicy(Politicas.Pessoa, p => p.RequireRole(Perfis.Cidadao, Perfis.Gestor));

        return services;
    }

    /// <summary>Lê e valida a seção Jwt. Sem chave: erro fora de desenvolvimento; em desenvolvimento, chave aleatória em memória.</summary>
    public static OpcoesJwt CriarOpcoes(IConfiguration configuration, IHostEnvironment ambiente)
    {
        var opcoes = configuration.GetSection(OpcoesJwt.Secao).Get<OpcoesJwt>() ?? new OpcoesJwt();
        if (string.IsNullOrWhiteSpace(opcoes.Chave))
        {
            if (!ambiente.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "Jwt:Chave não configurada. Defina a variável de ambiente Jwt__Chave com no mínimo 32 caracteres.");
            }

            // Apenas em desenvolvimento: chave aleatória em memória (tokens deixam de valer ao reiniciar a API).
            opcoes.Chave = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        }

        if (Encoding.UTF8.GetByteCount(opcoes.Chave) < 32)
        {
            throw new InvalidOperationException("Jwt:Chave deve ter no mínimo 32 caracteres (256 bits).");
        }

        return opcoes;
    }

    /// <summary>Id do usuário (ou do sensor) autenticado, lido da claim "sub".</summary>
    public static Guid ObterId(this ClaimsPrincipal usuario) =>
        Guid.TryParse(usuario.FindFirstValue(NomesClaims.Sub), out var id)
            ? id
            : throw new InvalidOperationException("Token sem identificador (sub).");

    public static bool EhGestor(this ClaimsPrincipal usuario) => usuario.IsInRole(Perfis.Gestor);
}
