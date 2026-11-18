using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using MoveSync.Application.Contratos;

namespace MoveSync.IntegrationTests.Infra;

/// <summary>
/// Sobe a API completa (Program.cs real) apontando para um arquivo SQLite temporário e exclusivo.
/// O seed cria o gestor, a cidadã de demonstração, 2 cruzamentos (SPO-001 e SPO-002) e um sensor por via.
/// </summary>
public class MoveSyncApiFactory : WebApplicationFactory<Program>
{
    public const string EmailGestor = "gestor@movesync.dev";
    public const string SenhaGestor = "Gestor@Teste1";
    public const string EmailCidada = "cidada@movesync.dev";
    public const string SenhaCidada = "Cidada@Teste1";
    public const string SegredoSensores = "segredo-dos-sensores-de-teste";

    private readonly string _arquivoBanco = Path.Combine(Path.GetTempPath(), $"movesync-testes-{Guid.NewGuid():N}.db");

    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Limites altos por padrão para não interferir; a classe de teste de rate limiting os reduz.</summary>
    protected virtual int LimiteAutenticacao => 1000;

    protected virtual int LimiteTelemetria => 1000;

    /// <summary>Configuração de teste: banco SQLite próprio, chave JWT de teste, seed conhecido e worker desligado.</summary>
    private Dictionary<string, string?> ConfiguracaoDeTeste() => new()
    {
        ["ConnectionStrings:MoveSync"] = $"Data Source={_arquivoBanco}",
        ["BancoDados:Provedor"] = "Sqlite",
        ["BancoDados:CriarTabelasAutomaticamente"] = "true",
        ["Jwt:Chave"] = "chave-exclusiva-dos-testes-de-integracao-0123456789",
        ["Semaforos:RecalculoAutomatico"] = "false",
        ["Seed:Habilitado"] = "true",
        ["Seed:GestorEmail"] = EmailGestor,
        ["Seed:GestorSenha"] = SenhaGestor,
        ["Seed:CidadaoEmail"] = EmailCidada,
        ["Seed:CidadaoSenha"] = SenhaCidada,
        ["Seed:SegredoSensores"] = SegredoSensores,
        ["LimiteRequisicoes:Autenticacao:Limite"] = LimiteAutenticacao.ToString(CultureInfo.InvariantCulture),
        ["LimiteRequisicoes:Publico:Limite"] = "1000",
        ["LimiteRequisicoes:Usuario:Limite"] = "1000",
        ["LimiteRequisicoes:Telemetria:Limite"] = LimiteTelemetria.ToString(CultureInfo.InvariantCulture)
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testes");

        // A configuração de teste entra como a ÚLTIMA fonte (maior prioridade), acima do appsettings.json.
        // A API lê as seções na criação dos serviços (depois do Build), então estes valores sempre valem.
        var configuracao = ConfiguracaoDeTeste();
        foreach (var (chave, valor) in configuracao)
        {
            builder.UseSetting(chave, valor);
        }

        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(configuracao));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            SqliteConnection.ClearAllPools();
            File.Delete(_arquivoBanco);
        }
        catch (IOException)
        {
            // arquivo temporário: se não der para apagar agora, o SO limpa depois
        }
    }

    // ---------------- Atalhos usados pelos testes ----------------

    public async Task<HttpClient> ClienteAutenticadoAsync(string email, string senha)
    {
        var cliente = CreateClient();
        var resposta = await cliente.PostAsJsonAsync("/api/auth/login", new { email, senha });
        resposta.EnsureSuccessStatusCode();
        var token = await resposta.Content.ReadFromJsonAsync<TokenResponse>(Json);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        return cliente;
    }

    public Task<HttpClient> ClienteGestorAsync() => ClienteAutenticadoAsync(EmailGestor, SenhaGestor);

    public Task<HttpClient> ClienteCidadaAsync() => ClienteAutenticadoAsync(EmailCidada, SenhaCidada);

    /// <summary>Cadastra um novo cidadão (e-mail único) e devolve o cliente já autenticado.</summary>
    public async Task<HttpClient> NovoCidadaoAsync()
    {
        var email = $"cidadao-{Guid.NewGuid():N}@teste.dev";
        var cliente = CreateClient();
        var registro = await cliente.PostAsJsonAsync("/api/auth/registro", new { nome = "Cidadão de Teste", email, senha = "Senha123" });
        registro.EnsureSuccessStatusCode();
        return await ClienteAutenticadoAsync(email, "Senha123");
    }

    public async Task<HttpClient> ClienteSensorAsync(string codigo)
    {
        var cliente = CreateClient();
        var resposta = await cliente.PostAsJsonAsync("/api/auth/sensores/token", new { codigo, segredo = SegredoSensores });
        resposta.EnsureSuccessStatusCode();
        var token = await resposta.Content.ReadFromJsonAsync<TokenResponse>(Json);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        return cliente;
    }

    public async Task<CruzamentoResponse> CruzamentoSeedAsync(string codigo)
    {
        var pagina = await CreateClient().GetFromJsonAsync<PaginaResponse<CruzamentoResponse>>($"/api/cruzamentos?busca={codigo}", Json);
        return pagina!.Itens.Single(c => c.Codigo == codigo);
    }

    public static object Leitura(string leituraId, int veiculos, int fila, DateTime? coletadaEm = null) => new
    {
        leituraId,
        coletadaEm = coletadaEm ?? DateTime.UtcNow.AddSeconds(-10),
        veiculosContados = veiculos,
        tamanhoFila = fila
    };
}

/// <summary>Mesma API com limites baixos, para provar o HTTP 429.</summary>
public sealed class MoveSyncApiFactoryLimitesBaixos : MoveSyncApiFactory
{
    protected override int LimiteAutenticacao => 3;

    protected override int LimiteTelemetria => 2;
}

public static class ExtensoesHttp
{
    public static HttpRequestMessage ComIdempotencia(this HttpRequestMessage requisicao, string chave)
    {
        requisicao.Headers.Add("Idempotency-Key", chave);
        return requisicao;
    }

    public static async Task<JsonElement> LerJsonAsync(this HttpResponseMessage resposta)
    {
        var texto = await resposta.Content.ReadAsStringAsync();
        return JsonDocument.Parse(texto).RootElement.Clone();
    }

    public static async Task<T> LerAsync<T>(this HttpResponseMessage resposta) =>
        (await resposta.Content.ReadFromJsonAsync<T>(MoveSyncApiFactory.Json))!;
}
