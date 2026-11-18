using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MoveSync.Domain.Servicos;
using MoveSync.Infrastructure.Integracoes;
using MoveSync.Infrastructure.Persistencia;

namespace MoveSync.Infrastructure.Saude;

/// <summary>Banco de dados: consegue conectar e consultar? Se não, a API está "Unhealthy" (503).</summary>
public sealed class BancoDadosHealthCheck(MoveSyncDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var cronometro = Stopwatch.StartNew();
        try
        {
            // Uma consulta real (e não só abrir conexão) confirma que as tabelas existem.
            var cruzamentos = await db.Cruzamentos.CountAsync(cancellationToken);
            cronometro.Stop();
            var dados = new Dictionary<string, object>
            {
                ["provedor"] = db.Database.ProviderName ?? "desconhecido",
                ["latenciaMs"] = cronometro.ElapsedMilliseconds,
                ["cruzamentosCadastrados"] = cruzamentos
            };
            return HealthCheckResult.Healthy("Banco de dados acessível.", dados);
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Falha ao acessar o banco de dados.", ex);
        }
    }
}

/// <summary>
/// Sensores IoT: quantos estão enviando dados. Sensor fora do ar deixa a API "Degraded" (e não Unhealthy),
/// porque o motor entra em CONTINGÊNCIA com tempo fixo seguro — o semáforo continua funcionando.
/// </summary>
public sealed class SensoresHealthCheck(
    MoveSyncDbContext db,
    ParametrosSemaforicos parametros,
    TimeProvider relogio) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var agora = relogio.GetUtcNow().UtcDateTime;
        var tolerancia = TimeSpan.FromMinutes(parametros.JanelaDadosMinutos);
        var ativos = await db.Sensores.AsNoTracking().Where(s => s.Ativo).ToListAsync(cancellationToken);
        var offline = ativos.Where(s => s.EstaOffline(agora, tolerancia)).Select(s => s.Codigo).OrderBy(c => c).ToList();

        var dados = new Dictionary<string, object>
        {
            ["sensoresAtivos"] = ativos.Count,
            ["sensoresOnline"] = ativos.Count - offline.Count,
            ["sensoresOffline"] = offline.Count,
            ["codigosOffline"] = offline,
            ["toleranciaMinutos"] = parametros.JanelaDadosMinutos
        };

        if (ativos.Count == 0)
        {
            return HealthCheckResult.Healthy("Nenhum sensor ativo cadastrado.", dados);
        }

        if (offline.Count == 0)
        {
            return HealthCheckResult.Healthy("Todos os sensores estão enviando dados.", dados);
        }

        var descricao = offline.Count == ativos.Count
            ? "Nenhum sensor enviou dados recentes: cruzamentos operando em contingência (tempo fixo seguro)."
            : $"{offline.Count} de {ativos.Count} sensor(es) sem dados recentes: cruzamentos afetados em contingência.";
        return HealthCheckResult.Degraded(descricao, data: dados);
    }
}

/// <summary>Serviço externo: núcleo Java (Spring Boot Actuator). Fora do ar = Degraded.</summary>
public sealed class NucleoJavaHealthCheck(
    IHttpClientFactory httpClientFactory,
    IOptions<OpcoesNucleoJava> opcoes) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var config = opcoes.Value;
        if (!config.Habilitada)
        {
            return HealthCheckResult.Healthy("Integração com o núcleo Java não configurada (opcional).");
        }

        var cronometro = Stopwatch.StartNew();
        try
        {
            var cliente = httpClientFactory.CreateClient(NotificadorNucleoHttp.NomeCliente);
            using var resposta = await cliente.GetAsync(config.CaminhoHealth, cancellationToken);
            cronometro.Stop();
            var dados = new Dictionary<string, object>
            {
                ["url"] = config.UrlBase!,
                ["statusHttp"] = (int)resposta.StatusCode,
                ["latenciaMs"] = cronometro.ElapsedMilliseconds
            };

            return resposta.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("Núcleo Java respondendo.", dados)
                : new HealthCheckResult(context.Registration.FailureStatus, $"Núcleo Java respondeu HTTP {(int)resposta.StatusCode}.", data: dados);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Núcleo Java inacessível.", ex);
        }
    }
}
