# 💼 MoveSync — Modelo de negócio

> **Resumo em uma frase:** a prefeitura paga uma assinatura mensal por cruzamento para ter semáforos que se ajustam à demanda, e a própria API mostra, com a telemetria real, quanto tempo de espera e quanto CO₂ isso devolve à cidade.

Este documento explica **quem paga, quanto, por quê e como a prefeitura comprova o retorno**. Os números marcados como **hipótese** são estimativas do grupo para o plano de negócio. Eles ficam na seção `ModeloNegocio` do `appsettings.json` e podem ser simulados no endpoint `GET /api/painel/impacto`; não são cotações reais.

---

## 1. Problema de mercado

- **Congestionamento custa caro.** Um estudo da FIRJAN estimou o custo dos congestionamentos nas regiões metropolitanas do Rio e de São Paulo em **R$ 98 bilhões por ano** (dados de 2013). [1]
- **Tempo fixo desatualizado é uma causa conhecida.** A FHWA (EUA) aponta que planos semafóricos desatualizados respondem por **mais de 10% do atraso** em vias principais, e que o controle adaptativo melhora o tempo de viagem em **mais de 10% em média**, chegando a **50% ou mais** onde os tempos estavam muito desatualizados. [2]
- **A solução adaptativa existe, mas é cara.** Sistemas como SCATS e InSync custam de **US$ 8 mil a US$ 35 mil por cruzamento** só na instalação (SCATS: US$ 25–30 mil; InSync: US$ 25–35 mil; ACS-Lite: US$ 8–12 mil). [3] Esse preço exclui a maioria das cidades pequenas e médias.
- **O brasileiro passa muito tempo no deslocamento.** O Censo 2022 mostrou **1,3 milhão de trabalhadores que levam mais de 2 horas** para chegar ao trabalho, e o **carro é o principal meio** de deslocamento (32,3%). [4]

## 2. Proposta de valor

| Para quem | O que entregamos |
|---|---|
| **Prefeitura / órgão de trânsito** (cliente pagante) | Semáforo adaptativo por uma fração do custo dos sistemas tradicionais, sem trocar o controlador inteiro; painel operacional; histórico auditável de cada plano; **relatório de impacto e benefício/custo** para prestar contas à câmara e à população |
| **Cidadão** (usuário, gratuito) | Menos tempo parado; tempo estimado de espera no app; **travessia acessível** (pessoas com deficiência, idosos, gestantes); canal de ocorrências com acompanhamento |
| **Serviços de emergência** | Corredor verde acionado pela central |
| **Meio ambiente** | Menos tempo em marcha lenta, menos CO₂ (ODS 13) |

**Diferenciais:** hardware de baixo custo (ESP32 + laço indutivo ou câmera comum), modo de contingência seguro (volta ao tempo fixo se o sensor falhar), acessibilidade do pedestre e transparência (cada decisão vem com justificativa em linguagem simples).

## 3. Business Model Canvas

| Bloco | MoveSync |
|---|---|
| **Segmento de clientes** | Prefeituras de cidades **médias** (100 mil a 500 mil habitantes) e consórcios intermunicipais de cidades menores |
| **Proposta de valor** | Ver seção 2 |
| **Canais** | Piloto gratuito de 1 cruzamento → apresentação dos resultados medidos → contratação; eventos de cidades inteligentes; associações de municípios |
| **Relacionamento** | Assinatura com suporte; relatório de impacto mensal gerado pela API; canal de ocorrências do cidadão como termômetro de satisfação |
| **Fontes de receita** | (1) **Implantação** por cruzamento; (2) **assinatura mensal** por cruzamento (SaaS: nuvem, atualizações, suporte, relatórios) |
| **Recursos-chave** | Motor semafórico (Webster + regras de segurança), API .NET, núcleo Java, app do cidadão, kit IoT, equipe de engenharia de tráfego |
| **Atividades-chave** | Instalação e calibração dos sensores; operação da plataforma em nuvem; manutenção; melhoria contínua do algoritmo |
| **Parcerias-chave** | Prefeituras e órgãos de trânsito (autorização e infraestrutura); instaladores locais; fornecedores de hardware; provedor de nuvem (Azure) e de conectividade |
| **Estrutura de custos** | Kit de hardware, instalação, nuvem, conectividade, suporte e manutenção, desenvolvimento |

## 4. Precificação (hipóteses)

| Item | Valor por cruzamento | Observação |
|---|---|---|
| **Piloto** | R$ 0 | 1 cruzamento por 60 dias para provar o ganho com dados reais |
| **Implantação** | **R$ 4.800** (uma vez) | Cobre kit, instalação e calibração. Mesmo a um câmbio de R$ 4,80/US$, fica **abaixo de US$ 1.000**, contra US$ 8 mil a US$ 35 mil dos sistemas tradicionais [3] |
| **Assinatura** | **R$ 590/mês** | Nuvem, monitoramento dos sensores, atualizações, suporte e relatórios |

**Custo mensal estimado por cruzamento (hipótese, a validar no piloto):**

| Custo | R$/mês |
|---|---|
| Conectividade IoT (chip 4G ou rede da prefeitura) | 30 |
| Nuvem (rateio de API, banco e mensageria) | 60 |
| Suporte e manutenção (rateio de equipe e deslocamento) | 150 |
| **Total** | **240** |
| **Margem de contribuição** | **R$ 350 (≈ 59%)** |

> O preço do kit (ESP32, câmera, caixa e fonte) **não foi cotado**; o grupo trata como estimativa até validar com fornecedores.

**Exemplo de contrato** — cidade média com **20 cruzamentos** semaforizados críticos:

| | Valor |
|---|---|
| Implantação | 20 × R$ 4.800 = **R$ 96.000** |
| Assinatura | 20 × R$ 590 = **R$ 11.800/mês** (R$ 141.600/ano) |

## 5. Como a prefeitura comprova o retorno (a API faz a conta)

O endpoint **`GET /api/painel/impacto`** (perfil Gestor) transforma a telemetria em números de gestão pública:

1. Para cada cruzamento ativo, compara **tempo fixo × adaptativo** com a demanda real dos últimos 5 minutos (atraso médio pela fórmula de Webster).
2. Converte a diferença em **horas de espera evitadas** e **CO₂ evitado** (0,5 g/s de CO₂ por veículo parado; conservador frente aos ~1,1 L/h de combustível de um carro em marcha lenta [5]).
3. Projeta o mês com as premissas: **10 h/dia de referência**, **22 dias/mês** e **R$ 21,15 por hora** do cidadão (rendimento médio de R$ 3.722/mês, IBGE PNAD 1º tri/2026 [6], dividido por 176 h).
4. Calcula a **relação benefício/custo** (valor do tempo devolvido ÷ assinatura) e o **payback social** da implantação.

A prefeitura pode **simular cenários** sem mexer na configuração:

```http
GET /api/painel/impacto?horasReferenciaPorDia=6&valorHoraReais=15
GET /api/painel/impacto?assinaturaMensalPorCruzamentoReais=0   # piloto gratuito
```

### Ponto de equilíbrio

Com as premissas padrão, a assinatura de um cruzamento se paga quando ele evita

```
R$ 590 ÷ (10 h × 22 dias × R$ 21,15) ≈ 0,127 hora de espera por hora ≈ 7,6 minutos
```

ou seja, **basta economizar cerca de 8 minutos de espera somados entre todos os veículos, a cada hora**, para que o benefício social empate com o custo.

### Sensibilidade (por cruzamento, premissas padrão)

| Espera evitada (h por hora) | Horas devolvidas/mês | Valor do tempo/mês | Benefício/custo |
|---|---|---|---|
| 0,127 (equilíbrio) | 28 | R$ 590 | 1,0 |
| 0,5 | 110 | R$ 2.327 | 3,9 |
| 1,0 | 220 | R$ 4.653 | 7,9 |
| 3,0 | 660 | R$ 13.959 | 23,7 |

> **Atenção na apresentação:** a demonstração usa um cenário de **pico** (avenida lotada e transversais vazias), que gera números bem altos ao ser projetado por 10 h/dia. Para defender o negócio, usem a tabela acima e digam que o piloto existe justamente para medir o ganho real de cada cidade.

### Benefício social × caixa da prefeitura

A relação benefício/custo mede **tempo devolvido à população**, não dinheiro que entra no caixa da prefeitura. Essa é a forma usual de avaliar investimento público em mobilidade: a prefeitura justifica o gasto pelo benefício à sociedade (tempo, emissões, segurança), e não por lucro. O relatório da API deixa isso explícito no texto de interpretação.

## 6. Mercado e escala

- **Mercado-alvo:** segundo o Censo 2022, **319 municípios** com mais de 100 mil habitantes concentram **57% da população** [7]; **49** deles têm mais de 500 mil [8]. Sobram cerca de **270 cidades médias** (100 mil a 500 mil), o foco inicial.
- **Estratégia de expansão:** 1 cruzamento (piloto) → corredor principal da cidade → toda a rede semaforizada → consórcios de cidades vizinhas compartilhando a mesma plataforma em nuvem.
- **Contratação pública:** a Lei 14.133/2021 (nova lei de licitações) prevê **ata de registro de preços** e compras por **consórcios públicos**; o Marco Legal das Startups (LC 182/2021) prevê o **contrato público para solução inovadora**, adequado para o piloto.

## 7. Riscos e como tratamos

| Risco | Mitigação |
|---|---|
| Sensor falha ou fica offline | Plano entra em **contingência (tempo fixo seguro)** automaticamente; health check `sensores-iot` alerta o gestor |
| Segurança viária | Verde mínimo de 15 s, amarelo calculado pela velocidade da via, vermelho geral de 2 s, travessia de pedestre sempre garantida |
| Privacidade (LGPD) | A câmera só **conta veículos**; a API recebe números (contagem, fila), não imagens nem placas |
| Ciclo longo de venda pública | Piloto gratuito com relatório de impacto pronto para a prefeitura apresentar |
| Concorrência de grandes fornecedores | Preço por cruzamento muito menor e foco em cidades que eles não atendem |

## 8. Métricas acompanhadas (todas disponíveis na API)

| Métrica | Onde |
|---|---|
| Redução do atraso médio (%) por cruzamento | `GET /api/cruzamentos/{id}/indicadores` |
| Horas de espera e CO₂ evitados por mês, valor do tempo, benefício/custo, payback | `GET /api/painel/impacto` |
| Sensores offline e cruzamentos em contingência | `GET /api/painel/resumo`, `/health/ready` |
| Ocorrências pendentes por prioridade | `GET /api/painel/resumo` |
| Travessia acessível ativa e tempo de travessia garantido ao pedestre | `GET /api/cruzamentos/{id}/status` (campo `travessia`) |

## 9. ODS

**ODS 11** (cidades sustentáveis e acessíveis) · **ODS 9** (infraestrutura e inovação de baixo custo) · **ODS 13** (menos emissões) · **ODS 10** (acessibilidade para pessoas com deficiência).

---

## Fontes

1. Portal do Trânsito — *Custo de congestionamentos no Rio e em São Paulo atinge R$ 98 bilhões* (estudo FIRJAN). https://www.portaldotransito.com.br/noticias/fiscalizacao-e-legislacao/estatisticas/custo-de-congestionamentos-no-rio-e-em-sao-paulo-atinge-r-98-bilhoes-2/
2. FHWA — *Every Day Counts: Adaptive Signal Control Technology*. https://www.fhwa.dot.gov/innovation/everydaycounts/edc-1/asct.cfm
3. U.S. DOT ITS JPO — *Installation of Adaptive Signal Control Technology systems ranges from $8,000 to $35,000 per intersection*. https://www.itskrs.its.dot.gov/2015-sc00355
4. InfoMoney — *1,3 milhão de brasileiros levam mais de duas horas para chegar ao trabalho, diz IBGE*. https://www.infomoney.com.br/?p=3056750
5. Argonne National Laboratory / AFDC — *Idling Reduction* (consumo em marcha lenta). https://afdc.energy.gov/files/pdfs/argonne_idling_handout.pdf
6. Correio da Manhã — *Salário médio do trabalhador chega a R$ 3.722 e bate recorde no primeiro trimestre* (IBGE, PNAD Contínua). https://www.correiodamanha.com.br/economia/2026/04/amp/282055-salario-medio-do-trabalhador-chega-a-rs-3-722-e-bate-recorde-no-primeiro-trimestre.html
7. Jovem Pan — Censo 2022: municípios com mais de 100 mil habitantes. https://jovempan.com.br/?p=60500
8. Folha de Pernambuco — *Brasil tem 49 municípios com mais de 500 mil habitantes, diz IBGE*. https://www.folhape.com.br/noticias/brasil-tem-49-municipios-com-mais-de-500-mil-habitantes-diz-ibge/152597/
