using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoveSync.Application.Abstracoes;
using MoveSync.Domain.Entidades;
using MoveSync.Domain.Enums;

namespace MoveSync.Infrastructure.Persistencia;

/// <summary>
/// Executado na subida da API: cria as tabelas (se configurado) e carrega dados de demonstração.
/// Usamos CreateTables (e não EnsureCreated) porque no Oracle da FIAP o usuário já tem tabelas
/// de outras disciplinas, e o EnsureCreated simplesmente não faria nada nesse caso.
/// </summary>
public static class InicializadorBanco
{
    public static async Task InicializarAsync(IServiceProvider servicos, CancellationToken ct = default)
    {
        await using var escopo = servicos.CreateAsyncScope();
        var provedor = escopo.ServiceProvider;
        var db = provedor.GetRequiredService<MoveSyncDbContext>();
        var opcoesBanco = provedor.GetRequiredService<IOptions<OpcoesBancoDados>>().Value;
        var opcoesSeed = provedor.GetRequiredService<IOptions<OpcoesSeed>>().Value;
        var logger = provedor.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(InicializadorBanco));

        if (opcoesBanco.CriarTabelasAutomaticamente)
        {
            await GarantirTabelasAsync(db, opcoesBanco, logger, ct);
        }

        if (opcoesSeed.Habilitado)
        {
            await PopularDemonstracaoAsync(
                db,
                opcoesSeed,
                provedor.GetRequiredService<IHashSenha>(),
                provedor.GetRequiredService<TimeProvider>(),
                logger,
                ct);
        }
    }

    private static async Task GarantirTabelasAsync(MoveSyncDbContext db, OpcoesBancoDados opcoes, ILogger logger, CancellationToken ct)
    {
        var criador = db.GetService<IRelationalDatabaseCreator>();
        if (!opcoes.EhOracle && !await criador.ExistsAsync(ct))
        {
            await criador.CreateAsync(ct);
        }

        if (await TabelasExistemAsync(db, ct))
        {
            logger.LogInformation("Tabelas MS_* já existem. Nenhuma alteração de schema necessária.");
            return;
        }

        logger.LogInformation("Criando tabelas MS_* no banco ({Provedor})...", opcoes.Provedor);
        await criador.CreateTablesAsync(ct);
    }

    private static async Task<bool> TabelasExistemAsync(MoveSyncDbContext db, CancellationToken ct)
    {
        try
        {
            _ = await db.Usuarios.AnyAsync(ct);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static async Task PopularDemonstracaoAsync(
        MoveSyncDbContext db,
        OpcoesSeed opcoes,
        IHashSenha hash,
        TimeProvider relogio,
        ILogger logger,
        CancellationToken ct)
    {
        var agora = relogio.GetUtcNow().UtcDateTime;

        await CriarUsuarioSeNecessarioAsync(db, hash, opcoes.GestorNome, opcoes.GestorEmail, opcoes.GestorSenha, PerfilUsuario.Gestor, agora, logger, ct);
        await CriarUsuarioSeNecessarioAsync(db, hash, opcoes.CidadaoNome, opcoes.CidadaoEmail, opcoes.CidadaoSenha, PerfilUsuario.Cidadao, agora, logger, ct);

        if (await db.Cruzamentos.AnyAsync(ct))
        {
            await db.SaveChangesAsync(ct);
            return;
        }

        // Coordenadas aproximadas, apenas para demonstração.
        var paulista = new Cruzamento("SPO-001", "Av. Paulista x R. da Consolação", "Av. Paulista, 2000 - Bela Vista, São Paulo/SP", -23.5567, -46.6620, 16, agora);
        paulista.AdicionarVia("Av. Paulista sentido Paraíso", "Consolação → Paraíso", 4, 50, agora);
        paulista.AdicionarVia("Av. Paulista sentido Consolação", "Paraíso → Consolação", 4, 50, agora);
        paulista.AdicionarVia("R. da Consolação sentido Centro", "Jardins → Centro", 3, 50, agora);
        paulista.AdicionarVia("R. da Consolação sentido Jardins", "Centro → Jardins", 3, 50, agora);

        var fariaLima = new Cruzamento("SPO-002", "Av. Faria Lima x Av. Rebouças", "Av. Brig. Faria Lima - Pinheiros, São Paulo/SP", -23.5730, -46.6895, 14, agora);
        fariaLima.AdicionarVia("Av. Faria Lima sentido Pinheiros", "Itaim → Pinheiros", 3, 50, agora);
        fariaLima.AdicionarVia("Av. Rebouças sentido Centro", "Pinheiros → Centro", 3, 50, agora);
        fariaLima.AdicionarVia("Av. Rebouças sentido Pinheiros", "Centro → Pinheiros", 3, 50, agora);

        db.Cruzamentos.AddRange(paulista, fariaLima);

        if (!string.IsNullOrWhiteSpace(opcoes.SegredoSensores) && opcoes.SegredoSensores.Length >= 16)
        {
            var segredoHash = hash.Gerar(opcoes.SegredoSensores);
            AdicionarSensores(db, paulista, "SPO001", segredoHash, agora);
            AdicionarSensores(db, fariaLima, "SPO002", segredoHash, agora);
        }
        else
        {
            logger.LogWarning("Seed:SegredoSensores não configurado (mín. 16 caracteres): sensores de demonstração não foram criados.");
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Dados de demonstração criados: 2 cruzamentos, {Vias} vias.", paulista.Vias.Count + fariaLima.Vias.Count);
    }

    private static void AdicionarSensores(MoveSyncDbContext db, Cruzamento cruzamento, string prefixo, string segredoHash, DateTime agora)
    {
        var numero = 1;
        foreach (var via in cruzamento.Vias.OrderBy(v => v.Nome, StringComparer.Ordinal))
        {
            var tipo = numero % 2 == 0 ? TipoSensor.Camera : TipoSensor.LacoIndutivo;
            db.Sensores.Add(new Sensor($"{prefixo}-V{numero}", cruzamento.Id, via.Id, tipo, segredoHash, agora));
            numero++;
        }
    }

    private static async Task CriarUsuarioSeNecessarioAsync(
        MoveSyncDbContext db,
        IHashSenha hash,
        string nome,
        string email,
        string? senha,
        PerfilUsuario perfil,
        DateTime agora,
        ILogger logger,
        CancellationToken ct)
    {
        var emailNormalizado = Usuario.NormalizarEmail(email);
        if (await db.Usuarios.AnyAsync(u => u.Email == emailNormalizado, ct))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(senha))
        {
            logger.LogWarning("Senha do usuário de demonstração {Email} não configurada: usuário não criado.", emailNormalizado);
            return;
        }

        db.Usuarios.Add(new Usuario(nome, emailNormalizado, hash.Gerar(senha), perfil, agora));
    }
}
