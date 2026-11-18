using System.Net;
using System.Net.Http.Json;
using MoveSync.IntegrationTests.Infra;

namespace MoveSync.IntegrationTests;

/// <summary>Rate limiting (HTTP 429) com limites reduzidos.</summary>
public class LimiteRequisicoesTests(MoveSyncApiFactoryLimitesBaixos api) : IClassFixture<MoveSyncApiFactoryLimitesBaixos>
{
    [Fact]
    public async Task Login_ForcaBruta_Retorna429ComRetryAfter()
    {
        var cliente = api.CreateClient();
        HttpResponseMessage? ultima = null;

        // Limite configurado: 3 tentativas por minuto por IP.
        for (var i = 0; i < 5; i++)
        {
            ultima = await cliente.PostAsJsonAsync("/api/auth/login", new { email = "ataque@teste.dev", senha = "Tentativa" + i });
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, ultima!.StatusCode);
        Assert.True(ultima.Headers.Contains("Retry-After"));
        Assert.Equal("LIMITE_REQUISICOES", (await ultima.LerJsonAsync()).GetProperty("codigo").GetString());
    }
}

/// <summary>Rate limiting por sensor: um ESP32 com defeito não prejudica os outros.</summary>
public class LimitePorSensorTests(MoveSyncApiFactoryLimitesBaixos api) : IClassFixture<MoveSyncApiFactoryLimitesBaixos>
{
    [Fact]
    public async Task SensorInundandoAApi_Recebe429_EnquantoOutroSensorContinua()
    {
        var sensorDefeituoso = await api.ClienteSensorAsync("SPO001-V1");
        var sensorSaudavel = await api.ClienteSensorAsync("SPO001-V2");

        // Limite de telemetria: 2 por minuto por sensor.
        HttpResponseMessage? ultima = null;
        for (var i = 0; i < 4; i++)
        {
            ultima = await sensorDefeituoso.PostAsJsonAsync("/api/telemetria/leituras", MoveSyncApiFactory.Leitura($"flood-{i}", 1, 0));
        }

        var envioSaudavel = await sensorSaudavel.PostAsJsonAsync("/api/telemetria/leituras", MoveSyncApiFactory.Leitura("normal-1", 5, 1));

        Assert.Equal(HttpStatusCode.TooManyRequests, ultima!.StatusCode);
        Assert.Equal(HttpStatusCode.Created, envioSaudavel.StatusCode);
    }
}

/// <summary>Health checks e documentação OpenAPI.</summary>
public class MonitoramentoTests(MoveSyncApiFactory api) : IClassFixture<MoveSyncApiFactory>
{
    [Fact]
    public async Task HealthCompleto_InformaBancoSensoresEServicoExterno()
    {
        var resposta = await api.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var corpo = await resposta.LerJsonAsync();
        var verificacoes = corpo.GetProperty("verificacoes").EnumerateArray().ToDictionary(
            v => v.GetProperty("nome").GetString()!,
            v => v.GetProperty("status").GetString()!);
        Assert.Equal("Healthy", verificacoes["banco-de-dados"]);
        Assert.Equal("Healthy", verificacoes["servico-externo-nucleo-java"]);

        // Sensores do seed ainda não enviaram dados: Degraded (contingência), mas a API segue 200.
        Assert.Equal("Degraded", verificacoes["sensores-iot"]);
        Assert.Equal("Degraded", corpo.GetProperty("status").GetString());
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthLiveEReady_Retornam200(string rota)
    {
        var resposta = await api.CreateClient().GetAsync(rota);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("Healthy", (await resposta.LerJsonAsync()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task OpenApi_DocumentoPublicadoComEsquemaBearer()
    {
        var resposta = await api.CreateClient().GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var texto = await resposta.Content.ReadAsStringAsync();
        Assert.Contains("MoveSync API", texto, StringComparison.Ordinal);
        Assert.Contains("Bearer", texto, StringComparison.Ordinal);
        Assert.Contains("Idempotency-Key", texto, StringComparison.Ordinal);
    }
}
