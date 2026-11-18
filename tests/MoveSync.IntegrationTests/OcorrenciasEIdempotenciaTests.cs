using System.Net;
using System.Net.Http.Json;
using MoveSync.Application.Contratos;
using MoveSync.Domain.Enums;
using MoveSync.IntegrationTests.Infra;

namespace MoveSync.IntegrationTests;

/// <summary>Ocorrências do cidadão e o middleware de idempotência (Idempotency-Key).</summary>
public class OcorrenciasEIdempotenciaTests(MoveSyncApiFactory api) : IClassFixture<MoveSyncApiFactory>
{
    private async Task<HttpRequestMessage> NovaOcorrenciaAsync(string? chave, string descricao = "Semáforo apagado desde as 7h da manhã")
    {
        var cruzamento = await api.CruzamentoSeedAsync("SPO-001");
        var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/ocorrencias")
        {
            Content = JsonContent.Create(new { cruzamentoId = cruzamento.Id, tipo = "SemaforoApagado", descricao })
        };
        return chave is null ? requisicao : requisicao.ComIdempotencia(chave);
    }

    [Fact]
    public async Task CriarOcorrencia_SemIdempotencyKey_Retorna400()
    {
        var cidadao = await api.NovoCidadaoAsync();

        var resposta = await cidadao.SendAsync(await NovaOcorrenciaAsync(chave: null));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal("IDEMPOTENCY_KEY_OBRIGATORIA", (await resposta.LerJsonAsync()).GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task CriarOcorrencia_MesmaChaveRepetida_DevolveAMesmaRespostaSemDuplicar()
    {
        var cidadao = await api.NovoCidadaoAsync();
        var chave = Guid.NewGuid().ToString();

        var primeira = await cidadao.SendAsync(await NovaOcorrenciaAsync(chave));
        var segunda = await cidadao.SendAsync(await NovaOcorrenciaAsync(chave));

        Assert.Equal(HttpStatusCode.Created, primeira.StatusCode);
        Assert.Equal(HttpStatusCode.Created, segunda.StatusCode);
        Assert.False(primeira.Headers.Contains("Idempotency-Replayed"));
        Assert.Equal("true", segunda.Headers.GetValues("Idempotency-Replayed").Single());
        Assert.Equal(primeira.Headers.Location, segunda.Headers.Location);

        var criada = await primeira.LerAsync<OcorrenciaResponse>();
        Assert.Equal(criada.Id, (await segunda.LerAsync<OcorrenciaResponse>()).Id);
        Assert.Equal(PrioridadeOcorrencia.Critica, criada.Prioridade);

        var minhas = await cidadao.GetFromJsonAsync<PaginaResponse<OcorrenciaResponse>>("/api/ocorrencias/minhas", MoveSyncApiFactory.Json);
        Assert.Equal(1, minhas!.TotalItens);
    }

    [Fact]
    public async Task CriarOcorrencia_MesmaChaveComCorpoDiferente_Retorna422()
    {
        var cidadao = await api.NovoCidadaoAsync();
        var chave = Guid.NewGuid().ToString();
        await cidadao.SendAsync(await NovaOcorrenciaAsync(chave));

        var resposta = await cidadao.SendAsync(await NovaOcorrenciaAsync(chave, "Outra descrição completamente diferente"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.Equal("IDEMPOTENCY_KEY_REUTILIZADA", (await resposta.LerJsonAsync()).GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task MesmaChave_UsuariosDiferentes_NaoColidem()
    {
        var chave = Guid.NewGuid().ToString();
        var cidadaoA = await api.NovoCidadaoAsync();
        var cidadaoB = await api.NovoCidadaoAsync();

        var respostaA = await cidadaoA.SendAsync(await NovaOcorrenciaAsync(chave));
        var respostaB = await cidadaoB.SendAsync(await NovaOcorrenciaAsync(chave));

        Assert.Equal(HttpStatusCode.Created, respostaB.StatusCode);
        Assert.NotEqual((await respostaA.LerAsync<OcorrenciaResponse>()).Id, (await respostaB.LerAsync<OcorrenciaResponse>()).Id);
    }

    [Fact]
    public async Task CicloDeVidaDaOcorrencia_CidadaoEditaGestorAnalisaEdicaoBloqueia()
    {
        var cidadao = await api.NovoCidadaoAsync();
        var outroCidadao = await api.NovoCidadaoAsync();
        var gestor = await api.ClienteGestorAsync();
        var criada = await (await cidadao.SendAsync(await NovaOcorrenciaAsync(Guid.NewGuid().ToString()))).LerAsync<OcorrenciaResponse>();

        // Cidadão edita enquanto está Aberta
        var edicao = await cidadao.PutAsJsonAsync($"/api/ocorrencias/{criada.Id}", new { tipo = "TravessiaInsegura", descricao = "Faixa de pedestres apagada e sem sinal" });
        Assert.Equal(HttpStatusCode.OK, edicao.StatusCode);
        Assert.Equal(PrioridadeOcorrencia.Alta, (await edicao.LerAsync<OcorrenciaResponse>()).Prioridade);

        // Outro cidadão não enxerga (404, sem revelar que existe)
        Assert.Equal(HttpStatusCode.NotFound, (await outroCidadao.GetAsync($"/api/ocorrencias/{criada.Id}")).StatusCode);

        // Gestor coloca em análise
        var analise = await gestor.PatchAsJsonAsync($"/api/ocorrencias/{criada.Id}/status", new { status = "EmAnalise" });
        Assert.Equal(HttpStatusCode.OK, analise.StatusCode);

        // Em análise o cidadão não pode mais editar nem excluir
        var edicaoBloqueada = await cidadao.PutAsJsonAsync($"/api/ocorrencias/{criada.Id}", new { tipo = "Outro", descricao = "Tentativa de edição tardia" });
        Assert.Equal(HttpStatusCode.Conflict, edicaoBloqueada.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await cidadao.DeleteAsync($"/api/ocorrencias/{criada.Id}")).StatusCode);

        // Resolver sem resposta viola regra (422); com resposta, OK
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await gestor.PatchAsJsonAsync($"/api/ocorrencias/{criada.Id}/status", new { status = "Resolvida" })).StatusCode);
        var resolvida = await gestor.PatchAsJsonAsync($"/api/ocorrencias/{criada.Id}/status", new { status = "Resolvida", resposta = "Equipe repintou a faixa e religou o semáforo." });
        Assert.Equal(StatusOcorrencia.Resolvida, (await resolvida.LerAsync<OcorrenciaResponse>()).Status);
    }

    [Fact]
    public async Task CidadaoExcluiOcorrenciaAberta_Retorna204()
    {
        var cidadao = await api.NovoCidadaoAsync();
        var criada = await (await cidadao.SendAsync(await NovaOcorrenciaAsync(Guid.NewGuid().ToString()))).LerAsync<OcorrenciaResponse>();

        var exclusao = await cidadao.DeleteAsync($"/api/ocorrencias/{criada.Id}");

        Assert.Equal(HttpStatusCode.NoContent, exclusao.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cidadao.GetAsync($"/api/ocorrencias/{criada.Id}")).StatusCode);
    }

    [Fact]
    public async Task TravessiaAcessivel_CriaEDepoisDevolveAVigente()
    {
        var cidadao = await api.NovoCidadaoAsync();
        var cruzamento = await api.CruzamentoSeedAsync("SPO-002");

        HttpRequestMessage Pedido() => new HttpRequestMessage(HttpMethod.Post, $"/api/cruzamentos/{cruzamento.Id}/travessia-acessivel")
        {
            Content = JsonContent.Create(new { motivo = "MobilidadeReduzida" })
        }.ComIdempotencia(Guid.NewGuid().ToString());

        var primeira = await cidadao.SendAsync(Pedido());
        var segunda = await cidadao.SendAsync(Pedido());

        Assert.Equal(HttpStatusCode.Created, primeira.StatusCode);
        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);
        var solicitacao = await primeira.LerAsync<SolicitacaoTravessiaResponse>();
        Assert.True(solicitacao.TempoTravessiaSegundos > solicitacao.TempoTravessiaPadraoSegundos);
    }
}
