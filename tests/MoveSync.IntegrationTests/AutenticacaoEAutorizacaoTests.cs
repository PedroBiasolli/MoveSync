using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MoveSync.Application.Contratos;
using MoveSync.IntegrationTests.Infra;

namespace MoveSync.IntegrationTests;

/// <summary>JWT: login, perfis (Cidadão, Gestor, Sensor) e proteção dos endpoints.</summary>
public class AutenticacaoEAutorizacaoTests(MoveSyncApiFactory api) : IClassFixture<MoveSyncApiFactory>
{
    [Fact]
    public async Task Login_GestorValido_Retorna200ComTokenEPerfil()
    {
        var resposta = await api.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = MoveSyncApiFactory.EmailGestor, senha = MoveSyncApiFactory.SenhaGestor });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var token = await resposta.LerAsync<TokenResponse>();
        Assert.False(string.IsNullOrWhiteSpace(token.AccessToken));
        Assert.Equal("Gestor", token.Perfil);
        Assert.True(token.ExpiraEm > DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_SenhaErrada_Retorna401ProblemDetails()
    {
        var resposta = await api.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = MoveSyncApiFactory.EmailGestor, senha = "SenhaErrada1" });

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        var corpo = await resposta.LerJsonAsync();
        Assert.Equal("CREDENCIAIS_INVALIDAS", corpo.GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task Registro_DadosInvalidos_Retorna400ComErroPorCampo()
    {
        var resposta = await api.CreateClient().PostAsJsonAsync("/api/auth/registro", new { nome = "A", email = "email-invalido", senha = "123" });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var erros = (await resposta.LerJsonAsync()).GetProperty("errors");
        Assert.True(erros.TryGetProperty("Nome", out _));
        Assert.True(erros.TryGetProperty("Email", out _));
        Assert.True(erros.TryGetProperty("Senha", out _));
    }

    [Fact]
    public async Task Registro_EmailJaCadastrado_Retorna409()
    {
        var resposta = await api.CreateClient().PostAsJsonAsync("/api/auth/registro", new { nome = "Outra Pessoa", email = MoveSyncApiFactory.EmailCidada, senha = "Senha1234" });

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
    }

    [Fact]
    public async Task JsonMalformado_Retorna400ProblemDetails()
    {
        var conteudo = new StringContent("{ isto não é json", System.Text.Encoding.UTF8, "application/json");

        var resposta = await api.CreateClient().PostAsync("/api/auth/login", conteudo);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal("REQUISICAO_INVALIDA", (await resposta.LerJsonAsync()).GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task EndpointProtegido_SemToken_Retorna401()
    {
        var resposta = await api.CreateClient().GetAsync("/api/painel/resumo");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task TokenAdulterado_Retorna401()
    {
        var cliente = api.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJ4In0.assinatura-falsa");

        var resposta = await cliente.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task EndpointDeGestor_ComTokenDeCidadao_Retorna403()
    {
        var cidada = await api.ClienteCidadaAsync();

        var resposta = await cidada.GetAsync("/api/painel/resumo");

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task Me_RetornaDadosDoUsuarioLogadoSemExporSenha()
    {
        var cidada = await api.ClienteCidadaAsync();

        var resposta = await cidada.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var texto = await resposta.Content.ReadAsStringAsync();
        Assert.Contains(MoveSyncApiFactory.EmailCidada, texto, StringComparison.Ordinal);
        Assert.DoesNotContain("senha", texto, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sensor_SegredoErrado_Retorna401()
    {
        var resposta = await api.CreateClient().PostAsJsonAsync("/api/auth/sensores/token", new { codigo = "SPO002-V1", segredo = "segredo-incorreto-123456" });

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Sensor_NaoAcessaEndpointsDePessoas_Retorna403()
    {
        var sensor = await api.ClienteSensorAsync("SPO002-V1");

        var resposta = await sensor.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }
}
