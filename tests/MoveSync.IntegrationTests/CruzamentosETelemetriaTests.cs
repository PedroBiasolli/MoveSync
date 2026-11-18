using System.Net;
using System.Net.Http.Json;
using MoveSync.Application.Contratos;
using MoveSync.Domain.Enums;
using MoveSync.IntegrationTests.Infra;

namespace MoveSync.IntegrationTests;

/// <summary>CRUD de cruzamentos, telemetria idempotente e o fluxo principal do motor semafórico.</summary>
public class CruzamentosETelemetriaTests(MoveSyncApiFactory api) : IClassFixture<MoveSyncApiFactory>
{
    private static object NovoCruzamento(string codigo) => new
    {
        codigo,
        nome = "Av. de Teste x R. de Integração",
        endereco = "Av. de Teste, 500 - Osasco/SP",
        latitude = -23.53,
        longitude = -46.79,
        larguraTravessiaMetros = 12,
        vias = new[]
        {
            new { nome = "Av. de Teste", sentido = "Centro → Bairro", numeroFaixas = 3, velocidadeMaximaKmh = 50 },
            new { nome = "R. de Integração", sentido = "Norte → Sul", numeroFaixas = 1, velocidadeMaximaKmh = 40 }
        }
    };

    [Fact]
    public async Task Cruzamento_CrudCompletoPeloGestor()
    {
        var gestor = await api.ClienteGestorAsync();
        var codigo = $"INT-{Random.Shared.Next(1000, 9999)}";

        // CREATE
        var criacao = await gestor.PostAsJsonAsync("/api/cruzamentos", NovoCruzamento(codigo));
        Assert.Equal(HttpStatusCode.Created, criacao.StatusCode);
        Assert.NotNull(criacao.Headers.Location);
        var criado = await criacao.LerAsync<CruzamentoResponse>();
        Assert.Equal(2, criado.Vias.Count);
        Assert.Equal(ModoOperacao.Adaptativo, criado.Modo);

        // READ (público)
        var leitura = await api.CreateClient().GetAsync($"/api/cruzamentos/{criado.Id}");
        Assert.Equal(HttpStatusCode.OK, leitura.StatusCode);

        // UPDATE
        var atualizacao = await gestor.PutAsJsonAsync($"/api/cruzamentos/{criado.Id}", new
        {
            nome = "Nome Atualizado",
            endereco = "Endereço atualizado, 1",
            latitude = -23.5,
            longitude = -46.7,
            larguraTravessiaMetros = 15,
            ativo = true
        });
        Assert.Equal(HttpStatusCode.OK, atualizacao.StatusCode);
        Assert.Equal("Nome Atualizado", (await atualizacao.LerAsync<CruzamentoResponse>()).Nome);

        // DELETE
        var exclusao = await gestor.DeleteAsync($"/api/cruzamentos/{criado.Id}");
        Assert.Equal(HttpStatusCode.NoContent, exclusao.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.CreateClient().GetAsync($"/api/cruzamentos/{criado.Id}")).StatusCode);
    }

    [Fact]
    public async Task Cruzamento_CodigoDuplicado_Retorna409()
    {
        var gestor = await api.ClienteGestorAsync();

        var resposta = await gestor.PostAsJsonAsync("/api/cruzamentos", NovoCruzamento("SPO-001"));

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
        Assert.Equal("CODIGO_DUPLICADO", (await resposta.LerJsonAsync()).GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task Cruzamento_ComUmaViaApenas_Retorna400()
    {
        var gestor = await api.ClienteGestorAsync();
        var invalido = new
        {
            codigo = "INV-001",
            nome = "Cruzamento inválido",
            endereco = "Rua inválida, 1",
            latitude = 0,
            longitude = 0,
            larguraTravessiaMetros = 10,
            vias = new[] { new { nome = "Única", sentido = "N → S", numeroFaixas = 1, velocidadeMaximaKmh = 40 } }
        };

        var resposta = await gestor.PostAsJsonAsync("/api/cruzamentos", invalido);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Listagem_EhPublicaEPaginada()
    {
        var resposta = await api.CreateClient().GetAsync("/api/cruzamentos?pagina=1&tamanhoPagina=1");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var pagina = await resposta.LerAsync<PaginaResponse<CruzamentoResponse>>();
        Assert.Single(pagina.Itens);
        Assert.True(pagina.TotalItens >= 2);
        Assert.True(pagina.TotalPaginas >= 2);
    }

    [Fact]
    public async Task Listagem_PaginacaoInvalida_Retorna400() =>
        Assert.Equal(HttpStatusCode.BadRequest, (await api.CreateClient().GetAsync("/api/cruzamentos?pagina=0&tamanhoPagina=500")).StatusCode);

    [Fact]
    public async Task Telemetria_MesmaLeituraDuasVezes_201DepoisReplay200SemDuplicar()
    {
        var sensor = await api.ClienteSensorAsync("SPO002-V1");
        var leitura = MoveSyncApiFactory.Leitura($"esp32-{Guid.NewGuid():N}", 20, 3);

        var primeira = await sensor.PostAsJsonAsync("/api/telemetria/leituras", leitura);
        var segunda = await sensor.PostAsJsonAsync("/api/telemetria/leituras", leitura);

        Assert.Equal(HttpStatusCode.Created, primeira.StatusCode);
        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);
        Assert.Equal("true", segunda.Headers.GetValues("Idempotency-Replayed").Single());
        Assert.Equal((await primeira.LerAsync<LeituraResponse>()).Id, (await segunda.LerAsync<LeituraResponse>()).Id);
    }

    [Fact]
    public async Task Telemetria_LeituraNoFuturo_Retorna422()
    {
        var sensor = await api.ClienteSensorAsync("SPO002-V2");

        var resposta = await sensor.PostAsJsonAsync("/api/telemetria/leituras", MoveSyncApiFactory.Leitura("futuro-1", 5, 0, DateTime.UtcNow.AddHours(2)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.Equal("LEITURA_NO_FUTURO", (await resposta.LerJsonAsync()).GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task Telemetria_EnviadaPorCidadao_Retorna403()
    {
        var cidada = await api.ClienteCidadaAsync();

        var resposta = await cidada.PostAsJsonAsync("/api/telemetria/leituras", MoveSyncApiFactory.Leitura("x", 1, 0));

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task Status_SemTelemetria_CidadaoVeModoContingencia()
    {
        var cruzamento = await api.CruzamentoSeedAsync("SPO-001");

        var resposta = await api.CreateClient().GetAsync($"/api/cruzamentos/{cruzamento.Id}/status");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var status = await resposta.LerAsync<StatusCruzamentoResponse>();
        Assert.Equal(ModoPlano.Contingencia, status.Modo);
        Assert.Equal(4, status.Vias.Count);
        Assert.NotEmpty(status.Avisos);
    }

    [Fact]
    public async Task FluxoPrincipal_SensoresEnviamDados_PlanoAdaptativoEIndicadoresDeImpacto()
    {
        var cruzamento = await api.CruzamentoSeedAsync("SPO-002");
        var gestor = await api.ClienteGestorAsync();

        // 1) Cada sensor (um por via) envia telemetria: Faria Lima muito carregada, Rebouças vazias.
        var cargas = new Dictionary<string, (int Veiculos, int Fila)>
        {
            ["SPO002-V1"] = (140, 25),
            ["SPO002-V2"] = (15, 2),
            ["SPO002-V3"] = (10, 1)
        };
        foreach (var (codigo, carga) in cargas)
        {
            var sensor = await api.ClienteSensorAsync(codigo);
            var envio = await sensor.PostAsJsonAsync("/api/telemetria/leituras", MoveSyncApiFactory.Leitura($"{codigo}-{Guid.NewGuid():N}", carga.Veiculos, carga.Fila));
            Assert.Equal(HttpStatusCode.Created, envio.StatusCode);
        }

        // 2) O motor recalcula: modo adaptativo, mais verde para a via mais carregada.
        var recalculo = await gestor.PostAsync($"/api/cruzamentos/{cruzamento.Id}/planos/recalcular", null);
        Assert.Equal(HttpStatusCode.OK, recalculo.StatusCode);
        var plano = await recalculo.LerAsync<PlanoResponse>();
        Assert.Equal(ModoPlano.Adaptativo, plano.Modo);
        var maisVerde = plano.Fases.MaxBy(f => f.VerdeSegundos)!;
        Assert.Equal("Av. Faria Lima sentido Pinheiros", maisVerde.NomeVia);
        Assert.False(string.IsNullOrWhiteSpace(plano.Justificativa));

        // 3) O cidadão vê o status (público) baseado no plano gravado.
        var status = await api.CreateClient().GetFromJsonAsync<StatusCruzamentoResponse>($"/api/cruzamentos/{cruzamento.Id}/status", MoveSyncApiFactory.Json);
        Assert.Equal(ModoPlano.Adaptativo, status!.Modo);
        Assert.False(status.PlanoProvisorio);

        // 4) O gestor mede o impacto (tempo fixo × adaptativo).
        var indicadores = await gestor.GetFromJsonAsync<IndicadoresResponse>($"/api/cruzamentos/{cruzamento.Id}/indicadores", MoveSyncApiFactory.Json);
        Assert.True(indicadores!.DadosSuficientes);
        Assert.NotNull(indicadores.Comparacao);
        Assert.True(indicadores.Comparacao!.ReducaoAtrasoPercentual > 0);

        // 5) Recalcular de novo com os mesmos dados não gera plano novo (idempotência natural).
        var novamente = await (await gestor.PostAsync($"/api/cruzamentos/{cruzamento.Id}/planos/recalcular", null)).LerAsync<PlanoResponse>();
        Assert.Equal(plano.Id, novamente.Id);
    }

    [Fact]
    public async Task Emergencia_AcionarEEncerrar_CorredorVerdeEVoltaAoNormal()
    {
        var cruzamento = await api.CruzamentoSeedAsync("SPO-001");
        var via = cruzamento.Vias.First();
        var gestor = await api.ClienteGestorAsync();

        var acionamento = new HttpRequestMessage(HttpMethod.Post, $"/api/cruzamentos/{cruzamento.Id}/emergencias")
        {
            Content = JsonContent.Create(new { viaId = via.Id, tipoVeiculo = "Ambulancia", duracaoSegundos = 120 })
        }.ComIdempotencia(Guid.NewGuid().ToString());
        var resposta = await gestor.SendAsync(acionamento);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var emergencia = await resposta.LerAsync<EmergenciaResponse>();
        Assert.Equal(ModoPlano.Emergencia, emergencia.PlanoAplicado!.Modo);
        Assert.Equal(90, emergencia.PlanoAplicado.Fases.Single(f => f.ViaId == via.Id).VerdeSegundos);

        var encerramento = await gestor.PostAsync($"/api/emergencias/{emergencia.Id}/encerrar", null);
        Assert.Equal(HttpStatusCode.OK, encerramento.StatusCode);
        Assert.NotEqual(ModoPlano.Emergencia, (await encerramento.LerAsync<EmergenciaResponse>()).PlanoAplicado!.Modo);

        var encerrarDeNovo = await gestor.PostAsync($"/api/emergencias/{emergencia.Id}/encerrar", null);
        Assert.Equal(HttpStatusCode.Conflict, encerrarDeNovo.StatusCode);
    }
}
