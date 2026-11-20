# ✅ Checklist das rubricas — .NET e QA

Conferência item a item com o PDF da Global Solution 2026/2. **Legenda:** ✅ pronto no projeto · ⏳ depende de ação do grupo (conta, deploy, prints, RMs).

> Entrega: **19/11 até 23h55, no Portal do Aluno**. Cada disciplina é uma entrega separada, cada uma com seu `.zip` **e o `.txt` de identificação** (RM, nome completo e turma de todos).

---

## Advanced Business Development with .NET

### Entrega escrita (70%)

| # | Critério (pontos) | Status | Evidência |
|---|---|---|---|
| — | API em .NET consumida pelo app Mobile, com funcionalidade de valor real ao cidadão; ASP.NET Minimal API + EF Core | ✅ | Minimal API, EF Core 10 (Oracle/SQLite), CORS para o app; cidadão: status e espera estimada, travessia acessível, ocorrências (CRUD) |
| 01 | Minimal API RESTful com validações, códigos HTTP e OpenAPI/Swagger (20) | ✅ | **42 endpoints**, `AddValidation` + DataAnnotations com mensagens em português, ProblemDetails, 200/201/204/400/401/403/404/409/422/429/503, Swagger em `/swagger` · `Api/Endpoints/*`, `DocumentacaoOpenApi.cs` |
| 02 | JWT protegendo endpoints e validando tokens (20) | ✅ | Perfis Cidadão, Gestor e Sensor; 4 políticas; emissor, audiência, assinatura e expiração validados · `Api/Infraestrutura/Autenticacao.cs` |
| 03 | Idempotência (15) | ✅ | `Idempotency-Key` persistida no banco (repetição → mesma resposta + `Idempotency-Replayed`; corpo diferente → 422); chave natural na telemetria; idempotência de negócio na travessia e no recálculo · `Api/Infraestrutura/Idempotencia.cs` |
| 04 | Rate limiting com HTTP 429 (15) | ✅ | 4 políticas (por IP, por usuário, por sensor), 429 com `Retry-After` e ProblemDetails · `LimitacaoRequisicoes.cs` |
| 05 | Testes unitários e de integração com xUnit (20) | ✅ | **101 unitários** e **42 de integração** com `WebApplicationFactory`: **143/143 aprovados** no Visual Studio 2026 em 18/11/2026, com os pacotes reais do NuGet |
| 06 | Health checks: API, banco e serviços externos (10) | ✅ | `/health`, `/health/live`, `/health/ready`: API, banco (consulta real), sensores IoT e núcleo Java · `Infrastructure/Saude/HealthChecks.cs` |

**Funcionalidade de negócio extra:** `GET /api/painel/impacto` — relatório de impacto e benefício/custo para a prefeitura (o modelo de negócio dentro da API). Ver [`MODELO-DE-NEGOCIO.md`](MODELO-DE-NEGOCIO.md).

### O que tira pontos (.NET)

| Penalidade | Como evitamos | Status |
|---|---|---|
| Fora do prazo ou do Portal (−30) | Entregar até 19/11 23h55 | ⏳ |
| Arquivo corrompido, incompleto ou sem `.txt` (−100) | `ENTREGA-identificacao.txt` na raiz; **preencher os RMs** e testar abrir o `.zip` | ⏳ |
| Sem relação com CitizenTech (−100) | README seção 1: cidadão beneficiado, ODS, acessibilidade | ✅ |
| API não executa (−20) | `dotnet run` com SQLite sem configurar nada; deploy no Azure com um comando (`scripts/deploy-azure.ps1` ou `.sh`) | ✅ ⏳ deploy |
| Código desorganizado ou **credenciais expostas** (−20) | Camadas Domain/Application/Infrastructure/Api; chave JWT e conexão Oracle só por variável de ambiente; `.env` no `.gitignore`; `.env.example` sem valores reais | ✅ |

> **Decisão consciente sobre credenciais:** o `appsettings.Development.json` traz apenas as senhas dos **usuários de demonstração locais** (`Gestor@2026`, `Cidada@2026`), para o professor rodar a API e o Swagger sem configurar nada. Não há chave JWT, senha de banco ou segredo de nuvem no repositório. Se o professor perguntar, expliquem isso; em produção os mesmos valores vêm das variáveis `Seed__*`.

### Formato da entrega (.NET)

`.zip` com **o link do GitHub** e o **`.txt` de identificação** (o PDF pede o link, não o código no zip), mais o PowerPoint exportado do deck de slides da apresentação.

### Apresentação (30%)

| # | Critério (pontos) | Onde se preparar |
|---|---|---|
| 01 | Apresentar o funcionamento do sistema (20) | Guia, Parte 6 (roteiro de 8 min) |
| 02 | Perguntas sobre a estrutura do projeto (20) | Guia, Parte 2 (mapa do código) |
| 03 | Perguntas sobre descrições e critérios de design (20) | README seção 8 e Guia, Parte 7 |
| 04 | Fluxos principais (20) | Guia, Parte 6: plano adaptativo, idempotência, 401/403, travessia, emergência, 429, impacto |
| 05 | Componentes do .NET (10) | Guia, Parte 5 |
| 06 | Como a solução aborda o tema (10) | README seção 1 e Guia, Parte 6.1 (modelo de negócio) |

---

## Compliance, Quality Assurance & Tests

### Entrega escrita (70%)

| # | Critério (pontos) | Status | Evidência |
|---|---|---|---|
| 01 | Estrutura com Epic, Feature e PBI (20) | ✅ ⏳ | 5 Epics, 12 Features, **25 PBIs** (processo **Scrum**) · `qa/01-backlog-azure-boards.csv` · ⏳ importar no Azure DevOps |
| 02 | Descrição e critério de aceite por Feature (20) | ✅ | Toda Feature e todo PBI com descrição e critérios em BDD (Dado/quando/então) |
| 03 | Test Cases positivos e negativos por Feature (20) | ✅ | **40 casos** (24 positivos, 16 negativos); toda Feature tem os dois tipos; todo PBI tem ao menos um caso |
| 04 | Sumário dos Test Cases (20) | ✅ | Objetivo, Preparação, Inputs e Outputs em todos os casos |
| 05 | Passos dos Test Cases (20) | ✅ | Ação do testador + resposta esperada do sistema em cada passo |

### O que tira pontos (QA)

| Penalidade | Como evitamos | Status |
|---|---|---|
| Fora do prazo ou do Portal (−10) | Entregar até 19/11 23h55 | ⏳ |
| Arquivo corrompido, incompleto ou sem `.txt` (−10) | `.zip` com o PDF + `.txt` | ⏳ |
| Sem relação com CitizenTech (zero) | Backlog centrado no cidadão (Epics de cidadão, acessibilidade, emergência) | ✅ |
| **Não usar a ferramenta e os métodos da aula (zero)** | Tudo dentro do **Azure DevOps** (Boards + Test Plans), processo Scrum, Epic → Feature → PBI, casos com vínculo *Tested By* | ⏳ importar com `qa/fonte/azure_devops_importar.py --url-api ...` |

### Formato da entrega (QA)

`.zip` com **um PDF** (Plano de Projeto + Plano de Testes) + `.txt`. O modelo `qa/Modelo-PDF-Plano-de-Projeto-e-Testes.docx` (18 páginas) já tem um espaço rotulado para cada print:

- [ ] Print da tela **Backlogs**
- [ ] Print da descrição e dos critérios de aceite de **cada Feature e cada PBI**
- [ ] Print do **Boards** com a lista de Test Cases por Feature
- [ ] Print do **Sumário** e dos **Passos** de **cada** Test Case
- [ ] **Link do projeto** no Azure Boards, com o professor convidado como **ADMIN**

### Apresentação (30%)

| # | Critério (pontos) | Preparação |
|---|---|---|
| 01 | Operar o Azure DevOps (20) | Navegar Backlogs → Feature → PBI → casos vinculados |
| 02 | Perguntas sobre a estrutura do projeto (20) | Por que 5 Epics; como Feature e PBI se dividem |
| 03 | Perguntas sobre descrições e critérios (20) | Ler em voz alta um critério BDD e o caso que o comprova |
| 04 | **Executar os testes manuais no Test Plans** (20) | Casos sugeridos no `qa/README.md` (ex.: CT-F11-04, impacto para a prefeitura); **a API precisa estar no ar** |
| 05 | **Relatórios analíticos de testes** (20) | Depois de executar, abrir **Test Plans → Progress report** e **Runs**; registrar o resultado real de cada caso (se algum falhar de verdade, marcar *Failed* e vincular um bug) |

---

## ⚠️ Pendências do grupo (ordem sugerida)

1. ~~Rodar `dotnet test`~~ ✅ Feito em 18/11/2026 no Visual Studio: compilação 6/6 e 143/143 testes aprovados. Dependências transitivas com vulnerabilidade conhecida foram fixadas em versões corrigidas no `Directory.Packages.props` (o NuGet Audit não acusa mais nada).
2. Criar o repositório no **GitHub** e subir o código (commits pequenos e com mensagens claras).
3. Publicar a API no **Azure** com `scripts/deploy-azure.ps1` (Windows) ou `scripts/deploy-azure.sh` e preencher o link do Swagger no README, no `.txt` e no último slide.
4. Importar backlog e casos no **Azure DevOps** com `--url-api` (os passos já saem com a URL real), convidar o professor como admin, executar os testes no Test Plans e tirar os prints.
5. Preencher **RMs, nomes e turma** no `ENTREGA-identificacao.txt` e no README.
6. Montar os dois `.zip` e **abrir cada um** antes de enviar.
