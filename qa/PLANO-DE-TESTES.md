# MoveSync — Plano de Projeto e Plano de Testes

> Gerado automaticamente a partir de `qa/fonte/dados_qa.py`. Não edite à mão: altere a fonte e rode o gerador.

**Totais:** 5 Epics · 12 Features · 25 PBIs · 40 casos de teste (24 positivos, 16 negativos)

## Parte 1 — Backlog do produto

### EP01 - Identidade e Acesso Seguro

Garantir que cidadãos, gestores de trânsito e dispositivos IoT acessem a MoveSync de forma segura, cada um apenas ao que lhe é permitido, protegendo dados pessoais (LGPD) e a integridade do controle semafórico.

#### F01 - Cadastro e autenticação de pessoas

**Descrição:** Permite que o cidadão crie sua conta e que cidadãos e gestores façam login com e-mail e senha, recebendo um token JWT que define o perfil de acesso (Cidadão ou Gestor). Senhas são armazenadas apenas como hash.

**Critérios de aceite:**

- Dado um visitante, quando ele se cadastra com dados válidos, então a conta é criada sempre com perfil Cidadão.
- Dado um usuário cadastrado, quando informa credenciais corretas, então recebe um JWT válido por 2 horas com seu perfil.
- Dado credenciais inválidas, quando o login é tentado, então a API responde 401 com mensagem genérica, sem revelar se o e-mail existe.
- Dado um endpoint protegido, quando acessado sem token ou com perfil sem permissão, então a API responde 401 ou 403.

- **PBI-01 — Cadastrar conta e fazer login** (prioridade 1, esforço 6)
  - *Como cidadão ou gestor, quero criar minha conta e entrar com e-mail e senha, para acessar as funcionalidades do meu perfil.*
  - Dado dados válidos, quando envio o cadastro, então recebo 201 com meus dados e perfil Cidadao, sem a senha.
  - Dado nome, e-mail ou senha inválidos, quando envio o cadastro, então recebo 400 com o erro de cada campo.
  - Dado um e-mail já cadastrado, quando envio o cadastro, então recebo 409.
  - Dado uma senha, então ela deve ter de 8 a 64 caracteres, com letras e números.
- **PBI-02 — Proteger o acesso por perfil** (prioridade 1, esforço 3)
  - *Como prefeitura, quero que cada perfil acesse apenas suas funções, para proteger a operação dos semáforos.*
  - Dado um token válido, quando consulto /api/auth/me, então recebo meus dados sem informações sensíveis.
  - Dado ausência de token ou token adulterado, então recebo 401.
  - Dado um cidadão logado, quando acessa funcionalidade exclusiva de gestor, então recebe 403.
  - Dado um token de sensor, quando acessa endpoints de pessoas, então recebe 403.

#### F02 - Autenticação de dispositivos IoT

**Descrição:** Sensores (ESP32 com laço indutivo ou câmera com visão computacional) trocam código + segredo por um JWT de perfil Sensor, usado apenas para enviar telemetria. Sensores desativados não autenticam.

**Critérios de aceite:**

- Dado um sensor ativo com segredo correto, quando solicita token, então recebe JWT de perfil Sensor válido por 24 h.
- Dado segredo incorreto, código inexistente ou sensor desativado, quando solicita token, então recebe 401.
- Dado um token de sensor, quando acessa endpoints de pessoas, então recebe 403.

- **PBI-03 — Emitir token para sensor** (prioridade 1, esforço 2)
  - *Como dispositivo IoT instalado no cruzamento, quero me autenticar com código e segredo, para enviar telemetria com segurança.*
  - Dado código e segredo corretos, quando solicito token, então recebo 200 com JWT de perfil Sensor.
  - Dado segredo ou código inválidos, então recebo 401.
- **PBI-04 — Bloquear sensores desativados** (prioridade 2, esforço 1)
  - *Como gestor, quero que sensores desativados não se autentiquem, para impedir dados de equipamentos retirados ou com defeito.*
  - Dado um sensor desativado, quando solicita token, então recebe 401.
  - Dado um token de sensor, quando acessa /api/auth/me, então recebe 403.

### EP02 - Monitoramento do Tráfego com IoT

Cadastrar a infraestrutura monitorada (cruzamentos, vias e sensores) e receber a telemetria dos dispositivos de forma confiável, mesmo com conectividade instável, servindo de base para as decisões do motor semafórico.

#### F03 - Gestão de cruzamentos e vias

**Descrição:** O gestor cadastra, consulta, altera, desativa e exclui cruzamentos e suas vias (2 a 6 por cruzamento). Qualquer pessoa consulta a lista de cruzamentos sem login. O amarelo de cada via é definido pela velocidade da via.

**Critérios de aceite:**

- Dado um gestor, quando cadastra um cruzamento válido com 2 a 6 vias, então recebe 201 com Location e o cruzamento em modo Adaptativo.
- Dado um código já usado, quando cadastra, então recebe 409.
- Dado qualquer visitante, quando lista cruzamentos, então recebe resultado paginado e filtrável sem login.
- Dado um cruzamento com ocorrências de cidadãos, quando o gestor tenta excluir, então recebe 409 e deve apenas desativá-lo.
- Dado uma via, então o amarelo é 3 s até 40 km/h, 4 s até 60 km/h e 5 s acima disso.

- **PBI-05 — Cadastrar e manter cruzamentos e vias** (prioridade 1, esforço 11)
  - *Como gestor de trânsito, quero cadastrar, atualizar, desativar e excluir cruzamentos e suas vias, para manter o cadastro fiel às ruas.*
  - Dado dados válidos e 2 a 6 vias, quando cadastro, então recebo 201 com Location, código em maiúsculas e modo Adaptativo.
  - Dado menos de 2 vias ou dados inválidos, então recebo 400.
  - Dado código duplicado, então recebo 409.
  - Dado dados válidos, quando atualizo, então recebo 200 com os novos dados.
- **PBI-06 — Consultar cruzamentos** (prioridade 1, esforço 3)
  - *Como cidadão, quero ver a lista de cruzamentos monitorados, para escolher o que me interessa no app.*
  - Dado qualquer visitante, quando lista, então recebe página com itens, totalItens e totalPaginas.
  - Dado o filtro busca, então apenas cruzamentos com o texto no nome, código ou endereço são retornados.
  - Dado página < 1 ou tamanho > 100, então recebo 400.
  - Dado um id inexistente, então recebo 404.

#### F04 - Gestão de sensores IoT

**Descrição:** O gestor cadastra um sensor por via (laço indutivo ou câmera), recebe o segredo uma única vez para gravar no dispositivo, acompanha quais estão online/offline e pode desativar ou remover sensores.

**Critérios de aceite:**

- Dado uma via sem sensor, quando o gestor cadastra um sensor, então recebe 201 com o segredo exibido uma única vez.
- Dado uma via que já possui sensor, então novo cadastro retorna 409 (evita contar veículos em dobro).
- Dado a lista de sensores, então cada um indica se está online (enviou dados nos últimos 5 min).
- Dado um sensor desativado, então suas leituras são recusadas com 403.

- **PBI-07 — Cadastrar sensor em uma via** (prioridade 1, esforço 3)
  - *Como gestor, quero cadastrar o sensor instalado em uma via, para que ele possa enviar telemetria.*
  - Dado via sem sensor e código inédito, quando cadastro, então recebo 201 com segredo e aviso de exibição única.
  - Dado via já monitorada, então recebo 409 VIA_JA_MONITORADA.
  - Dado código de sensor repetido, então recebo 409 CODIGO_SENSOR_DUPLICADO.
  - Dado via de outro cruzamento, então recebo 422.
- **PBI-08 — Acompanhar e controlar sensores** (prioridade 2, esforço 2)
  - *Como gestor, quero ver quais sensores estão online e desativar ou remover os defeituosos, para manter a qualidade dos dados.*
  - Dado um cruzamento, quando listo seus sensores, então vejo ativo, online e última leitura de cada um.
  - Dado um sensor, quando o desativo, então suas leituras são recusadas com 403.
  - Dado um sensor, quando o removo, então recebo 204.

#### F05 - Ingestão de telemetria dos sensores

**Descrição:** Recebe as leituras (veículos contados e tamanho da fila) enviadas pelos sensores, individualmente ou em lote (quando o dispositivo ficou sem internet). Cada leitura é idempotente pelo seu identificador: reenvios não duplicam dados.

**Critérios de aceite:**

- Dado uma leitura nova e válida, quando o sensor envia, então recebe 201.
- Dado uma leitura já recebida (mesmo leituraId), quando reenviada, então recebe 200 com a leitura original e Idempotency-Replayed: true.
- Dado data no futuro, com mais de 24 h, ou valores fora da faixa, então a leitura é recusada (422/400).
- Dado um lote de até 100 leituras, então novas são gravadas, repetidas ignoradas e inválidas rejeitadas individualmente.

- **PBI-09 — Receber leitura de forma idempotente** (prioridade 1, esforço 5)
  - *Como sensor IoT, quero enviar cada leitura com um identificador único, para que reenvios por falha de rede não dupliquem a contagem.*
  - Dado leitura nova, então recebo 201 com Location.
  - Dado reenvio com o mesmo leituraId, então recebo 200, mesmo id e cabeçalho Idempotency-Replayed: true.
  - Dado data futura (> 2 min) então 422 LEITURA_NO_FUTURO; com mais de 24 h então 422 LEITURA_EXPIRADA.
  - Dado veículos fora de 0–500 ou fila fora de 0–200, então 400.
- **PBI-10 — Receber lote offline e consultar leituras** (prioridade 2, esforço 4)
  - *Como sensor que ficou sem internet, quero enviar as leituras acumuladas em lote, e como gestor quero consultá-las, para não perder nem esconder dados.*
  - Dado um lote com leituras novas, repetidas e inválidas, então recebo 200 com contagem de registradas, duplicadas e rejeitadas.
  - Dado lote vazio ou com mais de 100 leituras, então recebo 400.
  - Dado uma via, quando consulto, então recebo as leituras paginadas, mais recentes primeiro.

### EP03 - Controle Semafórico Inteligente

Transformar a demanda medida em tempos de semáforo: plano adaptativo pelo método de Webster, fallback seguro quando faltam dados e prioridade para veículos de emergência — sempre respeitando tempos mínimos de segurança.

#### F06 - Plano semafórico adaptativo

**Descrição:** Recalcula os tempos de verde de cada via a cada 30 s (e sob demanda do gestor) proporcionalmente à demanda medida, entra em contingência (tempo fixo seguro) quando falta telemetria, permite ao gestor fixar o tempo e registra o histórico com a justificativa de cada decisão.

**Critérios de aceite:**

- Dado telemetria recente em todas as vias, então o plano é Adaptativo e a via com maior demanda recebe mais verde.
- Dado alguma via sem leituras nos últimos 5 minutos, então o plano entra em Contingência com 30 s de verde por via.
- Dado qualquer plano, então o verde fica entre 15 e 90 s, o amarelo segue a velocidade da via e a fase de pedestres é mantida.
- Dado o modo TempoFixo definido pelo gestor, então todas as vias recebem 30 s.
- Dado recálculo sem mudança relevante (≤ 2 s), então nenhum novo plano é gravado.

- **PBI-11 — Calcular plano adaptativo com fallback seguro** (prioridade 1, esforço 11)
  - *Como motorista, quero que o semáforo dê mais verde para a via mais carregada e continue seguro se um sensor falhar, para esperar menos sem correr riscos.*
  - Dado demanda desequilibrada, então a via com mais veículos e fila recebe o maior verde.
  - Dado o plano gerado, então ele traz uma justificativa em linguagem simples.
  - Dado via sem leitura nos últimos 5 min, então o modo é Contingencia com 30 s por via e a justificativa cita a via sem dados.
- **PBI-12 — Operar e auditar os planos** (prioridade 1, esforço 7)
  - *Como gestor, quero alternar o modo de operação, consultar o plano vigente e o histórico e contar com o recálculo automático, para controlar e auditar o semáforo.*
  - Dado o modo TempoFixo, então o plano vigente passa a TempoFixo imediatamente.
  - Dado um modo inexistente, então recebo 400.
  - Dado usuário logado, quando consulto o plano vigente, então vejo ciclo, fases e justificativa.
  - Dado o gestor, quando consulto o histórico, então vejo os planos mais recentes primeiro, com apenas um vigente.

#### F07 - Prioridade para veículos de emergência

**Descrição:** A central de trânsito aciona um corredor verde para ambulância, bombeiros, polícia ou defesa civil: a via do veículo recebe o verde máximo e as demais o mínimo de segurança, por 30 a 600 s ou até ser encerrado.

**Critérios de aceite:**

- Dado um acionamento válido, então o plano passa a Emergencia com 90 s na via do veículo e 15 s nas demais.
- Dado uma emergência ativa no cruzamento, então novo acionamento retorna 409.
- Dado o encerramento, então o cruzamento volta ao plano normal; encerrar de novo retorna 409.
- Dado o acionamento, então é exigido o cabeçalho Idempotency-Key.

- **PBI-13 — Acionar corredor verde** (prioridade 1, esforço 5)
  - *Como central de trânsito, quero priorizar a via por onde passa uma ambulância, para reduzir o tempo de resposta de emergências.*
  - Dado via válida e duração entre 30 e 600 s, então recebo 201 com o plano Emergencia aplicado.
  - Dado emergência já ativa no cruzamento, então recebo 409 EMERGENCIA_JA_ATIVA.
  - Dado via de outro cruzamento, então recebo 422; duração fora da faixa, então 400.
- **PBI-14 — Acompanhar e encerrar emergências** (prioridade 2, esforço 2)
  - *Como central de trânsito, quero listar e encerrar corredores de emergência, para devolver a normalidade ao trânsito.*
  - Dado emergências ativas, quando listo, então vejo todas em andamento.
  - Dado encerramento, então o plano volta ao modo normal; encerrar novamente retorna 409.

### EP04 - Serviços ao Cidadão

Entregar benefício direto ao cidadão: informação do tempo de espera, travessia acessível para quem precisa de mais tempo e um canal para relatar problemas e acompanhar a resposta da prefeitura.

#### F08 - Status do cruzamento e tempo de espera

**Descrição:** Consulta pública (sem login) da situação do cruzamento: modo do semáforo, tempo médio e máximo de espera por via, nível de congestionamento, tempo de travessia do pedestre e avisos (contingência, emergência, travessia acessível).

**Critérios de aceite:**

- Dado um cruzamento ativo, quando qualquer pessoa consulta o status, então recebe modo, situação, espera por via e travessia.
- Dado modo especial (contingência, emergência ou travessia acessível), então o status traz um aviso em linguagem simples.
- Dado cruzamento inexistente → 404; desativado → 422.

- **PBI-15 — Consultar situação e espera estimada** (prioridade 1, esforço 5)
  - *Como cidadão, quero saber quanto tempo vou esperar em cada via do cruzamento, para planejar meu trajeto.*
  - Dado um cruzamento ativo, então recebo esperaMediaSegundos, esperaMaximaSegundos e nível de congestionamento por via, sem login.
- **PBI-16 — Exibir avisos de situações especiais** (prioridade 2, esforço 2)
  - *Como cidadão, quero ser avisado quando o semáforo estiver em situação especial, para agir com mais atenção.*
  - Dado contingência, emergência ou travessia acessível, então a lista de avisos explica a situação.

#### F09 - Travessia acessível (botoeira virtual)

**Descrição:** Pessoas com deficiência, mobilidade reduzida, idosas, gestantes ou acompanhando crianças pedem pelo app mais tempo para atravessar. Por 5 minutos, a fase de pedestres é calculada a 0,8 m/s (em vez de 1,2 m/s).

**Critérios de aceite:**

- Dado um cidadão logado, quando solicita travessia acessível com motivo válido e Idempotency-Key, então recebe 201 e o plano é recalculado na hora.
- Dado uma solicitação vigente do mesmo cidadão, então nova solicitação retorna 200 com a mesma solicitação.
- Dado a solicitação vigente, então o status do cruzamento indica travessia acessível ativa.

- **PBI-17 — Solicitar travessia acessível** (prioridade 1, esforço 5)
  - *Como pessoa com mobilidade reduzida, quero pedir mais tempo para atravessar pelo app, para cruzar a rua com segurança.*
  - Dado motivo válido e Idempotency-Key, então recebo 201 com tempo de travessia ampliado e validade de 5 minutos.
  - Dado ausência de Idempotency-Key → 400; motivo inválido → 400; perfil gestor → 403.
- **PBI-18 — Reaproveitar solicitação vigente** (prioridade 2, esforço 1)
  - *Como cidadão, quero que um novo pedido enquanto o anterior vale não crie duplicidade, para não sobrecarregar o semáforo.*
  - Dado solicitação vigente, então novo pedido retorna 200 com a mesma solicitação.

#### F10 - Ocorrências do cidadão

**Descrição:** O cidadão relata problemas no cruzamento (semáforo apagado, piscando, travessia insegura, tempo insuficiente...). O sistema prioriza automaticamente e o gestor trata a ocorrência até resolvê-la ou rejeitá-la, respondendo ao cidadão.

**Critérios de aceite:**

- Dado um cidadão, quando registra ocorrência válida com Idempotency-Key, então recebe 201 com prioridade definida pelo tipo.
- Dado repetição com a mesma chave e o mesmo corpo, então recebe a mesma resposta sem duplicar.
- Dado uma ocorrência Aberta, então só o autor pode editá-la ou excluí-la; após análise, não pode mais.
- Dado o gestor, então ele altera o status seguindo Aberta → EmAnalise → Resolvida/Rejeitada, com resposta obrigatória ao finalizar.
- Dado outro cidadão, então ele não enxerga a ocorrência (404).

- **PBI-19 — Registrar ocorrência com priorização automática** (prioridade 1, esforço 5)
  - *Como cidadão, quero relatar um problema no cruzamento, para que a prefeitura conserte rapidamente.*
  - Dado tipo e descrição válidos, então recebo 201; SemaforoApagado = Crítica, Piscando/TravessiaInsegura = Alta.
  - Dado repetição com a mesma Idempotency-Key, então recebo a mesma resposta; mesma chave com outro corpo → 422.
  - Dado descrição com menos de 10 caracteres → 400; cruzamento inexistente → 404.
- **PBI-20 — Acompanhar, editar e tratar ocorrências** (prioridade 1, esforço 7)
  - *Como cidadão, quero acompanhar e corrigir minhas ocorrências, e como gestor quero tratá-las respondendo ao cidadão, para dar transparência ao atendimento.*
  - Dado meu login, então listo apenas as minhas ocorrências, com filtro opcional por status.
  - Dado a ocorrência de outro cidadão, então recebo 404.
  - Dado status Aberta, então posso editar (200) ou excluir (204).
  - Dado status EmAnalise ou finalizado, então recebo 409.

### EP05 - Gestão, Impacto e Confiabilidade

Dar à prefeitura visão operacional e indicadores de impacto (tempo, CO₂) e garantir que a API seja segura, documentada, resistente a abuso e monitorável em produção.

#### F11 - Painel do gestor, indicadores e relatório de impacto

**Descrição:** Painel com cruzamentos ativos, modos em vigor, sensores offline, leituras da última hora, ocorrências pendentes por prioridade e emergências ativas; e comparação tempo fixo × adaptativo (atraso médio, capacidade, horas e CO₂ evitados). Relatório de impacto para a prefeitura: projeção mensal de horas e CO₂ evitados, valor do tempo devolvido à população, relação benefício/custo da assinatura e payback social, com simulação de cenários.

**Critérios de aceite:**

- Dado o gestor, quando consulta o painel, então vê os totais operacionais atualizados.
- Dado telemetria recente em todas as vias, então os indicadores mostram a redução de atraso e o CO₂ evitado.
- Dado falta de dados, então os indicadores informam dadosSuficientes = false e explicam o motivo.
- Dado o relatório de impacto, então a prefeitura vê a relação benefício/custo calculada com a telemetria real e as premissas usadas.

- **PBI-21 — Painel operacional do gestor** (prioridade 2, esforço 3)
  - *Como gestor de trânsito, quero um resumo da operação, para agir rapidamente onde há problema.*
  - Dado o gestor, então recebo cruzamentos ativos/inativos, planos por modo, sensores offline (com códigos), leituras da última hora, ocorrências pendentes por prioridade e emergências ativas.
  - Dado outro perfil → 403; sem token → 401.
- **PBI-22 — Indicadores de impacto (tempo fixo × adaptativo)** (prioridade 1, esforço 5)
  - *Como prefeitura, quero medir quanto tempo e CO₂ o semáforo inteligente economiza, para justificar a expansão para outras regiões.*
  - Dado dados suficientes, então vejo atraso médio de cada modo, redução percentual, horas e kg de CO₂ evitados por hora e uma interpretação.
  - Dado dados insuficientes, então dadosSuficientes = false e comparacao nula.
- **PBI-23 — Relatório de impacto e benefício/custo para a prefeitura** (prioridade 1, esforço 5)
  - *Como prefeitura (cliente pagante), quero um relatório que converta o ganho medido em horas, CO₂ e reais, comparado ao custo da assinatura, para justificar o contrato e a expansão para novos cruzamentos.*
  - Dado telemetria recente, então vejo por mês as horas de espera e o CO₂ evitados, o valor do tempo devolvido, o custo da assinatura, a relação benefício/custo, o payback social e as premissas usadas.
  - Dado nenhum cruzamento com dados, então o relatório não inventa ganho: benefício zero e relação benefício/custo nula, com explicação.
  - Dado parâmetros de simulação (horas por dia, valor da hora, preço da assinatura), então o cálculo usa esses valores sem alterar a configuração; fora da faixa → 400.
  - Dado outro perfil → 403; sem token → 401.

#### F12 - Proteção, observabilidade e documentação da API

**Descrição:** Recursos transversais que garantem confiabilidade: idempotência de operações críticas, limitação de requisições (HTTP 429), health checks de API, banco, sensores e serviço externo, documentação OpenAPI/Swagger e erros padronizados (ProblemDetails).

**Critérios de aceite:**

- Dado excesso de requisições, então a API responde 429 com Retry-After, por IP (login/consultas), por usuário e por sensor.
- Dado /health, então são exibidos os status de API, banco, sensores IoT e serviço externo; banco fora → 503.
- Dado /swagger, então todos os endpoints estão documentados, com esquema Bearer e cabeçalho Idempotency-Key onde exigido.
- Dado qualquer erro, então a resposta segue o padrão ProblemDetails com campo codigo.

- **PBI-24 — Limitar requisições excessivas** (prioridade 1, esforço 3)
  - *Como prefeitura, quero limitar o excesso de requisições, para proteger a API contra ataques de força bruta e dispositivos com defeito.*
  - Dado mais de 10 logins por minuto do mesmo IP → 429 com Retry-After.
  - Dado um sensor acima do limite → 429 apenas para ele; outros sensores seguem normais.
- **PBI-25 — Monitorar e documentar a API** (prioridade 2, esforço 4)
  - *Como equipe de operação e desenvolvedores do app, queremos health checks e documentação OpenAPI com erros padronizados, para detectar falhas e integrar com segurança.*
  - Dado /health/live → 200 se a API está no ar; /health/ready → 200 se o banco responde.
  - Dado sensores sem dados, então a verificação sensores-iot fica Degraded (o sistema segue em contingência).
  - Dado /openapi/v1.json, então o documento descreve todos os endpoints, o esquema Bearer e o cabeçalho Idempotency-Key.
  - Dado JSON malformado, então recebo 400 em ProblemDetails com codigo REQUISICAO_INVALIDA.

## Parte 2 — Casos de teste

### F01 - Cadastro e autenticação de pessoas

#### CT-F01-01 - [Positivo] Cadastrar cidadão com dados válidos

- **Objetivo:** Verificar que um visitante consegue criar uma conta de cidadão com dados válidos.
- **Requisito:** PBI-01 · **Prioridade:** 1
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados. · E-mail a ser usado ainda não cadastrado.
- **Inputs:** nome: "Maria Teste" · email: "Maria.Teste+[data-hora]@Email.com" (com letras maiúsculas) · senha: "Senha123"
- **Outputs esperados:** HTTP 201 Created · Corpo com id, nome, email em minúsculas, perfil "Cidadao" e criadoEm · Nenhum campo de senha/hash no corpo

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Expandir POST /api/auth/registro e clicar em "Try it out". | O campo de corpo da requisição fica editável com um exemplo de JSON. |
| 3 | Informar o corpo {"nome": "Maria Teste", "email": "Maria.Teste+[data-hora]@Email.com", "senha": "Senha123"} e clicar em "Execute". | A API retorna HTTP 201 Created. |
| 4 | Analisar o corpo da resposta. | O JSON contém id, nome "Maria Teste", email em letras minúsculas, perfil "Cidadao" e criadoEm; não há campo de senha. |

#### CT-F01-02 - [Negativo] Recusar login com senha incorreta

- **Objetivo:** Verificar que credenciais erradas não geram token e que a mensagem não revela dados.
- **Requisito:** PBI-01 · **Prioridade:** 1
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados.
- **Inputs:** email: "gestor@movesync.dev" · senha: "SenhaErrada1"
- **Outputs esperados:** HTTP 401 Unauthorized · codigo "CREDENCIAIS_INVALIDAS" · Sem accessToken

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Executar POST /api/auth/login com {"email": "gestor@movesync.dev", "senha": "SenhaErrada1"}. | A API retorna HTTP 401 Unauthorized. |
| 3 | Analisar o corpo da resposta. | codigo "CREDENCIAIS_INVALIDAS" e detail "E-mail/código ou senha inválidos."; nenhum token é emitido. |

#### CT-F01-03 - [Negativo] Cidadão não acessa funcionalidade de gestor

- **Objetivo:** Verificar o controle de acesso por perfil (autorização).
- **Requisito:** PBI-02 · **Prioridade:** 1
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados.
- **Inputs:** Token da cidadã
- **Outputs esperados:** HTTP 403 Forbidden

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Em "Autenticação", expandir POST /api/auth/login, clicar em "Try it out", informar o corpo {"email": "cidada@movesync.dev", "senha": "Cidada@2026"} e clicar em "Execute". | A API retorna HTTP 200 com "accessToken", "tokenType": "Bearer" e "perfil": "Cidadao". |
| 3 | Copiar o valor de "accessToken" da cidadã, clicar no botão "Authorize" (cadeado), colar o token e confirmar em "Authorize" e "Close". | O cadeado dos endpoints protegidos aparece fechado, indicando que o token será enviado nas próximas requisições. |
| 4 | Executar GET /api/painel/resumo. | A API retorna HTTP 403 Forbidden. |
| 5 | Executar GET /api/ocorrencias (listagem geral do gestor). | A API retorna HTTP 403 Forbidden. |

### F02 - Autenticação de dispositivos IoT

#### CT-F02-01 - [Positivo] Emitir token para sensor com credenciais válidas

- **Objetivo:** Verificar que um sensor ativo obtém JWT de perfil Sensor.
- **Requisito:** PBI-03 · **Prioridade:** 1
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados. · Sensor SPO002-V1 ativo (seed).
- **Inputs:** codigo: "SPO002-V1" · segredo: "sensor-demo-local-2026"
- **Outputs esperados:** HTTP 200 · accessToken preenchido, perfil "Sensor", expiraEm cerca de 24 h no futuro · usuario = null

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Expandir POST /api/auth/sensores/token, clicar em "Try it out" e informar {"codigo": "SPO002-V1", "segredo": "sensor-demo-local-2026"}. | O corpo é aceito. |
| 3 | Clicar em "Execute". | A API retorna HTTP 200 OK. |
| 4 | Analisar o corpo da resposta. | perfil "Sensor", tokenType "Bearer", expiraEm cerca de 24 h à frente e usuario nulo. |

#### CT-F02-02 - [Negativo] Recusar token de sensor com segredo incorreto

- **Objetivo:** Garantir que apenas dispositivos com o segredo correto enviem dados.
- **Requisito:** PBI-03 · **Prioridade:** 1
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados.
- **Inputs:** codigo: "SPO002-V1" · segredo: "segredo-incorreto-123456"
- **Outputs esperados:** HTTP 401 · codigo "CREDENCIAIS_INVALIDAS"

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Executar POST /api/auth/sensores/token com {"codigo": "SPO002-V1", "segredo": "segredo-incorreto-123456"}. | A API retorna HTTP 401 Unauthorized. |
| 3 | Analisar o corpo. | codigo "CREDENCIAIS_INVALIDAS"; nenhum token emitido. |

#### CT-F02-03 - [Negativo] Sensor desativado não consegue se autenticar

- **Objetivo:** Verificar que a desativação do sensor bloqueia novos tokens.
- **Requisito:** PBI-04 · **Prioridade:** 2
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados. · Id do sensor SPO001-V3 obtido em GET /api/cruzamentos/{id}/sensores (cruzamento SPO-001).
- **Inputs:** PATCH /api/sensores/{id}/ativo com {"ativo": false} · Credenciais do sensor SPO001-V3
- **Outputs esperados:** PATCH retorna 200 com ativo=false · Solicitação de token retorna 401 · Após reativar, token volta a ser emitido

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Em "Autenticação", expandir POST /api/auth/login, clicar em "Try it out", informar o corpo {"email": "gestor@movesync.dev", "senha": "Gestor@2026"} e clicar em "Execute". | A API retorna HTTP 200 com "accessToken", "tokenType": "Bearer" e "perfil": "Gestor". |
| 3 | Copiar o valor de "accessToken" do gestor, clicar no botão "Authorize" (cadeado), colar o token e confirmar em "Authorize" e "Close". | O cadeado dos endpoints protegidos aparece fechado, indicando que o token será enviado nas próximas requisições. |
| 4 | Expandir GET /api/cruzamentos, clicar em "Try it out", informar busca = "SPO-001" e clicar em "Execute". Anotar o "id" do cruzamento e os "id" das vias. | A API retorna HTTP 200 com o cruzamento SPO-001, suas vias e respectivos identificadores. |
| 5 | Executar GET /api/cruzamentos/{id}/sensores com o id do SPO-001 e anotar o id do sensor SPO001-V3. | A API retorna HTTP 200 com a lista de sensores. |
| 6 | Executar PATCH /api/sensores/{id}/ativo com {"ativo": false}. | A API retorna HTTP 200 com "ativo": false. |
| 7 | Executar POST /api/auth/sensores/token com {"codigo": "SPO001-V3", "segredo": "sensor-demo-local-2026"}. | A API retorna HTTP 401 Unauthorized. |
| 8 | Restaurar: executar PATCH /api/sensores/{id}/ativo com {"ativo": true} e repetir a solicitação de token. | PATCH retorna 200 e o token volta a ser emitido (HTTP 200). |

### F03 - Gestão de cruzamentos e vias

#### CT-F03-01 - [Positivo] Gestor cadastra cruzamento com duas vias

- **Objetivo:** Verificar o cadastro completo de um cruzamento e o cálculo do amarelo por velocidade.
- **Requisito:** PBI-05 · **Prioridade:** 1
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados. · Código "OSA-QA1" ainda não utilizado.
- **Inputs:** codigo "osa-qa1" (minúsculo) · nome, endereço, latitude -23.53, longitude -46.79, largura 14 · Via 1: "Av. dos Autonomistas", 3 faixas, 60 km/h · Via 2: "R. de Teste", 1 faixa, 40 km/h
- **Outputs esperados:** HTTP 201 com cabeçalho Location · codigo "OSA-QA1", modo "Adaptativo", ativo true · amareloSegundos 4 na via de 60 km/h e 3 na via de 40 km/h

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Em "Autenticação", expandir POST /api/auth/login, clicar em "Try it out", informar o corpo {"email": "gestor@movesync.dev", "senha": "Gestor@2026"} e clicar em "Execute". | A API retorna HTTP 200 com "accessToken", "tokenType": "Bearer" e "perfil": "Gestor". |
| 3 | Copiar o valor de "accessToken" do gestor, clicar no botão "Authorize" (cadeado), colar o token e confirmar em "Authorize" e "Close". | O cadeado dos endpoints protegidos aparece fechado, indicando que o token será enviado nas próximas requisições. |
| 4 | Expandir POST /api/cruzamentos, clicar em "Try it out" e informar: {"codigo": "osa-qa1", "nome": "Av. dos Autonomistas x R. de Teste", "endereco": "Av. dos Autonomistas, 1500 - Osasco/SP", "latitude": -23.53, "longitude": -46.79, "larguraTravessiaMetros": 14, "vias": [{"nome": "Av. dos Autonomistas", "sentido": "Centro", "numeroFaixas": 3, "velocidadeMaximaKmh": 60}, {"nome": "R. de Teste", "sentido": "Bairro", "numeroFaixas": 1, "velocidadeMaximaKmh": 40}]}. | O corpo é aceito. |
| 5 | Clicar em "Execute". | A API retorna HTTP 201 Created e o cabeçalho location /api/cruzamentos/{id}. |
| 6 | Analisar o corpo da resposta. | codigo "OSA-QA1", modo "Adaptativo", ativo true, 2 vias; amarelo 4 s (60 km/h) e 3 s (40 km/h). |
| 7 | Executar GET /api/cruzamentos/{id} com o id retornado. | A API retorna HTTP 200 com os mesmos dados (persistidos). |

#### CT-F03-02 - [Negativo] Bloquear exclusão de cruzamento com ocorrências

- **Objetivo:** Garantir que o histórico de relatos dos cidadãos não seja apagado.
- **Requisito:** PBI-05 · **Prioridade:** 2
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados. · Ao menos uma ocorrência registrada no SPO-001 (se ainda não houver, registrar uma conforme o CT-F10-01).
- **Inputs:** id do SPO-001
- **Outputs esperados:** HTTP 409 · codigo "CRUZAMENTO_COM_OCORRENCIAS" · Cruzamento continua existindo

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Em "Autenticação", expandir POST /api/auth/login, clicar em "Try it out", informar o corpo {"email": "gestor@movesync.dev", "senha": "Gestor@2026"} e clicar em "Execute". | A API retorna HTTP 200 com "accessToken", "tokenType": "Bearer" e "perfil": "Gestor". |
| 3 | Copiar o valor de "accessToken" do gestor, clicar no botão "Authorize" (cadeado), colar o token e confirmar em "Authorize" e "Close". | O cadeado dos endpoints protegidos aparece fechado, indicando que o token será enviado nas próximas requisições. |
| 4 | Expandir GET /api/cruzamentos, clicar em "Try it out", informar busca = "SPO-001" e clicar em "Execute". Anotar o "id" do cruzamento e os "id" das vias. | A API retorna HTTP 200 com o cruzamento SPO-001, suas vias e respectivos identificadores. |
| 5 | Executar DELETE /api/cruzamentos/{id} com o id do SPO-001. | A API retorna HTTP 409 Conflict com codigo "CRUZAMENTO_COM_OCORRENCIAS". |
| 6 | Executar GET /api/cruzamentos/{id}. | A API retorna HTTP 200: o cruzamento não foi excluído. |

#### CT-F03-03 - [Positivo] Listar cruzamentos sem login com paginação

- **Objetivo:** Verificar a listagem pública e paginada.
- **Requisito:** PBI-06 · **Prioridade:** 1
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados. · Nenhum token configurado.
- **Inputs:** pagina = 1 · tamanhoPagina = 1
- **Outputs esperados:** HTTP 200 · itens com 1 elemento · totalItens ≥ 2 e totalPaginas ≥ 2

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Expandir GET /api/cruzamentos, informar pagina = 1 e tamanhoPagina = 1 e clicar em "Execute". | A API retorna HTTP 200 OK sem exigir login. |
| 3 | Analisar o corpo. | itens tem 1 elemento; pagina 1; tamanhoPagina 1; totalItens ≥ 2; totalPaginas = totalItens. |
| 4 | Repetir com pagina = 2. | Retorna outro cruzamento, diferente da página 1. |

### F04 - Gestão de sensores IoT

#### CT-F04-01 - [Positivo] Cadastrar sensor em via sem sensor

- **Objetivo:** Verificar o cadastro de sensor e a exibição única do segredo.
- **Requisito:** PBI-07 · **Prioridade:** 1
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados. · Cruzamento OSA-QA1 cadastrado (CT-F03-01), sem sensores.
- **Inputs:** codigo "sns-qa-01" · viaId da via "Av. dos Autonomistas" · tipo "Camera"
- **Outputs esperados:** HTTP 201 · sensor.codigo "SNS-QA-01", ativo true, online false · segredo preenchido e aviso de que não será exibido novamente

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Em "Autenticação", expandir POST /api/auth/login, clicar em "Try it out", informar o corpo {"email": "gestor@movesync.dev", "senha": "Gestor@2026"} e clicar em "Execute". | A API retorna HTTP 200 com "accessToken", "tokenType": "Bearer" e "perfil": "Gestor". |
| 3 | Copiar o valor de "accessToken" do gestor, clicar no botão "Authorize" (cadeado), colar o token e confirmar em "Authorize" e "Close". | O cadeado dos endpoints protegidos aparece fechado, indicando que o token será enviado nas próximas requisições. |
| 4 | Expandir GET /api/cruzamentos, clicar em "Try it out", informar busca = "OSA-QA1" e clicar em "Execute". Anotar o "id" do cruzamento e os "id" das vias. | A API retorna HTTP 200 com o cruzamento OSA-QA1, suas vias e respectivos identificadores. |
| 5 | Executar POST /api/cruzamentos/{id}/sensores com {"codigo": "sns-qa-01", "viaId": "[id da via Av. dos Autonomistas]", "tipo": "Camera"}. | A API retorna HTTP 201 Created. |
| 6 | Analisar o corpo da resposta. | sensor.codigo "SNS-QA-01", tipo "Camera", ativo true, online false; campo "segredo" preenchido e "aviso" orientando a guardá-lo. |
| 7 | Executar GET /api/cruzamentos/{id}/sensores. | O sensor aparece na lista e o segredo NÃO é exibido. |

#### CT-F04-02 - [Negativo] Recusar segundo sensor na mesma via

- **Objetivo:** Garantir um sensor por via para não duplicar a contagem de veículos.
- **Requisito:** PBI-07 · **Prioridade:** 2
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados. · Sensor SNS-QA-01 cadastrado na via "Av. dos Autonomistas" do OSA-QA1.
- **Inputs:** codigo "sns-qa-02" na mesma via
- **Outputs esperados:** HTTP 409 · codigo "VIA_JA_MONITORADA"

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Em "Autenticação", expandir POST /api/auth/login, clicar em "Try it out", informar o corpo {"email": "gestor@movesync.dev", "senha": "Gestor@2026"} e clicar em "Execute". | A API retorna HTTP 200 com "accessToken", "tokenType": "Bearer" e "perfil": "Gestor". |
| 3 | Copiar o valor de "accessToken" do gestor, clicar no botão "Authorize" (cadeado), colar o token e confirmar em "Authorize" e "Close". | O cadeado dos endpoints protegidos aparece fechado, indicando que o token será enviado nas próximas requisições. |
| 4 | Expandir GET /api/cruzamentos, clicar em "Try it out", informar busca = "OSA-QA1" e clicar em "Execute". Anotar o "id" do cruzamento e os "id" das vias. | A API retorna HTTP 200 com o cruzamento OSA-QA1, suas vias e respectivos identificadores. |
| 5 | Executar POST /api/cruzamentos/{id}/sensores com {"codigo": "sns-qa-02", "viaId": "[mesma via]", "tipo": "LacoIndutivo"}. | A API retorna HTTP 409 com codigo "VIA_JA_MONITORADA". |

#### CT-F04-03 - [Negativo] Recusar leitura de sensor desativado

- **Objetivo:** Verificar que a desativação bloqueia a telemetria mesmo com token ainda válido.
- **Requisito:** PBI-08 · **Prioridade:** 2
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados. · Token do sensor SPO002-V3 obtido antes da desativação.
- **Inputs:** PATCH /api/sensores/{id}/ativo {"ativo": false} · Leitura válida com o token do SPO002-V3
- **Outputs esperados:** Leitura recusada com HTTP 403 e codigo "SENSOR_INATIVO"

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Expandir POST /api/auth/sensores/token, clicar em "Try it out", informar {"codigo": "SPO002-V3", "segredo": "sensor-demo-local-2026"} e clicar em "Execute". | A API retorna HTTP 200 com "accessToken" e "perfil": "Sensor". |
| 3 | Registrar o token do sensor em "Authorize" (substituindo qualquer token anterior com "Logout"). | O Swagger passa a enviar o token do sensor nas requisições. |
| 4 | Copiar o token do sensor para uso posterior. | Token guardado. |
| 5 | Em "Autenticação", expandir POST /api/auth/login, clicar em "Try it out", informar o corpo {"email": "gestor@movesync.dev", "senha": "Gestor@2026"} e clicar em "Execute". | A API retorna HTTP 200 com "accessToken", "tokenType": "Bearer" e "perfil": "Gestor". |
| 6 | Copiar o valor de "accessToken" do gestor, clicar no botão "Authorize" (cadeado), colar o token e confirmar em "Authorize" e "Close". | O cadeado dos endpoints protegidos aparece fechado, indicando que o token será enviado nas próximas requisições. |
| 7 | Expandir GET /api/cruzamentos, clicar em "Try it out", informar busca = "SPO-002" e clicar em "Execute". Anotar o "id" do cruzamento e os "id" das vias. | A API retorna HTTP 200 com o cruzamento SPO-002, suas vias e respectivos identificadores. |
| 8 | Obter o id do SPO002-V3 em GET /api/cruzamentos/{id}/sensores e executar PATCH /api/sensores/{id}/ativo com {"ativo": false}. | A API retorna HTTP 200 com ativo false. |
| 9 | Registrar o token do sensor guardado e executar POST /api/telemetria/leituras com {"leituraId": "qa-inativo-01", "coletadaEm": "[data/hora UTC atual, ex.: 2026-11-19T18:00:00Z]", "veiculosContados": 5, "tamanhoFila": 0}. | A API retorna HTTP 403 com codigo "SENSOR_INATIVO". |
| 10 | Restaurar: com o token do gestor, executar PATCH /api/sensores/{id}/ativo com {"ativo": true}. | A API retorna HTTP 200 com ativo true. |

### F05 - Ingestão de telemetria dos sensores

#### CT-F05-01 - [Positivo] Registrar leitura válida de sensor

- **Objetivo:** Verificar o recebimento de uma leitura nova.
- **Requisito:** PBI-09 · **Prioridade:** 1
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados. · Token do sensor SPO002-V1.
- **Inputs:** {"leituraId": "qa-leitura-001", "coletadaEm": "[data/hora UTC atual, ex.: 2026-11-19T18:00:00Z]", "veiculosContados": 42, "tamanhoFila": 9}
- **Outputs esperados:** HTTP 201 com Location /api/telemetria/vias/{viaId}/leituras · Corpo com leituraId, viaId, veiculosContados 42, tamanhoFila 9, recebidaEm

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Expandir POST /api/auth/sensores/token, clicar em "Try it out", informar {"codigo": "SPO002-V1", "segredo": "sensor-demo-local-2026"} e clicar em "Execute". | A API retorna HTTP 200 com "accessToken" e "perfil": "Sensor". |
| 3 | Registrar o token do sensor em "Authorize" (substituindo qualquer token anterior com "Logout"). | O Swagger passa a enviar o token do sensor nas requisições. |
| 4 | Expandir POST /api/telemetria/leituras, clicar em "Try it out" e informar {"leituraId": "qa-leitura-001", "coletadaEm": "[data/hora UTC atual, ex.: 2026-11-19T18:00:00Z]", "veiculosContados": 42, "tamanhoFila": 9}. | O corpo é aceito. |
| 5 | Clicar em "Execute". | A API retorna HTTP 201 Created com cabeçalho location. |
| 6 | Analisar o corpo. | leituraId "qa-leitura-001", veiculosContados 42, tamanhoFila 9, viaId da via do sensor e recebidaEm preenchido. |

#### CT-F05-02 - [Positivo] Reenvio da mesma leitura não duplica (idempotência)

- **Objetivo:** Verificar que um reenvio com o mesmo leituraId devolve a leitura original sem criar outra.
- **Requisito:** PBI-09 · **Prioridade:** 1
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados. · CT-F05-01 executado (leitura qa-leitura-001 existente).
- **Inputs:** Mesmo corpo do CT-F05-01
- **Outputs esperados:** HTTP 200 (e não 201) · Cabeçalho Idempotency-Replayed: true · Mesmo id da leitura original

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Expandir POST /api/auth/sensores/token, clicar em "Try it out", informar {"codigo": "SPO002-V1", "segredo": "sensor-demo-local-2026"} e clicar em "Execute". | A API retorna HTTP 200 com "accessToken" e "perfil": "Sensor". |
| 3 | Registrar o token do sensor em "Authorize" (substituindo qualquer token anterior com "Logout"). | O Swagger passa a enviar o token do sensor nas requisições. |
| 4 | Executar novamente POST /api/telemetria/leituras com {"leituraId": "qa-leitura-001", "coletadaEm": "[data/hora UTC atual, ex.: 2026-11-19T18:00:00Z]", "veiculosContados": 42, "tamanhoFila": 9}. | A API retorna HTTP 200 OK. |
| 5 | Analisar os cabeçalhos da resposta (Response headers). | Existe o cabeçalho idempotency-replayed: true. |
| 6 | Comparar o "id" com o retornado no CT-F05-01. | O id é o mesmo: nenhuma leitura duplicada foi criada. |

#### CT-F05-03 - [Negativo] Recusar leitura com data no futuro

- **Objetivo:** Detectar sensor com relógio desajustado.
- **Requisito:** PBI-09 · **Prioridade:** 2
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados. · Token do sensor SPO002-V2.
- **Inputs:** {"leituraId": "qa-futuro-01", "coletadaEm": "2030-01-01T00:00:00Z", "veiculosContados": 10, "tamanhoFila": 1}
- **Outputs esperados:** HTTP 422 · codigo "LEITURA_NO_FUTURO"

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Expandir POST /api/auth/sensores/token, clicar em "Try it out", informar {"codigo": "SPO002-V2", "segredo": "sensor-demo-local-2026"} e clicar em "Execute". | A API retorna HTTP 200 com "accessToken" e "perfil": "Sensor". |
| 3 | Registrar o token do sensor em "Authorize" (substituindo qualquer token anterior com "Logout"). | O Swagger passa a enviar o token do sensor nas requisições. |
| 4 | Executar POST /api/telemetria/leituras com {"leituraId": "qa-futuro-01", "coletadaEm": "2030-01-01T00:00:00Z", "veiculosContados": 10, "tamanhoFila": 1}. | A API retorna HTTP 422 com codigo "LEITURA_NO_FUTURO". |

#### CT-F05-04 - [Positivo] Processar lote com leituras novas, repetidas e inválidas

- **Objetivo:** Verificar o envio em lote após queda de conexão do sensor.
- **Requisito:** PBI-10 · **Prioridade:** 2
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados. · Token do sensor SPO002-V1; leitura qa-leitura-001 já existente (CT-F05-01).
- **Inputs:** Lote com 3 leituras: qa-leitura-001 (repetida), qa-lote-002 (nova e válida), qa-lote-003 (data em 2030)
- **Outputs esperados:** HTTP 200 · recebidas 3, registradas 1, duplicadas 1 · rejeitadas com 1 item (qa-lote-003) e o motivo

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Expandir POST /api/auth/sensores/token, clicar em "Try it out", informar {"codigo": "SPO002-V1", "segredo": "sensor-demo-local-2026"} e clicar em "Execute". | A API retorna HTTP 200 com "accessToken" e "perfil": "Sensor". |
| 3 | Registrar o token do sensor em "Authorize" (substituindo qualquer token anterior com "Logout"). | O Swagger passa a enviar o token do sensor nas requisições. |
| 4 | Executar POST /api/telemetria/leituras/lote com {"leituras": [{"leituraId": "qa-leitura-001", "coletadaEm": "[data/hora UTC atual, ex.: 2026-11-19T18:00:00Z]", "veiculosContados": 42, "tamanhoFila": 9}, {"leituraId": "qa-lote-002", "coletadaEm": "[data/hora UTC atual, ex.: 2026-11-19T18:00:00Z]", "veiculosContados": 18, "tamanhoFila": 4}, {"leituraId": "qa-lote-003", "coletadaEm": "2030-01-01T00:00:00Z", "veiculosContados": 7, "tamanhoFila": 1}]}. | A API retorna HTTP 200 OK. |
| 5 | Analisar o corpo da resposta. | recebidas 3, registradas 1, duplicadas 1 e a lista rejeitadas contém qa-lote-003 com motivo de data no futuro. |

### F06 - Plano semafórico adaptativo

#### CT-F06-01 - [Positivo] Gerar plano adaptativo com demanda desequilibrada

- **Objetivo:** Verificar que a via mais carregada recebe mais verde, respeitando os limites de segurança.
- **Requisito:** PBI-11 · **Prioridade:** 1
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados. · Executar este caso em até 5 minutos entre o envio das leituras e o recálculo.
- **Inputs:** SPO002-V1: 140 veículos, fila 25 · SPO002-V2: 15 veículos, fila 2 · SPO002-V3: 10 veículos, fila 1
- **Outputs esperados:** Plano com modo "Adaptativo" · Maior verde na via "Av. Faria Lima sentido Pinheiros" (60 s ou mais) · Demais vias com 15 s; todos os verdes entre 15 e 90 s · Justificativa citando o método de Webster

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Expandir POST /api/auth/sensores/token, clicar em "Try it out", informar {"codigo": "SPO002-V1", "segredo": "sensor-demo-local-2026"} e clicar em "Execute". | A API retorna HTTP 200 com "accessToken" e "perfil": "Sensor". |
| 3 | Registrar o token do sensor em "Authorize" (substituindo qualquer token anterior com "Logout"). | O Swagger passa a enviar o token do sensor nas requisições. |
| 4 | Executar POST /api/telemetria/leituras com {"leituraId": "qa-plano-v1", "coletadaEm": "[data/hora UTC atual, ex.: 2026-11-19T18:00:00Z]", "veiculosContados": 140, "tamanhoFila": 25}. | HTTP 201. |
| 5 | Obter o token do SPO002-V2 (POST /api/auth/sensores/token), registrar em Authorize e enviar {"leituraId": "qa-plano-v2", "coletadaEm": "[data/hora UTC atual, ex.: 2026-11-19T18:00:00Z]", "veiculosContados": 15, "tamanhoFila": 2}. | HTTP 201. |
| 6 | Obter o token do SPO002-V3, registrar em Authorize e enviar {"leituraId": "qa-plano-v3", "coletadaEm": "[data/hora UTC atual, ex.: 2026-11-19T18:00:00Z]", "veiculosContados": 10, "tamanhoFila": 1}. | HTTP 201. |
| 7 | Em "Autenticação", expandir POST /api/auth/login, clicar em "Try it out", informar o corpo {"email": "gestor@movesync.dev", "senha": "Gestor@2026"} e clicar em "Execute". | A API retorna HTTP 200 com "accessToken", "tokenType": "Bearer" e "perfil": "Gestor". |
| 8 | Copiar o valor de "accessToken" do gestor, clicar no botão "Authorize" (cadeado), colar o token e confirmar em "Authorize" e "Close". | O cadeado dos endpoints protegidos aparece fechado, indicando que o token será enviado nas próximas requisições. |
| 9 | Expandir GET /api/cruzamentos, clicar em "Try it out", informar busca = "SPO-002" e clicar em "Execute". Anotar o "id" do cruzamento e os "id" das vias. | A API retorna HTTP 200 com o cruzamento SPO-002, suas vias e respectivos identificadores. |
| 10 | Executar POST /api/cruzamentos/{id}/planos/recalcular. | A API retorna HTTP 200 com o plano. |
| 11 | Analisar o plano. | modo "Adaptativo"; a fase "Av. Faria Lima sentido Pinheiros" tem o maior verdeSegundos (60 s ou mais); as demais têm 15 s; justificativa menciona "Webster" e a participação de cada via na demanda. |

#### CT-F06-02 - [Positivo] Entrar em contingência quando faltam dados

- **Objetivo:** Verificar o fallback seguro quando sensores não enviam dados.
- **Requisito:** PBI-11 · **Prioridade:** 1
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados. · Cruzamento SPO-001 sem leituras nos últimos 5 minutos.
- **Inputs:** Recálculo do SPO-001
- **Outputs esperados:** Plano com modo "Contingencia" · 30 s de verde em todas as 4 vias · Justificativa listando as vias sem dados

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Em "Autenticação", expandir POST /api/auth/login, clicar em "Try it out", informar o corpo {"email": "gestor@movesync.dev", "senha": "Gestor@2026"} e clicar em "Execute". | A API retorna HTTP 200 com "accessToken", "tokenType": "Bearer" e "perfil": "Gestor". |
| 3 | Copiar o valor de "accessToken" do gestor, clicar no botão "Authorize" (cadeado), colar o token e confirmar em "Authorize" e "Close". | O cadeado dos endpoints protegidos aparece fechado, indicando que o token será enviado nas próximas requisições. |
| 4 | Expandir GET /api/cruzamentos, clicar em "Try it out", informar busca = "SPO-001" e clicar em "Execute". Anotar o "id" do cruzamento e os "id" das vias. | A API retorna HTTP 200 com o cruzamento SPO-001, suas vias e respectivos identificadores. |
| 5 | Executar POST /api/cruzamentos/{id}/planos/recalcular. | A API retorna HTTP 200. |
| 6 | Analisar o plano. | modo "Contingencia"; as 4 fases com verdeSegundos 30; a justificativa informa "Modo de contingência" e cita as vias sem leituras. |

#### CT-F06-03 - [Negativo] Recusar modo de operação inexistente

- **Objetivo:** Verificar a validação do modo.
- **Requisito:** PBI-12 · **Prioridade:** 3
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados.
- **Inputs:** {"modo": "Turbo"} · {} (sem modo)
- **Outputs esperados:** HTTP 400 em ambos os casos

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Em "Autenticação", expandir POST /api/auth/login, clicar em "Try it out", informar o corpo {"email": "gestor@movesync.dev", "senha": "Gestor@2026"} e clicar em "Execute". | A API retorna HTTP 200 com "accessToken", "tokenType": "Bearer" e "perfil": "Gestor". |
| 3 | Copiar o valor de "accessToken" do gestor, clicar no botão "Authorize" (cadeado), colar o token e confirmar em "Authorize" e "Close". | O cadeado dos endpoints protegidos aparece fechado, indicando que o token será enviado nas próximas requisições. |
| 4 | Expandir GET /api/cruzamentos, clicar em "Try it out", informar busca = "SPO-002" e clicar em "Execute". Anotar o "id" do cruzamento e os "id" das vias. | A API retorna HTTP 200 com o cruzamento SPO-002, suas vias e respectivos identificadores. |
| 5 | Executar PATCH /api/cruzamentos/{id}/modo com {"modo": "Turbo"}. | A API retorna HTTP 400 (valor inválido). |
| 6 | Executar PATCH /api/cruzamentos/{id}/modo com {}. | A API retorna HTTP 400 com a mensagem "Informe o modo: Adaptativo ou TempoFixo.". |

### F07 - Prioridade para veículos de emergência

#### CT-F07-01 - [Positivo] Acionar corredor verde para ambulância

- **Objetivo:** Verificar a priorização da via do veículo de emergência.
- **Requisito:** PBI-13 · **Prioridade:** 1
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados. · Nenhuma emergência ativa no SPO-001.
- **Inputs:** Idempotency-Key: [UUID novo] · {"viaId": "[via R. da Consolação sentido Centro]", "tipoVeiculo": "Ambulancia", "duracaoSegundos": 120}
- **Outputs esperados:** HTTP 201 com Location /api/emergencias/{id} · ativa true, expiraEm = acionadoEm + 120 s · planoAplicado.modo "Emergencia": via escolhida com 90 s e demais com 15 s

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Em "Autenticação", expandir POST /api/auth/login, clicar em "Try it out", informar o corpo {"email": "gestor@movesync.dev", "senha": "Gestor@2026"} e clicar em "Execute". | A API retorna HTTP 200 com "accessToken", "tokenType": "Bearer" e "perfil": "Gestor". |
| 3 | Copiar o valor de "accessToken" do gestor, clicar no botão "Authorize" (cadeado), colar o token e confirmar em "Authorize" e "Close". | O cadeado dos endpoints protegidos aparece fechado, indicando que o token será enviado nas próximas requisições. |
| 4 | Expandir GET /api/cruzamentos, clicar em "Try it out", informar busca = "SPO-001" e clicar em "Execute". Anotar o "id" do cruzamento e os "id" das vias. | A API retorna HTTP 200 com o cruzamento SPO-001, suas vias e respectivos identificadores. |
| 5 | Expandir POST /api/cruzamentos/{id}/emergencias, preencher Idempotency-Key com um UUID novo e o corpo {"viaId": "[id da via R. da Consolação sentido Centro]", "tipoVeiculo": "Ambulancia", "duracaoSegundos": 120}. Clicar em "Execute". | A API retorna HTTP 201 Created. |
| 6 | Analisar o corpo. | ativa true; planoAplicado.modo "Emergencia"; a fase da via escolhida tem 90 s e as outras 3 têm 15 s; a fase de pedestres foi mantida. |

#### CT-F07-02 - [Negativo] Recusar segunda emergência simultânea no cruzamento

- **Objetivo:** Evitar conflito entre dois corredores verdes no mesmo cruzamento.
- **Requisito:** PBI-13 · **Prioridade:** 2
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados. · CT-F07-01 executado e emergência ainda ativa.
- **Inputs:** Nova Idempotency-Key · Mesmo corpo do CT-F07-01
- **Outputs esperados:** HTTP 409 · codigo "EMERGENCIA_JA_ATIVA"

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Em "Autenticação", expandir POST /api/auth/login, clicar em "Try it out", informar o corpo {"email": "gestor@movesync.dev", "senha": "Gestor@2026"} e clicar em "Execute". | A API retorna HTTP 200 com "accessToken", "tokenType": "Bearer" e "perfil": "Gestor". |
| 3 | Copiar o valor de "accessToken" do gestor, clicar no botão "Authorize" (cadeado), colar o token e confirmar em "Authorize" e "Close". | O cadeado dos endpoints protegidos aparece fechado, indicando que o token será enviado nas próximas requisições. |
| 4 | Expandir GET /api/cruzamentos, clicar em "Try it out", informar busca = "SPO-001" e clicar em "Execute". Anotar o "id" do cruzamento e os "id" das vias. | A API retorna HTTP 200 com o cruzamento SPO-001, suas vias e respectivos identificadores. |
| 5 | Executar POST /api/cruzamentos/{id}/emergencias com uma NOVA Idempotency-Key e o mesmo corpo do CT-F07-01. | A API retorna HTTP 409 com codigo "EMERGENCIA_JA_ATIVA". |

#### CT-F07-03 - [Positivo] Encerrar emergência e voltar ao plano normal

- **Objetivo:** Verificar o retorno à operação normal.
- **Requisito:** PBI-14 · **Prioridade:** 1
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados. · Emergência ativa do CT-F07-01 (id anotado).
- **Inputs:** id do acionamento
- **Outputs esperados:** HTTP 200 com ativa false e encerradoEm · planoAplicado com modo diferente de Emergencia · Segundo encerramento: HTTP 409

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Em "Autenticação", expandir POST /api/auth/login, clicar em "Try it out", informar o corpo {"email": "gestor@movesync.dev", "senha": "Gestor@2026"} e clicar em "Execute". | A API retorna HTTP 200 com "accessToken", "tokenType": "Bearer" e "perfil": "Gestor". |
| 3 | Copiar o valor de "accessToken" do gestor, clicar no botão "Authorize" (cadeado), colar o token e confirmar em "Authorize" e "Close". | O cadeado dos endpoints protegidos aparece fechado, indicando que o token será enviado nas próximas requisições. |
| 4 | Executar POST /api/emergencias/{id}/encerrar. | A API retorna HTTP 200 com ativa false, encerradoEm preenchido e planoAplicado em modo diferente de "Emergencia". |
| 5 | Executar novamente POST /api/emergencias/{id}/encerrar. | A API retorna HTTP 409 com codigo "EMERGENCIA_JA_ENCERRADA". |

### F08 - Status do cruzamento e tempo de espera

#### CT-F08-01 - [Positivo] Consultar status com espera estimada sem login

- **Objetivo:** Verificar a principal informação entregue ao cidadão.
- **Requisito:** PBI-15 · **Prioridade:** 1
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados. · CT-F06-01 executado há menos de 5 minutos (SPO-002 com dados).
- **Inputs:** id do SPO-002 · Nenhum token
- **Outputs esperados:** HTTP 200 · modo "Adaptativo" e situacao descritiva · 3 vias com verdeSegundos, esperaMediaSegundos, esperaMaximaSegundos e nivel · travessia com tempoTravessiaSegundos e esperaMediaSegundos

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Expandir GET /api/cruzamentos, clicar em "Try it out", informar busca = "SPO-002" e clicar em "Execute". Anotar o "id" do cruzamento e os "id" das vias. | A API retorna HTTP 200 com o cruzamento SPO-002, suas vias e respectivos identificadores. |
| 3 | Sem token, executar GET /api/cruzamentos/{id}/status. | A API retorna HTTP 200 OK. |
| 4 | Analisar o corpo. | modo "Adaptativo", situacao "Semáforo inteligente...", 3 vias com espera média e máxima e nivel (Livre/Moderado/Intenso); travessia com tempo e espera. |
| 5 | Conferir a via com mais verde. | A via "Av. Faria Lima sentido Pinheiros" tem a menor espera média entre as vias. |

#### CT-F08-02 - [Positivo] Status em contingência exibe aviso

- **Objetivo:** Verificar a comunicação ao cidadão quando faltam dados.
- **Requisito:** PBI-16 · **Prioridade:** 2
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados. · SPO-001 sem leituras nos últimos 5 minutos e sem emergência ativa.
- **Inputs:** id do SPO-001
- **Outputs esperados:** modo "Contingencia" · avisos contém "Sensores sem dados recentes..." · nivel "Indeterminado" nas vias

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Expandir GET /api/cruzamentos, clicar em "Try it out", informar busca = "SPO-001" e clicar em "Execute". Anotar o "id" do cruzamento e os "id" das vias. | A API retorna HTTP 200 com o cruzamento SPO-001, suas vias e respectivos identificadores. |
| 3 | Executar GET /api/cruzamentos/{id}/status. | HTTP 200. |
| 4 | Analisar modo, avisos e vias. | modo "Contingencia"; avisos informa que os sensores estão sem dados e o semáforo opera em tempo fixo seguro; nivel "Indeterminado". |

#### CT-F08-03 - [Negativo] Status de cruzamento inexistente

- **Objetivo:** Verificar a resposta para id desconhecido.
- **Requisito:** PBI-15 · **Prioridade:** 3
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados.
- **Inputs:** id 00000000-0000-0000-0000-000000000001
- **Outputs esperados:** HTTP 404

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Executar GET /api/cruzamentos/00000000-0000-0000-0000-000000000001/status. | A API retorna HTTP 404 com codigo "NAO_ENCONTRADO". |

### F09 - Travessia acessível (botoeira virtual)

#### CT-F09-01 - [Positivo] Solicitar travessia acessível

- **Objetivo:** Verificar a ampliação do tempo de travessia para quem precisa.
- **Requisito:** PBI-17 · **Prioridade:** 1
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados. · Cidadão recém-cadastrado, sem solicitação vigente no SPO-002 (travessia de 14 m).
- **Inputs:** Idempotency-Key: [UUID novo] · {"motivo": "MobilidadeReduzida"}
- **Outputs esperados:** HTTP 201 · tempoTravessiaSegundos 22 e tempoTravessiaPadraoSegundos 16 · expiraEm = solicitadaEm + 5 min · Mensagem de confirmação

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Expandir POST /api/auth/registro, clicar em "Try it out" e cadastrar um cidadão com e-mail inédito (ex.: {"nome": "Tester QA", "email": "qa+[data-hora]@teste.dev", "senha": "Senha123"}). | A API retorna HTTP 201 Created com o perfil "Cidadao". |
| 3 | Executar POST /api/auth/login com o e-mail e a senha cadastrados, copiar o "accessToken" e registrá-lo em "Authorize". | Login retorna HTTP 200 e o token fica configurado no Swagger. |
| 4 | Expandir GET /api/cruzamentos, clicar em "Try it out", informar busca = "SPO-002" e clicar em "Execute". Anotar o "id" do cruzamento e os "id" das vias. | A API retorna HTTP 200 com o cruzamento SPO-002, suas vias e respectivos identificadores. |
| 5 | Executar POST /api/cruzamentos/{id}/travessia-acessivel com Idempotency-Key nova e {"motivo": "MobilidadeReduzida"}. | A API retorna HTTP 201 Created. |
| 6 | Analisar o corpo. | tempoTravessiaSegundos 22 (maior que tempoTravessiaPadraoSegundos 16), expiraEm 5 minutos após solicitadaEm e mensagem de confirmação. |

#### CT-F09-02 - [Positivo] Nova solicitação com pedido vigente retorna a mesma

- **Objetivo:** Verificar a regra de não duplicar solicitações vigentes.
- **Requisito:** PBI-18 · **Prioridade:** 3
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados. · Mesmo cidadão do CT-F09-01, dentro dos 5 minutos.
- **Inputs:** NOVA Idempotency-Key · {"motivo": "MobilidadeReduzida"}
- **Outputs esperados:** HTTP 200 (e não 201) · Mesmo id da solicitação do CT-F09-01 · Mensagem informando que já existe solicitação ativa

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Com o token do cidadão do CT-F09-01, executar POST /api/cruzamentos/{id}/travessia-acessivel com uma NOVA Idempotency-Key. | A API retorna HTTP 200 OK. |
| 3 | Comparar o id com o do CT-F09-01. | O id é o mesmo e a mensagem informa que já existe travessia acessível ativa. |

#### CT-F09-03 - [Negativo] Exigir Idempotency-Key na solicitação

- **Objetivo:** Verificar a proteção contra toques duplicados no app.
- **Requisito:** PBI-17 · **Prioridade:** 2
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados.
- **Inputs:** Idempotency-Key vazio · {"motivo": "PessoaIdosa"}
- **Outputs esperados:** HTTP 400 · codigo "IDEMPOTENCY_KEY_OBRIGATORIA"

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Em "Autenticação", expandir POST /api/auth/login, clicar em "Try it out", informar o corpo {"email": "cidada@movesync.dev", "senha": "Cidada@2026"} e clicar em "Execute". | A API retorna HTTP 200 com "accessToken", "tokenType": "Bearer" e "perfil": "Cidadao". |
| 3 | Copiar o valor de "accessToken" da cidadã, clicar no botão "Authorize" (cadeado), colar o token e confirmar em "Authorize" e "Close". | O cadeado dos endpoints protegidos aparece fechado, indicando que o token será enviado nas próximas requisições. |
| 4 | Expandir GET /api/cruzamentos, clicar em "Try it out", informar busca = "SPO-002" e clicar em "Execute". Anotar o "id" do cruzamento e os "id" das vias. | A API retorna HTTP 200 com o cruzamento SPO-002, suas vias e respectivos identificadores. |
| 5 | Executar POST /api/cruzamentos/{id}/travessia-acessivel com Idempotency-Key "abc" (curta) e {"motivo": "PessoaIdosa"}. | A API retorna HTTP 400 com codigo "IDEMPOTENCY_KEY_OBRIGATORIA". |

### F10 - Ocorrências do cidadão

#### CT-F10-01 - [Positivo] Registrar ocorrência de semáforo apagado

- **Objetivo:** Verificar o registro e a priorização automática como Crítica.
- **Requisito:** PBI-19 · **Prioridade:** 1
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados. · Cidadão recém-cadastrado (sem ocorrências).
- **Inputs:** Idempotency-Key: qa-ocorrencia-0001-[data-hora] · {"cruzamentoId": "[id SPO-001]", "tipo": "SemaforoApagado", "descricao": "Semáforo apagado desde as 7h da manhã."}
- **Outputs esperados:** HTTP 201 com Location /api/ocorrencias/{id} · prioridade "Critica", status "Aberta", nomeCruzamento preenchido

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Expandir POST /api/auth/registro, clicar em "Try it out" e cadastrar um cidadão com e-mail inédito (ex.: {"nome": "Tester QA", "email": "qa+[data-hora]@teste.dev", "senha": "Senha123"}). | A API retorna HTTP 201 Created com o perfil "Cidadao". |
| 3 | Executar POST /api/auth/login com o e-mail e a senha cadastrados, copiar o "accessToken" e registrá-lo em "Authorize". | Login retorna HTTP 200 e o token fica configurado no Swagger. |
| 4 | Expandir GET /api/cruzamentos, clicar em "Try it out", informar busca = "SPO-001" e clicar em "Execute". Anotar o "id" do cruzamento e os "id" das vias. | A API retorna HTTP 200 com o cruzamento SPO-001, suas vias e respectivos identificadores. |
| 5 | Expandir POST /api/ocorrencias, preencher Idempotency-Key com "qa-ocorrencia-0001-[data-hora]" e o corpo {"cruzamentoId": "[id SPO-001]", "tipo": "SemaforoApagado", "descricao": "Semáforo apagado desde as 7h da manhã."}. Clicar em "Execute". | A API retorna HTTP 201 Created com cabeçalho location. |
| 6 | Analisar o corpo. | prioridade "Critica", status "Aberta", nomeCruzamento "Av. Paulista x R. da Consolação" e respostaGestor nula. |

#### CT-F10-02 - [Positivo] Repetir requisição com a mesma Idempotency-Key não duplica

- **Objetivo:** Verificar que um duplo envio do app gera uma única ocorrência.
- **Requisito:** PBI-19 · **Prioridade:** 1
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados. · CT-F10-01 executado (mesma sessão e mesmo token).
- **Inputs:** Mesma Idempotency-Key e mesmo corpo do CT-F10-01
- **Outputs esperados:** HTTP 201 com o MESMO id · Cabeçalho Idempotency-Replayed: true · GET /api/ocorrencias/minhas com totalItens 1

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Executar novamente POST /api/ocorrencias com a mesma Idempotency-Key e o mesmo corpo do CT-F10-01. | A API retorna HTTP 201 com o mesmo corpo (mesmo id). |
| 3 | Analisar os cabeçalhos da resposta. | Existe idempotency-replayed: true. |
| 4 | Executar GET /api/ocorrencias/minhas. | totalItens 1: nenhuma ocorrência duplicada. |

#### CT-F10-03 - [Negativo] Outro cidadão não enxerga nem altera a ocorrência

- **Objetivo:** Verificar a privacidade das ocorrências.
- **Requisito:** PBI-20 · **Prioridade:** 1
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados. · Ocorrência do CT-F10-01 (id anotado).
- **Inputs:** Token da cidadã de demonstração (não autora)
- **Outputs esperados:** GET → HTTP 404 · PUT → HTTP 404 · DELETE → HTTP 404

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Em "Autenticação", expandir POST /api/auth/login, clicar em "Try it out", informar o corpo {"email": "cidada@movesync.dev", "senha": "Cidada@2026"} e clicar em "Execute". | A API retorna HTTP 200 com "accessToken", "tokenType": "Bearer" e "perfil": "Cidadao". |
| 3 | Copiar o valor de "accessToken" da cidadã, clicar no botão "Authorize" (cadeado), colar o token e confirmar em "Authorize" e "Close". | O cadeado dos endpoints protegidos aparece fechado, indicando que o token será enviado nas próximas requisições. |
| 4 | Executar GET /api/ocorrencias/{id} com o id do CT-F10-01. | A API retorna HTTP 404 (não revela a existência). |
| 5 | Executar PUT /api/ocorrencias/{id} com corpo válido. | A API retorna HTTP 404. |
| 6 | Executar DELETE /api/ocorrencias/{id}. | A API retorna HTTP 404. |

#### CT-F10-04 - [Positivo] Gestor analisa e resolve a ocorrência

- **Objetivo:** Verificar o fluxo completo de atendimento.
- **Requisito:** PBI-20 · **Prioridade:** 1
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados. · Ocorrência Aberta do CT-F10-01.
- **Inputs:** PATCH {"status": "EmAnalise"} · PATCH {"status": "Resolvida", "resposta": "Equipe religou o semáforo às 9h."}
- **Outputs esperados:** Status EmAnalise (200) · Status Resolvida com respostaGestor (200) · Autor vê a resposta em /minhas

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Em "Autenticação", expandir POST /api/auth/login, clicar em "Try it out", informar o corpo {"email": "gestor@movesync.dev", "senha": "Gestor@2026"} e clicar em "Execute". | A API retorna HTTP 200 com "accessToken", "tokenType": "Bearer" e "perfil": "Gestor". |
| 3 | Copiar o valor de "accessToken" do gestor, clicar no botão "Authorize" (cadeado), colar o token e confirmar em "Authorize" e "Close". | O cadeado dos endpoints protegidos aparece fechado, indicando que o token será enviado nas próximas requisições. |
| 4 | Executar PATCH /api/ocorrencias/{id}/status com {"status": "EmAnalise"}. | HTTP 200 com status "EmAnalise". |
| 5 | Executar PATCH /api/ocorrencias/{id}/status com {"status": "Resolvida", "resposta": "Equipe religou o semáforo às 9h."}. | HTTP 200 com status "Resolvida" e respostaGestor preenchida. |
| 6 | Com o token do autor, executar GET /api/ocorrencias/minhas. | A ocorrência aparece Resolvida com a resposta do gestor. |

### F11 - Painel do gestor, indicadores e relatório de impacto

#### CT-F11-01 - [Positivo] Consultar painel operacional

- **Objetivo:** Verificar os totais exibidos ao gestor.
- **Requisito:** PBI-21 · **Prioridade:** 2
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados. · Cenário com ao menos 1 ocorrência pendente e sensores sem dados.
- **Inputs:** Token do gestor
- **Outputs esperados:** HTTP 200 com cruzamentosAtivos, planosVigentesPorModo, sensoresAtivos, sensoresOffline e codigosSensoresOffline, leiturasUltimaHora, ocorrenciasPendentesPorPrioridade, emergenciasAtivas

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Em "Autenticação", expandir POST /api/auth/login, clicar em "Try it out", informar o corpo {"email": "gestor@movesync.dev", "senha": "Gestor@2026"} e clicar em "Execute". | A API retorna HTTP 200 com "accessToken", "tokenType": "Bearer" e "perfil": "Gestor". |
| 3 | Copiar o valor de "accessToken" do gestor, clicar no botão "Authorize" (cadeado), colar o token e confirmar em "Authorize" e "Close". | O cadeado dos endpoints protegidos aparece fechado, indicando que o token será enviado nas próximas requisições. |
| 4 | Executar GET /api/painel/resumo. | A API retorna HTTP 200. |
| 5 | Analisar o corpo. | Todos os indicadores estão presentes; codigosSensoresOffline lista os sensores sem dados recentes; geradoEm é o horário atual (UTC). |

#### CT-F11-02 - [Positivo] Indicadores mostram ganho do plano adaptativo

- **Objetivo:** Verificar a medição de impacto da solução.
- **Requisito:** PBI-22 · **Prioridade:** 1
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados. · Leituras desbalanceadas enviadas há menos de 5 minutos para as 3 vias do SPO-002 (CT-F06-01).
- **Inputs:** id do SPO-002
- **Outputs esperados:** dadosSuficientes true · comparacao.adaptativo.atrasoMedioSegundosPorVeiculo < comparacao.tempoFixo.atrasoMedioSegundosPorVeiculo · reducaoAtrasoPercentual > 0 e co2EvitadoKgPorHora > 0 · interpretacao em linguagem simples

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Em "Autenticação", expandir POST /api/auth/login, clicar em "Try it out", informar o corpo {"email": "gestor@movesync.dev", "senha": "Gestor@2026"} e clicar em "Execute". | A API retorna HTTP 200 com "accessToken", "tokenType": "Bearer" e "perfil": "Gestor". |
| 3 | Copiar o valor de "accessToken" do gestor, clicar no botão "Authorize" (cadeado), colar o token e confirmar em "Authorize" e "Close". | O cadeado dos endpoints protegidos aparece fechado, indicando que o token será enviado nas próximas requisições. |
| 4 | Expandir GET /api/cruzamentos, clicar em "Try it out", informar busca = "SPO-002" e clicar em "Execute". Anotar o "id" do cruzamento e os "id" das vias. | A API retorna HTTP 200 com o cruzamento SPO-002, suas vias e respectivos identificadores. |
| 5 | Executar GET /api/cruzamentos/{id}/indicadores. | A API retorna HTTP 200. |
| 6 | Analisar comparacao e interpretacao. | Atraso médio adaptativo menor que o do tempo fixo; reducaoAtrasoPercentual positiva (≈ 40%); horas de espera e CO₂ evitados positivos; interpretação explica o resultado. |

#### CT-F11-03 - [Negativo] Painel e indicadores restritos ao gestor

- **Objetivo:** Verificar a proteção das informações gerenciais.
- **Requisito:** PBI-21 · **Prioridade:** 2
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados.
- **Inputs:** Sem token · Token da cidadã
- **Outputs esperados:** Sem token → 401 · Cidadã → 403

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Expandir GET /api/cruzamentos, clicar em "Try it out", informar busca = "SPO-002" e clicar em "Execute". Anotar o "id" do cruzamento e os "id" das vias. | A API retorna HTTP 200 com o cruzamento SPO-002, suas vias e respectivos identificadores. |
| 3 | Sem token, executar GET /api/painel/resumo e GET /api/cruzamentos/{id}/indicadores. | Ambos retornam HTTP 401. |
| 4 | Em "Autenticação", expandir POST /api/auth/login, clicar em "Try it out", informar o corpo {"email": "cidada@movesync.dev", "senha": "Cidada@2026"} e clicar em "Execute". | A API retorna HTTP 200 com "accessToken", "tokenType": "Bearer" e "perfil": "Cidadao". |
| 5 | Copiar o valor de "accessToken" da cidadã, clicar no botão "Authorize" (cadeado), colar o token e confirmar em "Authorize" e "Close". | O cadeado dos endpoints protegidos aparece fechado, indicando que o token será enviado nas próximas requisições. |
| 6 | Repetir as duas consultas. | Ambas retornam HTTP 403. |

#### CT-F11-04 - [Positivo] Relatório de impacto projeta benefício/custo para a prefeitura

- **Objetivo:** Verificar que o relatório converte o ganho medido em horas, CO₂, reais e relação benefício/custo, e que a simulação de cenário altera apenas o cálculo.
- **Requisito:** PBI-23 · **Prioridade:** 1
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados. · Leituras desbalanceadas enviadas há menos de 5 minutos para as 3 vias do SPO-002 (CT-F06-01).
- **Inputs:** Token do gestor · Sem parâmetros (premissas padrão) · horasReferenciaPorDia = 6
- **Outputs esperados:** HTTP 200 com cruzamentosComDados ≥ 1 · resumo.horasEvitadasPorMes, resumo.valorTempoEconomizadoMesReais e resumo.relacaoBeneficioCusto maiores que 0 · premissas.valorHoraCidadaoReais = 21.15 e premissas.horasReferenciaPorDia = 10 · Com horasReferenciaPorDia = 6: premissas.horasReferenciaPorDia = 6 e horasEvitadasPorMes menor que na consulta padrão

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Em "Autenticação", expandir POST /api/auth/login, clicar em "Try it out", informar o corpo {"email": "gestor@movesync.dev", "senha": "Gestor@2026"} e clicar em "Execute". | A API retorna HTTP 200 com "accessToken", "tokenType": "Bearer" e "perfil": "Gestor". |
| 3 | Copiar o valor de "accessToken" do gestor, clicar no botão "Authorize" (cadeado), colar o token e confirmar em "Authorize" e "Close". | O cadeado dos endpoints protegidos aparece fechado, indicando que o token será enviado nas próximas requisições. |
| 4 | Em "Painel do gestor", expandir GET /api/painel/impacto, clicar em "Try it out" (sem preencher parâmetros) e clicar em "Execute". | A API retorna HTTP 200. |
| 5 | Analisar "resumo", "premissas", "cruzamentos" e "interpretacao". Anotar resumo.horasEvitadasPorMes. | cruzamentosComDados ≥ 1; SPO-002 aparece com dadosSuficientes = true; horasEvitadasPorMes, valorTempoEconomizadoMesReais e relacaoBeneficioCusto são positivos; assinaturaMensalReais = número de cruzamentos ativos × 590; a interpretação cita as premissas (h/dia e dias/mês) e explica que é benefício social. |
| 6 | Executar novamente com horasReferenciaPorDia = 6. | A API retorna HTTP 200 com premissas.horasReferenciaPorDia = 6 e horasEvitadasPorMes menor que o valor anotado. |
| 7 | Executar mais uma vez sem parâmetros. | premissas.horasReferenciaPorDia volta a 10: a simulação não alterou a configuração. |

#### CT-F11-05 - [Negativo] Relatório de impacto rejeita simulação inválida e acesso indevido

- **Objetivo:** Verificar a validação dos parâmetros de simulação e a restrição do relatório ao gestor.
- **Requisito:** PBI-23 · **Prioridade:** 2
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados.
- **Inputs:** horasReferenciaPorDia = 25 · valorHoraReais = 0 · Token da cidadã
- **Outputs esperados:** horasReferenciaPorDia = 25 → HTTP 400 (ProblemDetails) com a mensagem "As horas de referência por dia devem estar entre 1 e 24." · valorHoraReais = 0 → HTTP 400 com a mensagem do valor da hora · Cidadã → HTTP 403

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Em "Autenticação", expandir POST /api/auth/login, clicar em "Try it out", informar o corpo {"email": "gestor@movesync.dev", "senha": "Gestor@2026"} e clicar em "Execute". | A API retorna HTTP 200 com "accessToken", "tokenType": "Bearer" e "perfil": "Gestor". |
| 3 | Copiar o valor de "accessToken" do gestor, clicar no botão "Authorize" (cadeado), colar o token e confirmar em "Authorize" e "Close". | O cadeado dos endpoints protegidos aparece fechado, indicando que o token será enviado nas próximas requisições. |
| 4 | Expandir GET /api/painel/impacto, clicar em "Try it out", informar horasReferenciaPorDia = 25 e clicar em "Execute". | A API retorna HTTP 400 em ProblemDetails; "errors" contém "As horas de referência por dia devem estar entre 1 e 24.". |
| 5 | Limpar o campo anterior, informar valorHoraReais = 0 e executar. | A API retorna HTTP 400; "errors" contém "O valor da hora deve estar entre R$ 0,01 e R$ 1.000,00.". |
| 6 | Em "Autenticação", expandir POST /api/auth/login, clicar em "Try it out", informar o corpo {"email": "cidada@movesync.dev", "senha": "Cidada@2026"} e clicar em "Execute". | A API retorna HTTP 200 com "accessToken", "tokenType": "Bearer" e "perfil": "Cidadao". |
| 7 | Copiar o valor de "accessToken" da cidadã, clicar no botão "Authorize" (cadeado), colar o token e confirmar em "Authorize" e "Close". | O cadeado dos endpoints protegidos aparece fechado, indicando que o token será enviado nas próximas requisições. |
| 8 | Executar GET /api/painel/impacto sem parâmetros. | A API retorna HTTP 403: o relatório é exclusivo do gestor. |

### F12 - Proteção, observabilidade e documentação da API

#### CT-F12-01 - [Positivo] Health check completo da solução

- **Objetivo:** Verificar o monitoramento de API, banco, sensores e serviço externo.
- **Requisito:** PBI-25 · **Prioridade:** 1
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados.
- **Inputs:** GET /health
- **Outputs esperados:** HTTP 200 · verificacoes: api, banco-de-dados (Healthy, com latenciaMs), sensores-iot (Healthy ou Degraded com códigos offline), servico-externo-nucleo-java

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/health no navegador. | A API retorna HTTP 200 com JSON. |
| 2 | Analisar "verificacoes". | Existem as verificações api, banco-de-dados, sensores-iot e servico-externo-nucleo-java, cada uma com status, descrição e duração. |
| 3 | Analisar banco-de-dados e sensores-iot. | banco-de-dados Healthy com latência; sensores-iot Degraded caso haja sensores sem dados, listando os códigos. |

#### CT-F12-02 - [Negativo] Bloquear força bruta no login (HTTP 429)

- **Objetivo:** Verificar o rate limiting por IP no login.
- **Requisito:** PBI-24 · **Prioridade:** 1
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados. · Aguardar 1 minuto sem tentativas de login a partir da máquina do testador.
- **Inputs:** 11 tentativas de login com senha errada em menos de 1 minuto
- **Outputs esperados:** Tentativas 1–10: HTTP 401 · 11ª: HTTP 429 com cabeçalho Retry-After e codigo "LIMITE_REQUISICOES"

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 2 | Executar POST /api/auth/login com {"email": "gestor@movesync.dev", "senha": "Errada123"} 10 vezes seguidas. | As 10 respostas são HTTP 401. |
| 3 | Executar a 11ª tentativa imediatamente. | A API retorna HTTP 429 Too Many Requests. |
| 4 | Analisar cabeçalhos e corpo. | Cabeçalho retry-after presente; codigo "LIMITE_REQUISICOES" e tenteNovamenteEmSegundos no corpo. |
| 5 | Aguardar o tempo indicado e tentar login com a senha correta. | A API volta a responder HTTP 200. |

#### CT-F12-03 - [Positivo] Documentação OpenAPI e Swagger completos

- **Objetivo:** Verificar a documentação interativa da API.
- **Requisito:** PBI-25 · **Prioridade:** 2
- **Preparação:** API publicada e acessível; banco de dados com os dados de demonstração (seed) carregados.
- **Inputs:** /openapi/v1.json e /swagger
- **Outputs esperados:** Documento com título "MoveSync API", esquema Bearer e parâmetro Idempotency-Key · Cadeado somente nos endpoints protegidos

| # | Ação do testador | Resposta esperada |
|---|---|---|
| 1 | Acessar [URL da API]/openapi/v1.json. | HTTP 200 com o documento OpenAPI contendo "MoveSync API", components.securitySchemes.Bearer e o parâmetro Idempotency-Key. |
| 2 | Acessar [URL da API]/swagger no navegador. | A página do Swagger UI é exibida com o título "MoveSync API" e a lista de endpoints agrupados por tags. |
| 3 | Observar POST /api/auth/login, POST /api/auth/registro e GET /api/cruzamentos. | Não exibem cadeado (são públicos). |
| 4 | Observar POST /api/ocorrencias. | Exibe cadeado, descrição, exemplo de corpo, parâmetro de cabeçalho Idempotency-Key e respostas 201/400/404/409/422. |
