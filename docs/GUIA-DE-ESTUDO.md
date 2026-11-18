# 📘 Guia de estudo — como entender e defender a API .NET da MoveSync

A apresentação vale **30% da nota** e o professor vai perguntar sobre **estrutura**, **critérios de design** e **como os componentes do .NET funcionam**. Este guia explica cada parte do código com as palavras que vocês podem usar na defesa. Leiam com o projeto aberto ao lado.

---

## Parte 1 — A ideia em 30 segundos (decorem)

> "Semáforos de tempo fixo dão o mesmo verde para uma avenida lotada e para uma rua vazia. A MoveSync usa sensores baratos (ESP32 ou câmera) para medir quantos carros chegam em cada via e recalcula os tempos a cada 30 segundos pelo método de Webster. O cidadão vê no app quanto tempo vai esperar, pessoas com mobilidade reduzida pedem mais tempo para atravessar, ambulâncias ganham corredor verde e a prefeitura mede o impacto em minutos e CO₂ economizados. Se um sensor falha, o cruzamento volta sozinho para um tempo fixo seguro."

---

## Parte 2 — Mapa do código (onde fica cada coisa)

Pensem no projeto como uma cebola: quanto mais para dentro, menos dependências.

| Camada | Pergunta que ela responde | Exemplo |
|---|---|---|
| **Domain** | "Quais são as regras do trânsito?" | `CalculadoraPlanoSemaforico` decide os tempos; `Ocorrencia` sabe que semáforo apagado é prioridade crítica |
| **Application** | "O que o sistema faz para o usuário?" | `TelemetriaService.RegistrarAsync` recebe a leitura, evita duplicata e salva |
| **Infrastructure** | "Como isso é guardado/enviado?" | `MoveSyncDbContext` (EF Core), `HashSenhaPbkdf2`, `NotificadorNucleoHttp` |
| **Api** | "Como isso chega pela internet?" | Endpoints, JWT, rate limiting, idempotência, Swagger, health checks |

**Pergunta provável:** *"Por que separar em 4 projetos?"*
**Resposta:** "Para que as regras de negócio não dependam de banco nem de HTTP. Assim testamos o motor semafórico em milissegundos, sem banco, e podemos trocar o Oracle por outro banco mexendo só na Infrastructure. É a Clean Architecture: as dependências apontam para dentro, para o domínio."

---

## Parte 3 — Os 6 requisitos, explicados

### 3.1 Minimal API, validação, códigos HTTP e Swagger (20 pts)

**O que é Minimal API:** em vez de Controllers com classes e atributos, cada endpoint é uma linha `app.MapGet(...)` / `MapPost(...)` com uma função (lambda). É mais leve e mais rápido de iniciar.

Abram `Endpoints/CruzamentoEndpoints.cs`:
```csharp
var grupo = app.MapGroup("/api/cruzamentos").WithTags("Cruzamentos");
grupo.MapPost("", async (CriarCruzamentoRequest requisicao, CruzamentoService servico, CancellationToken ct) => { ... })
     .RequireAuthorization(Politicas.SomenteGestor)
     .ProducesValidationProblem();
```
- `MapGroup` agrupa rotas com o mesmo prefixo.
- Os **parâmetros da lambda são resolvidos automaticamente**: `CriarCruzamentoRequest` vem do corpo JSON, `CruzamentoService` vem da **injeção de dependência**, `Guid id` vem da rota, `CancellationToken` é cancelado se o cliente desistir.
- `TypedResults.Created(...)` devolve 201 com o cabeçalho `Location`; `TypedResults.NoContent()` devolve 204.

**Validação:** os DTOs em `Application/Contratos/Requisicoes.cs` têm `[Required]`, `[StringLength]`, `[Range]`... A linha `builder.Services.AddValidation()` (novidade do .NET 10) faz o ASP.NET validar **antes** de chamar o endpoint e devolver **400** com o erro de cada campo. Usamos `int?` com `[Required]` para detectar quando o campo nem foi enviado (um `int` normal viraria 0).

**Erros de negócio:** o serviço lança `RegraNegocioException` e o `TratadorExcecoesGlobal` converte em ProblemDetails:
| Exceção | HTTP |
|---|---|
| `RegraNegocioException` (Validacao) | 422 |
| `RegraNegocioException` (Conflito) | 409 |
| `RegraNegocioException` (AcessoNegado) | 403 |
| `RecursoNaoEncontradoException` | 404 |
| `CredenciaisInvalidasException` | 401 |

**Diferença 400 × 422 (o professor adora):** 400 = o formato está errado (faltou campo, texto curto). 422 = o formato está certo, mas viola uma regra (leitura com data no futuro, resolver ocorrência sem resposta).

**OpenAPI/Swagger:** `AddOpenApi()` gera o documento `/openapi/v1.json` a partir dos próprios endpoints (`WithSummary`, `WithDescription`, `Produces...`). Os *transformers* em `DocumentacaoOpenApi.cs` adicionam o esquema Bearer e colocam o cadeado só nos endpoints que exigem login. O Swagger UI (`/swagger`) só desenha esse documento.

### 3.2 JWT e autorização (20 pts)

**Fluxo:**
1. `POST /api/auth/login` → o serviço confere a senha (hash PBKDF2) e o `GeradorTokenJwt` cria o token.
2. O token tem 3 partes `cabeçalho.dados.assinatura`. Os dados (claims) são: `sub` (id), `name`, `email`, `role` (perfil), `exp` (expiração). A assinatura é HMAC-SHA256 com a chave secreta.
3. O app envia `Authorization: Bearer <token>` em cada requisição.
4. O middleware `UseAuthentication()` valida assinatura, emissor, audiência e expiração. Se algo falhar → **401**.
5. `UseAuthorization()` confere a política do endpoint (ex.: `SomenteGestor`). Perfil errado → **403**.

**Perguntas prováveis:**
- *"Onde fica a chave?"* — Variável de ambiente `Jwt__Chave`. Nunca no código. Em desenvolvimento, se não houver chave, geramos uma aleatória em memória.
- *"Por que o sensor tem token?"* — Para que só dispositivos cadastrados enviem telemetria, e cada sensor só envie dados **dele mesmo** (o id vem do token, não do corpo).
- *"Como a senha é guardada?"* — Nunca em texto: PBKDF2 com SHA-512, salt aleatório e 100 mil iterações (PasswordHasher do ASP.NET Identity).
- *"Por que a mensagem de login errado é genérica?"* — Para não revelar se o e-mail existe (evita enumeração de usuários).
- *"Alguém pode se cadastrar como gestor?"* — Não. O registro sempre cria Cidadão; gestores são criados pela prefeitura (seed).

### 3.3 Idempotência (15 pts)

**Conceito:** uma operação é idempotente quando repeti-la produz o mesmo efeito que executá-la uma vez. GET, PUT e DELETE já são; **POST não** — por isso protegemos os POSTs importantes.

**Por que importa aqui:** o ESP32 em 3G instável reenvia leituras; o cidadão toca duas vezes no botão "enviar ocorrência". Sem idempotência teríamos dados duplicados e planos errados.

**Três mecanismos (mostrem os três!):**
1. **Middleware `Idempotency-Key`** (`Api/Infraestrutura/Idempotencia.cs`) — usado em ocorrências, travessia acessível e emergências:
   - O cliente manda um UUID no cabeçalho `Idempotency-Key`.
   - 1ª vez: a API *reserva* a chave no banco, executa e grava status + corpo da resposta.
   - Repetição com o mesmo corpo: devolve a resposta gravada + `Idempotency-Replayed: true` **sem executar de novo**.
   - Mesma chave com corpo diferente: **422**. Chave ainda processando: **409**. Sem chave: **400**.
   - A chave é isolada por usuário + método + rota (a chave do João não colide com a da Maria) e expira em 24 h.
2. **Chave natural na telemetria** — cada leitura tem um `leituraId` gerado no dispositivo; o índice único `(SENSOR_ID, LEITURA_EXTERNA_ID)` no banco garante que ela entra uma vez. Reenvio → **200** com a mesma leitura (em vez de 201).
3. **Idempotência por regra de negócio** — pedir travessia acessível de novo devolve a solicitação vigente; recalcular o plano com os mesmos dados não cria registro novo.

**Pergunta provável:** *"E se duas requisições iguais chegarem ao mesmo tempo?"* — "A reserva usa um índice único no banco; a segunda recebe 409 'em processamento'. Na telemetria, se as duas tentarem gravar, o índice único barra a segunda e devolvemos a leitura que venceu."

### 3.4 Rate limiting (15 pts)

Configurado em `LimitacaoRequisicoes.cs` com o middleware nativo `AddRateLimiter` / `UseRateLimiter`.

| Política | Algoritmo | Chave | Limite padrão | Por quê |
|---|---|---|---|---|
| `autenticacao` | Janela fixa | IP | 10/min | Força bruta no login |
| `publico` | Janela deslizante | IP | 60/min | Consulta de status sem login |
| `usuario` | Token bucket | usuário (`sub`) | 120/min | Uso normal do app, permite rajadas |
| `telemetria` | Janela fixa | **sensor** | 30/min | Um ESP32 com defeito não derruba os demais |

**Como explicar os algoritmos:**
- **Janela fixa:** contador zera a cada minuto cheio.
- **Janela deslizante:** a janela é dividida em segmentos e "anda", evitando o pico na virada do minuto.
- **Token bucket:** um balde com N fichas que se recarrega; cada requisição gasta uma ficha.

Ao estourar: **429** em ProblemDetails com `Retry-After` (segundos para tentar de novo). Os limites ficam no `appsettings.json` (seção `LimiteRequisicoes`) — dá para ajustar sem recompilar.

**Detalhe de nuvem:** no Azure a API fica atrás de um proxy; o `UseForwardedHeaders()` lê o IP real do cliente em `X-Forwarded-For`, senão todos os usuários pareceriam o mesmo IP.

### 3.5 Testes com xUnit (20 pts)

- **Unitários (101 casos)** — `tests/MoveSync.UnitTests`. Testam o domínio puro (motor semafórico, máquina de estados da ocorrência, validações) e os casos de uso com repositórios **em memória** (pasta `Fakes`). Padrão **AAA**: *Arrange* (prepara), *Act* (executa), *Assert* (confere). `[Fact]` = um caso; `[Theory]` + `[InlineData]` = o mesmo teste com vários dados.
- **Integração (42 casos)** — `tests/MoveSync.IntegrationTests`. O `WebApplicationFactory<Program>` sobe a **API inteira em memória** (o mesmo `Program.cs`), com um SQLite temporário, e os testes fazem requisições HTTP de verdade: login, CRUD, 401/403, idempotência, 429, health checks, OpenAPI.

**Pergunta provável:** *"Qual a diferença?"* — "O unitário testa uma regra isolada, rápido e sem banco. O de integração testa a API como o app vê: rotas, middlewares, autenticação, banco e JSON juntos."

### 3.6 Health checks (10 pts)

| Rota | O que verifica | Uso |
|---|---|---|
| `/health/live` | A API está viva | Azure reinicia o container se falhar |
| `/health/ready` | Banco acessível | Só recebe tráfego quando pronta |
| `/health` | Tudo: API, **banco**, **sensores IoT**, **serviço externo (núcleo Java)** | Monitoramento e demonstração |

- `BancoDadosHealthCheck` faz uma consulta real (não só abre conexão) e mede a latência.
- `SensoresHealthCheck` lista sensores sem dados recentes → **Degraded** (e não Unhealthy), porque o cruzamento entra em contingência e continua funcionando. **Ótimo ponto para a defesa: degradação graciosa.**
- `NucleoJavaHealthCheck` chama o `/actuator/health` do Spring Boot; se a URL não estiver configurada, informa "integração opcional".

Status HTTP: Healthy/Degraded → 200; Unhealthy → 503.

---

## Parte 4 — O motor semafórico (o "cérebro")

Arquivo: `Domain/Servicos/CalculadoraPlanoSemaforico.cs`. Ordem de decisão:

1. **Emergência ativa?** → via da ambulância 90 s, demais 15 s.
2. **Gestor fixou tempo fixo?** → 30 s por via.
3. **Alguma via sem dados nos últimos 5 min?** → **Contingência** (tempo fixo seguro) + evento para o núcleo Java.
4. **Senão → Adaptativo (Webster).**

Webster em palavras simples:
- Para cada via calculamos a **ocupação** `y = carros que chegam ÷ carros que a via consegue escoar`.
- Somamos tudo (`Y`). Quanto mais perto de 1, mais congestionado.
- O **ciclo** ideal cresce quando `Y` cresce: `C = (1,5·L + 5)/(1 − Y)`.
- O **verde** de cada via é proporcional à sua ocupação: quem tem mais demanda ganha mais verde.
- Tudo limitado a 15–90 s por via. **Amarelo, vermelho geral e travessia de pedestres nunca são reduzidos.**

Exemplo real (é exatamente o cenário do teste de integração `FluxoPrincipal`): *"Na Faria Lima chegaram 140 carros em 5 min com fila de 25; nas Rebouças, 15 e 10. O motor deu **62 s** de verde para a Faria Lima e **15 s** para cada Rebouças, em vez de 30 s para todas. Com essa demanda, o atraso médio cai de **52 s para 30 s por veículo (−42%)**, cerca de **15 horas de espera e 28 kg de CO₂ evitados por hora** nesse cruzamento."* (os números variam um pouco com os dados enviados; confiram no `GET /indicadores` antes de apresentar).

**Background worker:** `RecalculoPlanosWorker` roda a cada 30 s e recalcula todos os cruzamentos — é isso que faz o sistema **agir** sobre o trânsito, não só monitorar. Um plano novo só é gravado quando muda de verdade (tolerância de 2 s), o que mantém o histórico limpo.

---

## Parte 5 — Componentes do .NET que vocês devem saber explicar (10 pts)

| Componente | Onde | Para que serve |
|---|---|---|
| `WebApplication.CreateBuilder` | `Program.cs` | Monta configuração (appsettings + variáveis de ambiente), logging e DI |
| Injeção de dependência (`AddScoped`, `AddSingleton`) | `Program.cs`, `InjecaoDependencias.cs` | O framework cria e entrega os objetos. *Scoped* = um por requisição (DbContext); *Singleton* = um para a aplicação (calculadora) |
| Middleware pipeline | `Program.cs` (`app.Use...`) | Cada requisição passa em ordem: erros → autenticação → rate limit → autorização → idempotência → endpoint |
| `IExceptionHandler` + ProblemDetails | `TratadorExcecoes.cs` | Um único lugar transforma exceções em respostas HTTP padronizadas |
| EF Core (`DbContext`, `DbSet`, Fluent API) | `Infrastructure/Persistencia` | ORM: classes C# ↔ tabelas Oracle. `IEntityTypeConfiguration` define tabela, tamanhos, índices e chaves |
| `AsNoTracking()` | Repositórios | Consultas só de leitura ficam mais rápidas (o EF não monitora mudanças) |
| `IOptions` / seções de configuração | `OpcoesJwt`, `OpcoesBancoDados`... | Configuração tipada; `__` em variável de ambiente = `:` no JSON |
| `BackgroundService` + `PeriodicTimer` | `Workers.cs` | Tarefas recorrentes dentro da própria API |
| `IHttpClientFactory` | `NotificadorNucleoHttp` | Cria HttpClient corretamente (sem esgotar conexões), com timeout |
| `TimeProvider` | Serviços | Relógio injetável — nos testes usamos um relógio fixo |
| Rate limiting / Health checks / Auth | Pacotes nativos do ASP.NET Core | Sem bibliotecas de terceiros |

---

## Parte 6 — Roteiro da demonstração (≈ 8 min)

1. **Swagger** aberto na nuvem. Mostrar `/health` (banco Healthy, sensores Degraded = contingência). *(1 min)*
2. **Status público** de SPO-002 sem login → modo Contingência e aviso. *(30 s)*
3. Rodar o **simulador** (`--perfil pico`) por ~30 s. Mostrar no console o reenvio com `Idempotency-Replayed`. *(1 min)*
4. Login **gestor** → Authorize → `POST /planos/recalcular` → modo **Adaptativo**, ler a **justificativa**. `GET /indicadores` → redução de atraso e CO₂. *(1,5 min)*
   Em seguida `GET /api/painel/impacto` → ler a **interpretação** (horas e CO₂ evitados por mês, valor do tempo, benefício/custo). Mostrar a simulação `?horasReferenciaPorDia=6`. *(+30 s)*
5. Login **cidadã** → `POST /ocorrencias` com `Idempotency-Key`; repetir → mesma resposta; mudar o corpo → 422. *(1,5 min)*
6. Cidadã chama `/api/painel/resumo` → **403**. Sem token → **401**. *(30 s)*
7. **Travessia acessível** → status mostra travessia ampliada. **Emergência** → plano Emergência; encerrar. *(1 min)*
8. Login repetido com senha errada até o **429** + `Retry-After`. *(30 s)*
9. Terminal: `dotnet test` passando. *(30 s)*

---

## Parte 6.1 — Como defender o modelo de negócio (decorem os números)

Documento completo: [`MODELO-DE-NEGOCIO.md`](MODELO-DE-NEGOCIO.md).

- **Quem paga?** A **prefeitura** (B2G). O cidadão usa de graça.
- **Quanto?** Hipótese do grupo: **R$ 4.800 de implantação** e **R$ 590/mês por cruzamento**. Sistemas adaptativos tradicionais custam **US$ 8 mil a US$ 35 mil por cruzamento** (Departamento de Transportes dos EUA).
- **Por que a prefeitura aceitaria?** Porque a própria API prova o retorno: `GET /api/painel/impacto` mede o ganho com a telemetria real e calcula a **relação benefício/custo**.
- **O número para guardar:** com as premissas padrão, a assinatura se paga se o cruzamento evitar **~8 minutos de espera somados por hora** (0,127 h). Qualquer cruzamento com demanda desequilibrada passa disso com folga.
- **Benefício social, não caixa.** A relação benefício/custo mede tempo devolvido à população (R$ 21,15 por hora, a partir da renda média do IBGE). É assim que se avalia investimento público em mobilidade; a prefeitura não lucra, ela justifica o gasto.
- **Por que o número da demo é tão alto?** Porque a demo usa um cenário de pico projetado por 10 h/dia. Na apresentação, usem a tabela de sensibilidade (0,5 h/h → B/C 3,9; 1 h/h → 7,9) e digam que o **piloto gratuito** existe para medir o ganho real.
- **Onde está no código?** Regra de cálculo no domínio (`Domain/Servicos/CalculadoraImpactoEconomico.cs`, com 12 casos de teste unitário), caso de uso em `Application/Servicos/ImpactoService.cs`, premissas no `appsettings.json` (seção `ModeloNegocio`) e validadas na inicialização.

---

## Parte 7 — Perguntas difíceis e respostas

- **"O que acontece se o banco cair?"** — `/health` vai para 503 (Unhealthy), as requisições recebem 500 genérico sem vazar detalhes e o erro vai para o log. O worker não morre: tenta de novo no próximo ciclo.
- **"E se o sensor mandar dados falsos?"** — O domínio rejeita valores fora da faixa (0–500 veículos, fila 0–200, data no futuro ou com mais de 24 h) com 422. O sensor só envia para a via dele (identidade vem do JWT).
- **"Como vocês medem o impacto?"** — `GET /indicadores` compara, para a mesma demanda real, o atraso médio no tempo fixo e no adaptativo (Webster), e converte em horas de espera e kg de CO₂ evitados por hora.
- **"Isso é viável financeiramente?"** — "A prefeitura paga R$ 590 por cruzamento ao mês; a margem estimada é de cerca de 59% depois de nuvem, conectividade e suporte. Para a prefeitura, o relatório de impacto mostra o benefício/custo com os dados da própria cidade, e o ponto de equilíbrio é de ~8 minutos de espera evitados por hora."
- **"Quem é o concorrente?"** — SCOOT e SCATS, que custam dezenas de milhares de dólares por cruzamento e miram grandes capitais. Nosso alvo são as cerca de 270 cidades entre 100 mil e 500 mil habitantes.
- **"Por que Oracle 19c?"** — É o banco da FIAP e da disciplina de BD. O EF Core 10 assume Oracle 23; configuramos compatibilidade 19 para gerar `NUMBER(1)` em vez de `BOOLEAN`.
- **"Por que guardar a idempotência no banco e não em memória?"** — Na nuvem pode haver mais de uma instância da API e reinícios; em memória a proteção se perderia.
- **"Como isso se relaciona com o tema CitizenTech?"** — Benefício concreto e mensurável ao cidadão (espera menor, travessia acessível, canal de ocorrências, prioridade a emergências), baixo custo para cidades pequenas e funcionamento seguro mesmo sem conectividade ideal (contingência e envio em lote).
- **"Se o professor pedir uma mudança de requisito?"** (ex.: *"prioridade para ônibus"*) — "Adicionaríamos um tipo de prioridade no domínio, um acionamento parecido com o de emergência e um peso maior para a via com ônibus no cálculo da demanda. Como as regras estão no Domain, a mudança fica concentrada lá, com novos testes unitários."
