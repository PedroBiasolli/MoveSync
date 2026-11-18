using System.Net;
using System.Net.Http.Json;
using MoveSync.IntegrationTests.Infra;

namespace MoveSync.IntegrationTests;

/// <summary>
/// Relatório de impacto e benefício/custo (GET /api/painel/impacto): o modelo de negócio exposto pela API.
/// A prefeitura usa esse relatório para justificar a assinatura e a expansão para novos cruzamentos.
/// </summary>
public class PainelImpactoTests(MoveSyncApiFactory api) : IClassFixture<MoveSyncApiFactory>
{
    [Fact]
    public async Task Impacto_ComTelemetriaNasVias_RetornaGanhoEBeneficioCusto()
    {
        // Arrange: demanda desequilibrada no SPO-001 (avenida cheia, transversais vazias).
        var demanda = new[] { ("SPO001-V1", 95, 14), ("SPO001-V2", 90, 12), ("SPO001-V3", 8, 1), ("SPO001-V4", 6, 1) };
        foreach (var (codigo, veiculos, fila) in demanda)
        {
            var sensor = await api.ClienteSensorAsync(codigo);
            var envio = await sensor.PostAsJsonAsync("/api/telemetria/leituras", MoveSyncApiFactory.Leitura($"impacto-{codigo}", veiculos, fila));
            Assert.Equal(HttpStatusCode.Created, envio.StatusCode);
        }

        var gestor = await api.ClienteGestorAsync();

        // Act
        var resposta = await gestor.GetAsync("/api/painel/impacto");

        // Assert
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var json = await resposta.LerJsonAsync();
        Assert.True(json.GetProperty("cruzamentosComDados").GetInt32() >= 1);
        var resumo = json.GetProperty("resumo");
        Assert.True(resumo.GetProperty("horasEvitadasPorMes").GetDouble() > 0);
        Assert.True(resumo.GetProperty("valorTempoEconomizadoMesReais").GetDecimal() > 0);
        Assert.True(resumo.GetProperty("relacaoBeneficioCusto").GetDouble() > 0);
        Assert.Equal(21.15m, json.GetProperty("premissas").GetProperty("valorHoraCidadaoReais").GetDecimal());
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("interpretacao").GetString()));
    }

    [Fact]
    public async Task Impacto_SimulacaoComPilotoGratuito_RelacaoBeneficioCustoNula()
    {
        var gestor = await api.ClienteGestorAsync();

        var resposta = await gestor.GetAsync("/api/painel/impacto?assinaturaMensalPorCruzamentoReais=0&horasReferenciaPorDia=16");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var json = await resposta.LerJsonAsync();
        Assert.Equal(16, json.GetProperty("premissas").GetProperty("horasReferenciaPorDia").GetInt32());
        Assert.Equal(0m, json.GetProperty("resumo").GetProperty("assinaturaMensalReais").GetDecimal());
    }

    [Theory]
    [InlineData("horasReferenciaPorDia=25")]
    [InlineData("horasReferenciaPorDia=0")]
    [InlineData("valorHoraReais=-5")]
    public async Task Impacto_ParametroDeSimulacaoForaDaFaixa_Retorna400(string consulta)
    {
        var gestor = await api.ClienteGestorAsync();

        var resposta = await gestor.GetAsync($"/api/painel/impacto?{consulta}");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Impacto_AcessadoPorCidadao_Retorna403()
    {
        var cidada = await api.ClienteCidadaAsync();

        var resposta = await cidada.GetAsync("/api/painel/impacto");

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task Impacto_SemToken_Retorna401()
    {
        var resposta = await api.CreateClient().GetAsync("/api/painel/impacto");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }
}
