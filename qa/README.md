# 🧪 MoveSync — Pacote de QA (Compliance, Quality Assurance & Tests)

Tudo o que a disciplina pede, pronto para colocar no **Azure DevOps**: backlog Scrum (Epics → Features → PBIs) com descrição e critérios de aceite, e **40 casos de teste funcionais caixa-preta** (24 positivos e 16 negativos), em versão enxuta: cada Feature tem casos positivos e negativos e cada PBI tem ao menos um caso com Sumário (Objetivo, Preparação, Inputs, Outputs) e Passos.

| Arquivo | Para que serve |
|---|---|
| `01-backlog-azure-boards.csv` | Importar o backlog completo no **Azure Boards** (5 Epics, 12 Features, 25 PBIs) |
| `02-casos-de-teste/F01.csv … F12.csv` | Importar os casos de cada Feature na **suíte** correspondente do **Azure Test Plans** |
| `02-casos-de-teste/TODOS.csv` | Todos os 40 casos em um único arquivo |
| `Plano-QA-MoveSync.xlsx` | Backlog, sumário e passos de todos os casos, rastreabilidade e planilha de execução |
| `Modelo-PDF-Plano-de-Projeto-e-Testes.docx` | Modelo compacto do PDF (18 páginas, 2 prints por linha) com **um espaço rotulado para cada print exigido** |
| `PLANO-DE-TESTES.md` | Todo o conteúdo em texto, para revisão da equipe |
| `fonte/dados_qa.py` | **Fonte única** do conteúdo (edite aqui) |
| `fonte/dados_qa_versao_completa.py` | Versão ampliada (35 PBIs, 99 casos), só como referência caso queiram mais casos |
| `fonte/gerar_artefatos_qa.py` | Regenera CSVs, Excel e Markdown após editar a fonte |
| `fonte/azure_devops_importar.py` | (Opcional) Cria **tudo** no Azure DevOps via API em ~5 minutos |

---

## 1. O que a disciplina avalia (e onde está cada coisa)

**Entrega (70%)** — PDF com prints do Azure DevOps:

| Critério (20 pts cada) | Onde está pronto |
|---|---|
| Estrutura com Epic, Feature e Product Backlog Item | `01-backlog-azure-boards.csv` |
| Descrição e critério de aceite por Feature (e por PBI) | colunas *Description* e *Acceptance Criteria* do CSV |
| Lista de Test Cases positivos e negativos por Feature | `02-casos-de-teste/F01…F12.csv` + vínculo *Tested By* |
| Sumário dos Test Cases (Objetivo, Preparação, Inputs, Outputs) | aba *Summary* (via script) ou aba *Casos de Teste - Sumário* do Excel |
| Passos dos Test Cases (ação do testador → resposta do sistema) | coluna *Step Action / Step Expected* dos CSVs |

**Apresentação (30%)** — operar o Azure DevOps ao vivo, responder sobre estrutura e critérios, **executar testes no Test Plans** e mostrar **relatórios analíticos** (seções 7 a 9).

> ⚠️ "Não utilização da ferramenta e métodos usados em aula" = **nota zero**. Tudo precisa estar dentro do Azure DevOps; o Excel é apoio.

---

## 2. Criar o projeto no Azure DevOps

1. Acesse https://dev.azure.com e entre com a conta da FIAP (ou crie uma organização).
2. **New project** → nome `MoveSync` → **Private** → *Advanced* → Version control **Git** → Work item process **Scrum** → *Create*.
   - O processo precisa ser **Scrum**: é ele que tem o tipo *Product Backlog Item*.
   - Dá para usar o mesmo projeto da disciplina de DevOps (Boards/Repos/Pipelines convivem sem problema).
3. Exiba o nível de Epics: **Project settings → Boards → Team configuration → Backlogs** → marque **Epics**.
4. **Test Plans**: se o menu *Test Plans* não aparecer ou pedir licença, ative o **trial gratuito de 30 dias do Azure Test Plans** (Organization settings → Billing / Users → *Basic + Test Plans*). Criar planos e suítes exige essa licença.

---

## 3. Caminho A (recomendado) — script cria tudo via API

Cria backlog, Test Plan, 12 suítes, 40 casos com **Sumário completo** e os vínculos *Tested By* — exatamente o que os prints exigem.

1. Crie um **Personal Access Token**: ícone do usuário → *Personal access tokens* → *New Token* → escopos **Work Items (Read & write)** e **Test Management (Read & write)** → copie o token.
2. No terminal, na raiz do repositório:
   ```bash
   pip install requests
   # 1º simule (não cria nada):
   python qa/fonte/azure_devops_importar.py --org https://dev.azure.com/SUA-ORG --projeto "MoveSync" --simular
   # depois crie de verdade, já com a URL da API publicada nos passos:
   python qa/fonte/azure_devops_importar.py --org https://dev.azure.com/SUA-ORG --projeto "MoveSync" --pat SEU_PAT --url-api https://SUA-API.azurewebsites.net
   ```
   Com `--url-api`, todo `[URL da API]` dos passos vira a URL real, e o testador só clica.
3. Confira em *Boards → Backlogs* (nível Epics) e em *Test Plans*.
> Rodar o script duas vezes **duplica** os itens. Se algo der errado no meio, apague os itens criados (consulta + seleção em massa → *Delete*) antes de rodar de novo.

## 4. Caminho B — importação por CSV (sem programar)

Antes de importar, gere os CSVs já com a URL da API publicada: `python qa/fonte/gerar_artefatos_qa.py --url-api https://SUA-API.azurewebsites.net`.

### 4.1 Backlog
1. **Boards → Work items → Import Work Items** → selecione `01-backlog-azure-boards.csv` → *Upload*.
2. Confira a prévia (Epics, Features e PBIs indentados) → **Save items**.
3. Em *Boards → Backlogs*, escolha o nível **Epics** e expanda tudo: a hierarquia deve aparecer como Epic > Feature > PBI.
   - Se algum item vier sem pai, arraste-o para baixo do pai correto no backlog (ou use *Change parent*).

### 4.2 Casos de teste
1. **Test Plans → New Test Plan** → nome `Plano de Testes - MoveSync`.
2. No plano, crie **12 suítes estáticas** (*New Suite → Static suite*) com os nomes das Features (`F01 - Cadastro e autenticação de pessoas`, …).
3. Em cada suíte → **⋯ → Import test cases from CSV/XLSX** → selecione o CSV da Feature (ex.: `F01.csv`) → confira o mapeamento das colunas → **Import**.
4. **Sumário:** o CSV não preenche a aba *Summary*. Abra cada caso e cole no campo *Description* o Objetivo, a Preparação, os Inputs e os Outputs da aba **"Casos de Teste - Sumário"** do Excel (por isso o Caminho A é recomendado).
5. **Vínculo com a Feature:** abra a Feature → aba *Links* (ou *Related Work*) → *Add link → Existing item* → tipo **Tested By** → informe os IDs dos casos da Feature.

---

## 5. Tela "Test Cases por Feature" (print exigido)

Crie uma consulta que mostra cada Feature com seus casos de teste:

1. **Boards → Queries → New query**.
2. *Type of query*: **Work items and direct links**.
3. Filtro de cima: `Work Item Type = Feature`. Filtro de baixo (itens vinculados): `Work Item Type = Test Case`. *Type of links*: **Tested By**.
4. *Column options*: adicione *Title*, *State*, *Tags*. Salve como `Test Cases por Feature`.
5. Execute: cada Feature aparece com a lista de Test Cases logo abaixo — tire um print por Feature (ou do conjunto).

Alternativa: abrir cada Feature e mostrar a aba *Links*/*Related Work*.

---

## 6. Prints para o PDF (use o modelo Word)

Abra `Modelo-PDF-Plano-de-Projeto-e-Testes.docx`: cada espaço cinza diz qual print colar.

| Seção do PDF | Print |
|---|---|
| Capa | Nome do grupo, integrantes (RM, nome, turma) e **link do projeto no Azure Boards** |
| 1.1 | **Boards → Backlogs** (nível Epics, hierarquia expandida) |
| 1.x | Cada **Feature** e cada **PBI** abertos mostrando *Description* e *Acceptance Criteria* |
| 2.1 | **Boards**: Test Cases por Feature (consulta da seção 5) |
| 2.2 | **Test Plans** com as 12 suítes |
| 2.3 | De **cada** caso: aba **Summary** e aba **Steps** |

Cada linha do modelo tem 2 prints lado a lado (ex.: Sumário | Passos do mesmo caso). Depois: *Arquivo → Salvar como → PDF*. Dica: use *Win + Shift + S* para recortes rápidos e cole direto no Word.

**Entrega (formato do PDF da GS):** um `.zip` com o **PDF** + um **.txt** com RM, nome completo e turma de cada integrante. E convide o professor:
- *Organization settings → Users → Add users* → e-mail do professor → Access level **Basic** (ou Basic + Test Plans).
- *Project settings → Permissions → Project Administrators → Members → Add* → professor.

---

## 7. Executar os testes (apresentação — 20 pts)

1. Garanta que a API esteja publicada e com os usuários de teste (no Azure, use nas variáveis `Seed__GestorSenha=Gestor@2026`, `Seed__CidadaoSenha=Cidada@2026` e `Seed__SegredoSensores=sensor-demo-local-2026` para os casos funcionarem como escritos; troque `[URL da API]` pela URL real).
2. **Test Plans → Plano → suíte → aba Execute** → selecione os casos → **Run → Run for web application**.
3. O **Test Runner** abre ao lado do navegador: siga cada passo no Swagger e marque ✔ (passou) ou ✘ (falhou) em cada passo; anexe um print se quiser.
4. Ao terminar: *Save and close*. Se algo falhar: **Create bug** direto do Test Runner (o bug já nasce vinculado ao caso).
5. Sugestão de casos para a demonstração ao vivo (rápidos e visuais): CT-F01-03 (403), CT-F05-02 (idempotência), CT-F10-02 (Idempotency-Key), CT-F06-01 (plano adaptativo), CT-F11-04 (relatório de impacto e benefício/custo para a prefeitura), CT-F12-02 (429).

## 8. Relatórios analíticos (apresentação — 20 pts)

- **Test Plans → Progress report**: percentual de casos passados/falhados/não executados por plano e suíte.
- **Plano → aba Chart → New chart** (*Test case results* / *Outcome* por *Suite*): gráfico de pizza ou barras.
- **Test Plans → Runs**: resumo de cada execução (duração, resultado, testador).
- **Dashboard de Qualidade**: *Overview → Dashboards → New dashboard* → widgets **Chart for Test Plans**, **Test Results Trend (Advanced)** e um **Query Tile** com a consulta "Test Cases por Feature".

## 9. Perguntas prováveis do professor

- **Diferença entre Epic, Feature e PBI?** Epic = grande objetivo de negócio (ex.: Serviços ao Cidadão); Feature = funcionalidade entregável que agrega valor (ex.: Travessia acessível); PBI = item de trabalho que cabe em uma sprint, escrito como história de usuário.
- **Por que "Dado/Quando/Então"?** Critérios objetivos e testáveis: cada critério vira, no mínimo, um caso de teste (rastreabilidade).
- **O que é teste caixa-preta?** Testa o comportamento pelas entradas e saídas (HTTP, JSON), sem olhar o código.
- **Positivo × negativo?** Positivo confirma que o fluxo correto funciona; negativo confirma que entradas inválidas, acessos indevidos e regras violadas são **recusados** corretamente (400, 401, 403, 404, 409, 422, 429).
- **Para que serve a Preparação?** Garante que qualquer pessoa reproduza o teste nas mesmas condições (dados, usuários, estado do sistema).
- **Como sabemos que toda Feature está coberta?** Aba *Rastreabilidade* do Excel + vínculo *Tested By*: cada PBI tem ao menos um caso positivo, e as regras de negócio têm casos negativos.
- **Como os testes manuais se relacionam com os automatizados?** Os mesmos cenários críticos (idempotência, 429, 401/403, plano adaptativo) também existem como testes xUnit no repositório .NET — o manual valida a experiência do usuário; o automatizado protege contra regressões a cada build.

---

## 10. Mantendo o conteúdo atualizado

Se mudarem algo na API ou quiserem ajustar um caso:
```bash
# edite qa/fonte/dados_qa.py e rode:
python qa/fonte/gerar_artefatos_qa.py
# com a URL da API publicada nos passos:
python qa/fonte/gerar_artefatos_qa.py --url-api https://SUA-API.azurewebsites.net
```
Todos os CSVs, o Excel, o Markdown e o modelo do PDF são regenerados a partir da mesma fonte.
