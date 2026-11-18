using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;
using MoveSync.Api.Endpoints;
using MoveSync.Api.Infraestrutura;
using MoveSync.Application.Servicos;
using MoveSync.Domain.Servicos;
using MoveSync.Infrastructure;
using MoveSync.Infrastructure.Persistencia;

// =====================================================================================
//  MOVESYNC API — composição da aplicação (ordem importa!)
//  1) Registro de serviços (injeção de dependência)
//  2) Pipeline de middlewares
//  3) Mapeamento dos endpoints
// =====================================================================================

var builder = WebApplication.CreateBuilder(args);
var configuracao = builder.Configuration;

// ---------- Domínio: parâmetros de engenharia de tráfego e motor de cálculo ----------
// As seções de configuração são lidas na criação do serviço (depois do Build), não aqui: assim os valores
// de variáveis de ambiente e dos testes de integração (WebApplicationFactory) sempre prevalecem.
builder.Services.AddSingleton(sp => LerSecao<ParametrosSemaforicos>(sp, "Semaforos:Parametros"));
builder.Services.AddSingleton<CalculadoraPlanoSemaforico>();
builder.Services.AddSingleton<AnalisadorDesempenho>();
builder.Services.AddSingleton<CalculadoraImpactoEconomico>();
builder.Services.AddSingleton(sp =>
{
    var premissas = LerSecao<PremissasEconomicas>(sp, "ModeloNegocio");
    premissas.Validar();
    return premissas;
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(sp => LerSecao<OpcoesSemaforos>(sp, OpcoesSemaforos.Secao));

// ---------- Infraestrutura: EF Core (Oracle/SQLite), repositórios, hash, integração ----------
builder.Services.AdicionarInfraestrutura(configuracao);

// ---------- Aplicação: casos de uso ----------
builder.Services.AddScoped<AutenticacaoService>();
builder.Services.AddScoped<CruzamentoService>();
builder.Services.AddScoped<SensorService>();
builder.Services.AddScoped<TelemetriaService>();
builder.Services.AddScoped<PlanoSemaforicoService>();
builder.Services.AddScoped<OcorrenciaService>();
builder.Services.AddScoped<PrioridadeService>();
builder.Services.AddScoped<PainelService>();
builder.Services.AddScoped<ImpactoService>();

// ---------- Segurança, proteção e qualidade da API ----------
builder.Services.AdicionarAutenticacaoJwt();
builder.Services.AdicionarLimitacaoRequisicoes(configuracao);
builder.Services.AdicionarVerificacoesSaude();
builder.Services.AddValidation();                       // valida DataAnnotations dos DTOs automaticamente (400)
builder.Services.AddProblemDetails();                   // erros no padrão RFC 9457
builder.Services.AddExceptionHandler<TratadorExcecoesGlobal>();
builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true); // JSON malformado → ProblemDetails
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi(o =>
{
    o.AddDocumentTransformer<DocumentoOpenApiTransformer>();
    o.AddOperationTransformer<SegurancaOperacaoTransformer>();
});

// App mobile em modo web (Expo) precisa de CORS; o app nativo não é afetado.
builder.Services.AddCors(o => o.AddPolicy("app", p => p
    .AllowAnyOrigin()
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders(ExtensoesIdempotencia.CabecalhoRepeticao, "Retry-After", "Location")));

// Atrás do proxy do Azure, o IP real do cliente vem em X-Forwarded-For (importante para o rate limiting por IP).
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

builder.Services.AddHostedService<RecalculoPlanosWorker>();
builder.Services.AddHostedService<LimpezaIdempotenciaWorker>();

var app = builder.Build();

// Falha rápida: valida na subida a configuração obrigatória (chave JWT, premissas, parâmetros),
// em vez de descobrir o erro só na primeira requisição.
_ = app.Services.GetRequiredService<OpcoesJwt>();
_ = app.Services.GetRequiredService<PremissasEconomicas>();
_ = app.Services.GetRequiredService<ParametrosSemaforicos>();

// Cria as tabelas (se necessário) e carrega dados de demonstração.
await InicializadorBanco.InicializarAsync(app.Services);

// ---------- Pipeline ----------
app.UseForwardedHeaders();
app.UseExceptionHandler();      // captura exceções e devolve ProblemDetails
app.UseStatusCodePages();       // 401/403/404 sem corpo também viram ProblemDetails
app.UseCors("app");

app.MapOpenApi();               // documento: /openapi/v1.json
app.UseSwaggerUI(o =>
{
    o.SwaggerEndpoint("/openapi/v1.json", "MoveSync API v1");
    o.RoutePrefix = "swagger";
    o.DocumentTitle = "MoveSync API";
});

app.UseRouting();
app.UseAuthentication();        // lê o JWT e preenche o usuário
app.UseRateLimiter();           // depois da autenticação: limites por usuário/sensor
app.UseAuthorization();         // aplica as políticas (perfis)
app.UseMiddleware<IdempotenciaMiddleware>();

// ---------- Endpoints ----------
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapearVerificacoesSaude();
app.MapearAutenticacao();
app.MapearCruzamentos();
app.MapearSensores();
app.MapearTelemetria();
app.MapearOcorrencias();
app.MapearEmergenciasEPainel();

await app.RunAsync();

static T LerSecao<T>(IServiceProvider sp, string secao) where T : new() =>
    sp.GetRequiredService<IConfiguration>().GetSection(secao).Get<T>() ?? new T();

/// <summary>Exposto para os testes de integração (WebApplicationFactory&lt;Program&gt;).</summary>
public partial class Program;
