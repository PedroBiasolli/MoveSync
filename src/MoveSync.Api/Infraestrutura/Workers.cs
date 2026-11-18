using MoveSync.Application.Servicos;
using MoveSync.Infrastructure.Idempotencia;

namespace MoveSync.Api.Infraestrutura;

/// <summary>Seção "Semaforos" do appsettings.</summary>
public sealed class OpcoesSemaforos
{
    public const string Secao = "Semaforos";

    /// <summary>Liga o recálculo automático dos planos (o "loop de controle" do sistema).</summary>
    public bool RecalculoAutomatico { get; set; } = true;

    public int IntervaloRecalculoSegundos { get; set; } = 30;
}

/// <summary>
/// Loop de controle: a cada N segundos recalcula o plano de todos os cruzamentos ativos com base na
/// telemetria recente. É isto que faz o sistema AGIR sobre o trânsito, e não apenas monitorar.
/// </summary>
public sealed class RecalculoPlanosWorker(
    IServiceScopeFactory fabricaEscopo,
    OpcoesSemaforos opcoes,
    ILogger<RecalculoPlanosWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!opcoes.RecalculoAutomatico)
        {
            logger.LogInformation("Recálculo automático de planos desabilitado por configuração.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, opcoes.IntervaloRecalculoSegundos)));
        do
        {
            try
            {
                await using var escopo = fabricaEscopo.CreateAsyncScope();
                var servico = escopo.ServiceProvider.GetRequiredService<PlanoSemaforicoService>();
                var resultado = await servico.RecalcularTodosAsync(stoppingToken);
                if (resultado.NovosPlanos > 0 || resultado.Falhas.Count > 0)
                {
                    logger.LogInformation(
                        "Recálculo: {Processados} cruzamento(s), {Novos} novo(s) plano(s), {Falhas} falha(s). {Detalhes}",
                        resultado.Processados,
                        resultado.NovosPlanos,
                        resultado.Falhas.Count,
                        string.Join(" | ", resultado.Falhas));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // O worker nunca pode morrer: registra e tenta de novo no próximo ciclo.
                logger.LogError(ex, "Falha no ciclo de recálculo dos planos semafóricos.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

/// <summary>Remove chaves de idempotência vencidas (mais de 24 h) a cada hora.</summary>
public sealed class LimpezaIdempotenciaWorker(
    IArmazenamentoIdempotencia armazenamento,
    OpcoesSemaforos opcoes,
    ILogger<LimpezaIdempotenciaWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!opcoes.RecalculoAutomatico)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var removidos = await armazenamento.RemoverExpiradosAsync(stoppingToken);
                logger.LogInformation("Limpeza de idempotência: {Removidos} registro(s) expirado(s) removido(s).", removidos);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Falha na limpeza de chaves de idempotência.");
            }
        }
    }
}
