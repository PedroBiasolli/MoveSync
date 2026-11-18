using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoveSync.Application.Abstracoes;

namespace MoveSync.Infrastructure.Integracoes;

/// <summary>Configuração da integração com o núcleo Java (seção "Integracoes:NucleoJava").</summary>
public sealed class OpcoesNucleoJava
{
    public const string Secao = "Integracoes:NucleoJava";

    /// <summary>URL base da API Java (ex.: https://movesync-java.azurewebsites.net). Vazio = integração desligada.</summary>
    public string? UrlBase { get; set; }

    /// <summary>Caminho do health check do Spring Boot Actuator.</summary>
    public string CaminhoHealth { get; set; } = "/actuator/health";

    /// <summary>Caminho que recebe os eventos da API .NET.</summary>
    public string CaminhoEventos { get; set; } = "/api/integracoes/eventos";

    public int TimeoutSegundos { get; set; } = 3;

    public bool Habilitada => !string.IsNullOrWhiteSpace(UrlBase);
}

/// <summary>
/// Envia eventos relevantes (emergência acionada, ocorrência crítica, cruzamento em contingência)
/// para o núcleo Java, que publica no RabbitMQ e usa o Spring AI para resumir para o gestor.
/// Falhas são registradas em log e NUNCA interrompem a operação principal (resiliência).
/// </summary>
public sealed class NotificadorNucleoHttp(
    HttpClient httpClient,
    IOptions<OpcoesNucleoJava> opcoes,
    ILogger<NotificadorNucleoHttp> logger) : INotificadorNucleo
{
    public const string NomeCliente = "nucleo-java";

    public async Task NotificarAsync(EventoIntegracao evento, CancellationToken ct)
    {
        var config = opcoes.Value;
        if (!config.Habilitada)
        {
            logger.LogDebug("Integração com o núcleo Java desabilitada; evento {Tipo} não enviado.", evento.Tipo);
            return;
        }

        try
        {
            using var resposta = await httpClient.PostAsJsonAsync(config.CaminhoEventos, evento, ct);
            if (!resposta.IsSuccessStatusCode)
            {
                logger.LogWarning("Núcleo Java respondeu {Status} ao evento {Tipo}.", (int)resposta.StatusCode, evento.Tipo);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Falha ao notificar o núcleo Java sobre o evento {Tipo}. A operação local foi concluída normalmente.", evento.Tipo);
        }
    }
}
