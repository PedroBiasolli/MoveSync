using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MoveSync.Application.Abstracoes;
using MoveSync.Infrastructure.Idempotencia;
using MoveSync.Infrastructure.Integracoes;
using MoveSync.Infrastructure.Persistencia;
using MoveSync.Infrastructure.Repositorios;
using MoveSync.Infrastructure.Seguranca;

namespace MoveSync.Infrastructure;

public static class InjecaoDependencias
{
    public const string NomeConnectionString = "MoveSync";

    /// <summary>Registra banco de dados, repositórios, segurança e integrações externas.</summary>
    public static IServiceCollection AdicionarInfraestrutura(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OpcoesBancoDados>(configuration.GetSection(OpcoesBancoDados.Secao));
        services.Configure<OpcoesSeed>(configuration.GetSection(OpcoesSeed.Secao));
        services.Configure<OpcoesNucleoJava>(configuration.GetSection(OpcoesNucleoJava.Secao));

        // AddDbContextFactory registra a fábrica (singleton) E o próprio DbContext (scoped).
        // Provedor e connection string são lidos na criação das opções (depois do Build), não aqui:
        // assim variáveis de ambiente e os testes de integração sempre prevalecem.
        services.AddDbContextFactory<MoveSyncDbContext>((sp, options) =>
        {
            var configuracao = sp.GetRequiredService<IConfiguration>();
            var opcoesBanco = sp.GetRequiredService<IOptions<OpcoesBancoDados>>().Value;
            var connectionString = configuracao.GetConnectionString(NomeConnectionString);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"Connection string '{NomeConnectionString}' não configurada. " +
                    $"Defina a variável de ambiente ConnectionStrings__{NomeConnectionString}.");
            }

            if (opcoesBanco.EhOracle)
            {
                options.UseOracle(connectionString, oracle => oracle.UseOracleSQLCompatibility(opcoesBanco.VersaoOracle switch
                {
                    23 => OracleSQLCompatibility.DatabaseVersion23,
                    21 => OracleSQLCompatibility.DatabaseVersion21,
                    _ => OracleSQLCompatibility.DatabaseVersion19
                }));
            }
            else
            {
                options.UseSqlite(connectionString);
            }
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<MoveSyncDbContext>());
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<ICruzamentoRepository, CruzamentoRepository>();
        services.AddScoped<ISensorRepository, SensorRepository>();
        services.AddScoped<ILeituraRepository, LeituraRepository>();
        services.AddScoped<IPlanoRepository, PlanoRepository>();
        services.AddScoped<IOcorrenciaRepository, OcorrenciaRepository>();
        services.AddScoped<ISolicitacaoTravessiaRepository, SolicitacaoTravessiaRepository>();
        services.AddScoped<IEmergenciaRepository, EmergenciaRepository>();

        services.AddSingleton<IHashSenha, HashSenhaPbkdf2>();
        services.AddSingleton<IArmazenamentoIdempotencia, ArmazenamentoIdempotenciaEf>();

        // HttpClient tipado para o núcleo Java, com timeout curto: a API .NET nunca fica presa esperando.
        services.AddHttpClient<INotificadorNucleo, NotificadorNucleoHttp>(NotificadorNucleoHttp.NomeCliente, ConfigurarClienteNucleo);
        services.AddHttpClient(NotificadorNucleoHttp.NomeCliente, ConfigurarClienteNucleo);

        return services;
    }

    private static void ConfigurarClienteNucleo(IServiceProvider sp, HttpClient cliente)
    {
        var opcoes = sp.GetRequiredService<IOptions<OpcoesNucleoJava>>().Value;
        if (opcoes.Habilitada)
        {
            cliente.BaseAddress = new Uri(opcoes.UrlBase!.TrimEnd('/') + "/");
        }

        cliente.Timeout = TimeSpan.FromSeconds(Math.Clamp(opcoes.TimeoutSegundos, 1, 30));
    }
}
