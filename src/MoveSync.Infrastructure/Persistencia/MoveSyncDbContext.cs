using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MoveSync.Application.Abstracoes;
using MoveSync.Application.Excecoes;
using MoveSync.Domain.Entidades;
using MoveSync.Domain.Enums;

namespace MoveSync.Infrastructure.Persistencia;

/// <summary>
/// DbContext do EF Core. Também é a Unit of Work: SaveChanges grava tudo em uma única transação.
/// Funciona com Oracle (produção/nuvem) e SQLite (desenvolvimento e testes de integração).
/// </summary>
public sealed class MoveSyncDbContext(DbContextOptions<MoveSyncDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    public DbSet<Cruzamento> Cruzamentos => Set<Cruzamento>();

    public DbSet<Via> Vias => Set<Via>();

    public DbSet<Sensor> Sensores => Set<Sensor>();

    public DbSet<LeituraSensor> Leituras => Set<LeituraSensor>();

    public DbSet<PlanoSemaforico> Planos => Set<PlanoSemaforico>();

    public DbSet<FasePlano> FasesPlano => Set<FasePlano>();

    public DbSet<Ocorrencia> Ocorrencias => Set<Ocorrencia>();

    public DbSet<SolicitacaoTravessia> SolicitacoesTravessia => Set<SolicitacaoTravessia>();

    public DbSet<AcionamentoEmergencia> Emergencias => Set<AcionamentoEmergencia>();

    public DbSet<RegistroIdempotencia> RegistrosIdempotencia => Set<RegistroIdempotencia>();

    public async Task<int> SalvarAsync(CancellationToken ct)
    {
        try
        {
            return await SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (EhViolacaoDeUnicidade(ex))
        {
            throw new ConflitoPersistenciaException("Registro duplicado.", ex);
        }
    }

    public void DescartarAlteracoes() => ChangeTracker.Clear();

    /// <summary>
    /// Detecta violação de índice único sem acoplar a camada de aplicação ao provedor:
    /// Oracle = ORA-00001; SQLite = "UNIQUE constraint failed" (código 19).
    /// </summary>
    public static bool EhViolacaoDeUnicidade(DbUpdateException ex)
    {
        var mensagem = ex.InnerException?.Message ?? ex.Message;
        return mensagem.Contains("ORA-00001", StringComparison.OrdinalIgnoreCase)
            || mensagem.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MoveSyncDbContext).Assembly);
        ConvencaoNomesOracle.Aplicar(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Todas as datas são UTC. O conversor garante DateTimeKind.Utc na leitura (JSON sai com "Z").
        configurationBuilder.Properties<DateTime>().HaveConversion<ConversorDataUtc>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<ConversorDataUtc>();

        // Enums gravados como texto: um SELECT no banco fica legível (ex.: 'Adaptativo' em vez de 1).
        ConfigurarEnum<PerfilUsuario>(configurationBuilder);
        ConfigurarEnum<ModoOperacao>(configurationBuilder);
        ConfigurarEnum<ModoPlano>(configurationBuilder);
        ConfigurarEnum<TipoSensor>(configurationBuilder);
        ConfigurarEnum<TipoOcorrencia>(configurationBuilder);
        ConfigurarEnum<StatusOcorrencia>(configurationBuilder);
        ConfigurarEnum<PrioridadeOcorrencia>(configurationBuilder);
        ConfigurarEnum<MotivoTravessiaAcessivel>(configurationBuilder);
        ConfigurarEnum<TipoVeiculoEmergencia>(configurationBuilder);
    }

    private static void ConfigurarEnum<TEnum>(ModelConfigurationBuilder builder)
        where TEnum : struct, Enum =>
        builder.Properties<TEnum>().HaveConversion<string>().HaveMaxLength(30);
}

/// <summary>Garante que toda data lida do banco volte marcada como UTC.</summary>
public sealed class ConversorDataUtc : ValueConverter<DateTime, DateTime>
{
    public ConversorDataUtc()
        : base(
            valor => valor.Kind == DateTimeKind.Utc ? valor : valor.ToUniversalTime(),
            valor => DateTime.SpecifyKind(valor, DateTimeKind.Utc))
    {
    }
}
