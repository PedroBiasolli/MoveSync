# -*- coding: utf-8 -*-
"""
Gera os artefatos de QA a partir de dados_qa.py:
  qa/01-backlog-azure-boards.csv                -> importar em Boards > Work items > Import from CSV
  qa/02-casos-de-teste/<Feature>.csv           -> importar em Test Plans > suíte da Feature > Import test cases from CSV
  qa/02-casos-de-teste/TODOS.csv                -> todos os casos (alternativa)
  qa/Plano-QA-MoveSync.xlsx                 -> backlog + sumário + passos + rastreabilidade + planilha de execução
  qa/PLANO-DE-TESTES.md                         -> versão legível para revisão da equipe
  qa/Modelo-PDF-Plano-de-Projeto-e-Testes.docx  -> modelo compacto do PDF de entrega (2 prints por linha)

Uso: python qa/fonte/gerar_artefatos_qa.py
     python qa/fonte/gerar_artefatos_qa.py --url-api https://minha-api.azurewebsites.net
     (com --url-api, todo "[URL da API]" dos casos de teste vira a URL real da API publicada)
"""
import csv
import html
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dados_qa import EPICOS  # noqa: E402

RAIZ = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
PASTA_CT = os.path.join(RAIZ, "02-casos-de-teste")


MARCADOR_URL = "[URL da API]"


def aplicar_url_api(valor, url):
    """Troca o marcador [URL da API] pela URL publicada em qualquer texto dos dados (recursivo)."""
    if isinstance(valor, str):
        return valor.replace(MARCADOR_URL, url.rstrip("/"))
    if isinstance(valor, list):
        return [aplicar_url_api(v, url) for v in valor]
    if isinstance(valor, tuple):
        return tuple(aplicar_url_api(v, url) for v in valor)
    if isinstance(valor, dict):
        return {k: aplicar_url_api(v, url) for k, v in valor.items()}
    return valor


def ler_url_api(argv):
    """Lê --url-api da linha de comando (ou a variável MOVESYNC_URL_API)."""
    if "--url-api" in argv:
        indice = argv.index("--url-api")
        if indice + 1 >= len(argv) or not argv[indice + 1].startswith("http"):
            raise SystemExit("Informe a URL completa: --url-api https://minha-api.azurewebsites.net")
        return argv[indice + 1]
    return os.environ.get("MOVESYNC_URL_API", "")


def features():
    for epico in EPICOS:
        for feature in epico["features"]:
            yield epico, feature


def ul(itens):
    return "<ul>" + "".join(f"<li>{html.escape(i)}</li>" for i in itens) + "</ul>"


def titulo_ct(ct):
    return f"{ct['id']} - [{ct['tipo']}] {ct['titulo']}"


def descricao_ct_html(ct, feature):
    return (f"<p><b>Objetivo:</b> {html.escape(ct['objetivo'])}</p>"
            f"<p><b>Tipo:</b> Teste funcional de sistema, caixa-preta, {ct['tipo'].lower()}.</p>"
            f"<p><b>Requisito:</b> {html.escape(feature['titulo'])} / {ct['pbi']}</p>"
            f"<p><b>Preparação (pré-condições):</b></p>{ul(ct['preparacao'])}"
            f"<p><b>Inputs (dados de entrada):</b></p>{ul(ct['inputs'])}"
            f"<p><b>Outputs esperados:</b></p>{ul(ct['outputs'])}")


def criterios_html(criterios):
    return ul(criterios)


# ---------------------------------------------------------------------------
# 1) Backlog para o Azure Boards (hierarquia Epic > Feature > PBI)
# ---------------------------------------------------------------------------
def gerar_backlog_csv():
    caminho = os.path.join(RAIZ, "01-backlog-azure-boards.csv")
    with open(caminho, "w", newline="", encoding="utf-8-sig") as f:
        w = csv.writer(f, quoting=csv.QUOTE_ALL)
        w.writerow(["ID", "Work Item Type", "Title 1", "Title 2", "Title 3", "Description", "Acceptance Criteria", "Priority", "Effort"])
        for epico in EPICOS:
            w.writerow(["", "Epic", epico["titulo"], "", "", f"<p>{html.escape(epico['descricao'])}</p>", "", "", ""])
            for feature in epico["features"]:
                w.writerow(["", "Feature", "", feature["titulo"], "",
                            f"<p>{html.escape(feature['descricao'])}</p>", criterios_html(feature["criterios"]), "", ""])
                for pbi in feature["pbis"]:
                    w.writerow(["", "Product Backlog Item", "", "", f"{pbi['id']} - {pbi['titulo']}",
                                f"<p><b>História de usuário:</b> {html.escape(pbi['historia'])}</p>",
                                criterios_html(pbi["criterios"]), pbi["prioridade"], pbi["esforco"]])
    return caminho


# ---------------------------------------------------------------------------
# 2) Casos de teste para o Azure Test Plans (1 linha por passo)
# ---------------------------------------------------------------------------
CABECALHO_CT = ["ID", "Work Item Type", "Title", "Test Step", "Step Action", "Step Expected", "State", "Priority"]


def linhas_ct(ct):
    for numero, (acao, esperado) in enumerate(ct["passos"], start=1):
        yield ["", "Test Case", titulo_ct(ct), numero, acao, esperado, "Design", ct["prioridade"]]


def gerar_csv_casos():
    os.makedirs(PASTA_CT, exist_ok=True)
    gerados = []
    with open(os.path.join(PASTA_CT, "TODOS.csv"), "w", newline="", encoding="utf-8-sig") as todos:
        wt = csv.writer(todos, quoting=csv.QUOTE_ALL)
        wt.writerow(CABECALHO_CT)
        for _, feature in features():
            nome = feature["titulo"].split(" - ")[0] + ".csv"
            caminho = os.path.join(PASTA_CT, nome)
            with open(caminho, "w", newline="", encoding="utf-8-sig") as f:
                w = csv.writer(f, quoting=csv.QUOTE_ALL)
                w.writerow(CABECALHO_CT)
                for ct in feature["testes"]:
                    for linha in linhas_ct(ct):
                        w.writerow(linha)
                        wt.writerow(linha)
            gerados.append(caminho)
    return gerados


# ---------------------------------------------------------------------------
# 3) Planilha Excel completa
# ---------------------------------------------------------------------------
def gerar_excel():
    from openpyxl import Workbook
    from openpyxl.styles import Alignment, Border, Font, PatternFill, Side
    from openpyxl.utils import get_column_letter
    from openpyxl.worksheet.datavalidation import DataValidation

    wb = Workbook()
    azul = PatternFill("solid", fgColor="1F3A5F")
    epico_fill = PatternFill("solid", fgColor="DCE6F1")
    feature_fill = PatternFill("solid", fgColor="EEF3F8")
    pos_fill = PatternFill("solid", fgColor="E8F5E9")
    neg_fill = PatternFill("solid", fgColor="FDECEA")
    branco = Font(color="FFFFFF", bold=True)
    fino = Side(style="thin", color="C9D3DE")
    borda = Border(left=fino, right=fino, top=fino, bottom=fino)
    quebra = Alignment(wrap_text=True, vertical="top")

    def cabecalho(ws, colunas, larguras):
        ws.append(colunas)
        for i, largura in enumerate(larguras, start=1):
            c = ws.cell(row=1, column=i)
            c.fill, c.font, c.alignment, c.border = azul, branco, Alignment(wrap_text=True, vertical="center"), borda
            ws.column_dimensions[get_column_letter(i)].width = largura
        ws.freeze_panes = "A2"
        ws.auto_filter.ref = f"A1:{get_column_letter(len(colunas))}1"

    def formatar(ws, linha, fill=None):
        for c in ws[linha]:
            c.alignment, c.border = quebra, borda
            if fill:
                c.fill = fill

    # --- Instruções
    ws = wb.active
    ws.title = "Instruções"
    texto = [
        ("MOVESYNC — Plano de Projeto (Scrum) e Plano de Testes", True),
        ("FIAP · Global Solution 2026/2 · Compliance, Quality Assurance & Tests", False),
        ("", False),
        ("Abas desta planilha", True),
        ("Backlog — hierarquia Epic > Feature > Product Backlog Item com descrição, critérios de aceite, prioridade e esforço.", False),
        ("Casos de Teste - Sumário — objetivo, preparação, inputs e outputs esperados de cada caso (aba Summary do Azure).", False),
        ("Casos de Teste - Passos — ação do testador e resposta esperada do sistema, passo a passo (aba Steps do Azure).", False),
        ("Rastreabilidade — quais casos positivos e negativos cobrem cada Feature e cada PBI.", False),
        ("Execução — use durante a homologação para registrar Passou/Falhou (espelha o Test Runner do Azure).", False),
        ("", False),
        ("Convenções", True),
        ("PBI no formato de história de usuário: Como <persona>, quero <ação>, para <benefício>.", False),
        ("Critérios de aceite no formato BDD: Dado <contexto>, quando <ação>, então <resultado>.", False),
        ("Casos de teste funcionais de sistema, caixa-preta, executados pelo Swagger UI da API publicada.", False),
        ("Usuários de teste: gestor@movesync.dev / Gestor@2026 · cidada@movesync.dev / Cidada@2026 · sensores SPO001-V1..V4 e SPO002-V1..V3 com segredo sensor-demo-local-2026.", False),
    ]
    for linha, negrito in texto:
        ws.append([linha])
        ws.cell(row=ws.max_row, column=1).font = Font(bold=negrito, size=14 if ws.max_row == 1 else 11)
    ws.column_dimensions["A"].width = 150

    # --- Backlog
    ws = wb.create_sheet("Backlog")
    cabecalho(ws, ["Tipo", "ID", "Título", "Descrição / História de usuário", "Critérios de aceite", "Prioridade", "Esforço"], [12, 9, 45, 70, 90, 10, 9])
    for epico in EPICOS:
        ws.append(["Epic", epico["id"], epico["titulo"], epico["descricao"], "", "", ""])
        formatar(ws, ws.max_row, epico_fill)
        for feature in epico["features"]:
            ws.append(["Feature", feature["id"], feature["titulo"], feature["descricao"], "\n".join("• " + c for c in feature["criterios"]), "", ""])
            formatar(ws, ws.max_row, feature_fill)
            for pbi in feature["pbis"]:
                ws.append(["PBI", pbi["id"], pbi["titulo"], pbi["historia"], "\n".join("• " + c for c in pbi["criterios"]), pbi["prioridade"], pbi["esforco"]])
                formatar(ws, ws.max_row)

    # --- Sumário dos casos
    ws = wb.create_sheet("Casos de Teste - Sumário")
    cabecalho(ws, ["ID", "Feature", "PBI", "Tipo", "Título", "Objetivo", "Preparação", "Inputs", "Outputs esperados", "Prioridade"],
              [12, 34, 9, 10, 42, 45, 50, 50, 55, 10])
    for _, feature in features():
        for ct in feature["testes"]:
            ws.append([ct["id"], feature["titulo"], ct["pbi"], ct["tipo"], ct["titulo"], ct["objetivo"],
                       "\n".join("• " + x for x in ct["preparacao"]), "\n".join("• " + x for x in ct["inputs"]),
                       "\n".join("• " + x for x in ct["outputs"]), ct["prioridade"]])
            formatar(ws, ws.max_row, pos_fill if ct["tipo"] == "Positivo" else neg_fill)

    # --- Passos
    ws = wb.create_sheet("Casos de Teste - Passos")
    cabecalho(ws, ["ID", "Título", "Passo", "Ação do testador", "Resposta esperada do sistema"], [12, 45, 7, 80, 80])
    for _, feature in features():
        for ct in feature["testes"]:
            for numero, (acao, esperado) in enumerate(ct["passos"], start=1):
                ws.append([ct["id"], titulo_ct(ct) if numero == 1 else "", numero, acao, esperado])
                formatar(ws, ws.max_row, pos_fill if ct["tipo"] == "Positivo" else neg_fill)

    # --- Rastreabilidade
    ws = wb.create_sheet("Rastreabilidade")
    cabecalho(ws, ["Feature", "PBI", "Casos positivos", "Casos negativos", "Total"], [40, 45, 45, 45, 8])
    for _, feature in features():
        for pbi in feature["pbis"]:
            casos = [c for c in feature["testes"] if c["pbi"] == pbi["id"]]
            pos = [c["id"] for c in casos if c["tipo"] == "Positivo"]
            neg = [c["id"] for c in casos if c["tipo"] == "Negativo"]
            ws.append([feature["titulo"], f"{pbi['id']} - {pbi['titulo']}", ", ".join(pos) or "—", ", ".join(neg) or "—", len(casos)])
            formatar(ws, ws.max_row)

    # --- Execução
    ws = wb.create_sheet("Execução")
    cabecalho(ws, ["ID", "Título", "Tipo", "Resultado", "Executado por", "Data", "Evidência / observação", "Bug vinculado"], [12, 55, 10, 13, 18, 12, 50, 14])
    validacao = DataValidation(type="list", formula1='"Não executado,Passou,Falhou,Bloqueado"', allow_blank=True)
    ws.add_data_validation(validacao)
    for _, feature in features():
        for ct in feature["testes"]:
            ws.append([ct["id"], titulo_ct(ct), ct["tipo"], "Não executado", "", "", "", ""])
            formatar(ws, ws.max_row)
            validacao.add(ws.cell(row=ws.max_row, column=4))
    total = ws.max_row
    ws.append([])
    for rotulo, criterio in [("Passou", "Passou"), ("Falhou", "Falhou"), ("Bloqueado", "Bloqueado"), ("Não executado", "Não executado")]:
        ws.append(["", f"Total {rotulo}", "", f'=COUNTIF(D2:D{total},"{criterio}")'])
    ws.append(["", "% de aprovação", "", f'=IFERROR(COUNTIF(D2:D{total},"Passou")/COUNTA(A2:A{total}),0)'])
    ws.cell(row=ws.max_row, column=4).number_format = "0.0%"

    caminho = os.path.join(RAIZ, "Plano-QA-MoveSync.xlsx")
    wb.save(caminho)
    return caminho


# ---------------------------------------------------------------------------
# 4) Markdown legível
# ---------------------------------------------------------------------------
def gerar_markdown():
    linhas = ["# MoveSync — Plano de Projeto e Plano de Testes", "",
              "> Gerado automaticamente a partir de `qa/fonte/dados_qa.py`. Não edite à mão: altere a fonte e rode o gerador.", ""]
    todos = [c for _, f in features() for c in f["testes"]]
    pos = sum(1 for c in todos if c["tipo"] == "Positivo")
    linhas += [f"**Totais:** {len(EPICOS)} Epics · {sum(len(e['features']) for e in EPICOS)} Features · "
               f"{sum(len(f['pbis']) for _, f in features())} PBIs · {len(todos)} casos de teste ({pos} positivos, {len(todos) - pos} negativos)", ""]
    linhas += ["## Parte 1 — Backlog do produto", ""]
    for epico in EPICOS:
        linhas += [f"### {epico['titulo']}", "", epico["descricao"], ""]
        for feature in epico["features"]:
            linhas += [f"#### {feature['titulo']}", "", f"**Descrição:** {feature['descricao']}", "", "**Critérios de aceite:**", ""]
            linhas += [f"- {c}" for c in feature["criterios"]] + [""]
            for pbi in feature["pbis"]:
                linhas += [f"- **{pbi['id']} — {pbi['titulo']}** (prioridade {pbi['prioridade']}, esforço {pbi['esforco']})",
                           f"  - *{pbi['historia']}*"]
                linhas += [f"  - {c}" for c in pbi["criterios"]]
            linhas += [""]
    linhas += ["## Parte 2 — Casos de teste", ""]
    for _, feature in features():
        linhas += [f"### {feature['titulo']}", ""]
        for ct in feature["testes"]:
            linhas += [f"#### {titulo_ct(ct)}", "",
                       f"- **Objetivo:** {ct['objetivo']}",
                       f"- **Requisito:** {ct['pbi']} · **Prioridade:** {ct['prioridade']}",
                       "- **Preparação:** " + " · ".join(ct["preparacao"]),
                       "- **Inputs:** " + " · ".join(ct["inputs"]),
                       "- **Outputs esperados:** " + " · ".join(ct["outputs"]), "",
                       "| # | Ação do testador | Resposta esperada |", "|---|---|---|"]
            for n, (acao, esperado) in enumerate(ct["passos"], start=1):
                linhas.append(f"| {n} | {acao.replace('|', '/')} | {esperado.replace('|', '/')} |")
            linhas.append("")
    caminho = os.path.join(RAIZ, "PLANO-DE-TESTES.md")
    with open(caminho, "w", encoding="utf-8") as f:
        f.write("\n".join(linhas))
    return caminho


# ---------------------------------------------------------------------------
# 5) Modelo compacto do PDF de entrega (Word) — 2 prints por linha
# ---------------------------------------------------------------------------
def gerar_modelo_pdf(pasta_prints=None, nome_arquivo="Modelo-PDF-Plano-de-Projeto-e-Testes.docx"):
    """Sem pasta_prints: modelo com espaços "[ print ]". Com pasta_prints: encaixa cada print
    (gerado por capturar_prints_azure.py) no seu espaço e salva o documento final."""
    from docx import Document
    from docx.enum.table import WD_ROW_HEIGHT_RULE, WD_TABLE_ALIGNMENT
    from docx.enum.text import WD_ALIGN_PARAGRAPH, WD_BREAK
    from docx.oxml import OxmlElement
    from docx.oxml.ns import qn
    from docx.shared import Cm, Pt, RGBColor

    azul, cinza_texto = RGBColor(0x1F, 0x3A, 0x5F), RGBColor(0x7A, 0x88, 0x99)
    doc = Document()
    for secao in doc.sections:
        secao.page_height, secao.page_width = Cm(29.7), Cm(21.0)
        secao.top_margin = secao.bottom_margin = Cm(1.5)
        secao.left_margin = secao.right_margin = Cm(1.5)
    estilo = doc.styles["Normal"]
    estilo.font.name, estilo.font.size = "Arial", Pt(9)
    estilo.paragraph_format.space_after = Pt(2)

    def texto(t, tamanho=9, negrito=False, cor=None, centro=False, antes=0, depois=2):
        par = doc.add_paragraph()
        par.paragraph_format.space_before, par.paragraph_format.space_after = Pt(antes), Pt(depois)
        if centro:
            par.alignment = WD_ALIGN_PARAGRAPH.CENTER
        run = par.add_run(t)
        run.font.size, run.font.bold = Pt(tamanho), negrito
        if cor:
            run.font.color.rgb = cor
        return par

    def titulo(t):
        texto(t, 13, True, azul, antes=8, depois=4)

    def sombrear(celula):
        tc_pr = celula._tc.get_or_add_tcPr()
        sombra = OxmlElement("w:shd")
        sombra.set(qn("w:val"), "clear")
        sombra.set(qn("w:fill"), "F2F5F9")
        tc_pr.append(sombra)

    def grade(itens, altura_cm=5.6, colunas=2):
        """Tabela de prints: cada célula = legenda + print (ou área cinza "[ print ]" no modelo).
        itens: lista de (legenda, nome_do_arquivo_sem_extensao). Legenda e print ficam na mesma
        célula e a linha não quebra entre páginas."""
        largura_cm = 18.0 / colunas - 0.3
        linhas = (len(itens) + colunas - 1) // colunas
        tabela = doc.add_table(rows=linhas, cols=colunas)
        tabela.style = "Table Grid"
        tabela.alignment = WD_TABLE_ALIGNMENT.CENTER
        for linha in tabela.rows:
            tr_pr = linha._tr.get_or_add_trPr()
            nao_quebrar = OxmlElement("w:cantSplit")
            tr_pr.append(nao_quebrar)
        for i, (rotulo, arquivo) in enumerate(itens):
            linha, coluna = divmod(i, colunas)
            celula = tabela.cell(linha, coluna)
            legenda = celula.paragraphs[0]
            legenda.paragraph_format.keep_with_next = True
            run = legenda.add_run(rotulo)
            run.font.size, run.font.bold = Pt(7.5), True
            par = celula.add_paragraph()
            par.alignment = WD_ALIGN_PARAGRAPH.CENTER
            imagem = os.path.join(pasta_prints, f"{arquivo}.jpg") if pasta_prints and arquivo else None
            if imagem and os.path.exists(imagem):
                par.add_run().add_picture(imagem, width=Cm(largura_cm))
            else:
                sombrear(celula)
                par.paragraph_format.space_before = Cm(altura_cm / 2 - 0.3)
                par.paragraph_format.space_after = Cm(altura_cm / 2 - 0.3)
                marca = par.add_run("[ print ]")
                marca.font.size, marca.font.color.rgb = Pt(8), cinza_texto
        doc.add_paragraph().paragraph_format.space_after = Pt(2)

    def quebra():
        doc.add_paragraph().add_run().add_break(WD_BREAK.PAGE)

    # Capa
    texto("FIAP · GLOBAL SOLUTION 2026/2 · CITIZENTECH", 10, True, azul, centro=True, antes=120)
    texto("MoveSync", 30, True, azul, centro=True, antes=12)
    texto("Plano de Projeto Scrum e Plano de Testes no Azure DevOps", 13, centro=True)
    texto("Compliance, Quality Assurance & Tests · 2TDSPO", 10, cor=cinza_texto, centro=True, depois=24)
    membros = doc.add_table(rows=6, cols=3)
    membros.style = "Table Grid"
    for i, (nome, rm, turma) in enumerate([("Nome completo", "RM", "Turma"),
                                           ("Pedro Pereira Biasolli da Fonseca", "RM562521", "2TDSPO"),
                                           ("Matheus Enrico de Souza", "RM562532", "2TDSPO"),
                                           ("Bruno Andrade Zanateli", "RM563736", "2TDSPO"),
                                           ("Rodrigo Tiezzi", "RM562975", "2TDSPO"),
                                           ("Christian de Sousa Freitas", "RM566098", "2TDSPO")]):
        for j, valor in enumerate((nome, rm, turma)):
            run = membros.cell(i, j).paragraphs[0].add_run(valor)
            run.font.bold = i == 0
    texto("", depois=8)
    texto("Link do projeto no Azure Boards: https://dev.azure.com/movesync-2tdspo/MoveSync/_boards", 9, True)
    texto("Professor convidado como Project Administrator: sim", 9)
    total_ct = sum(len(f["testes"]) for _, f in features())
    total_pbi = sum(len(f["pbis"]) for _, f in features())
    texto(f"Conteúdo: {len(EPICOS)} Epics · {sum(len(e['features']) for e in EPICOS)} Features · {total_pbi} PBIs · "
          f"{total_ct} casos de teste funcionais caixa-preta (positivos e negativos).", 9, cor=cinza_texto, antes=6)
    quebra()

    # 1. Plano de projeto
    titulo("1. Plano de Projeto Scrum")
    texto("Epics → Features → Product Backlog Items. PBIs como histórias de usuário; critérios de aceite em Dado / Quando / Então.")
    grade([("1.1 Boards > Backlogs (nível Epics, hierarquia expandida)", "backlog-1"),
           ("1.1 Backlogs (continuação)", "backlog-2")], 7.5, colunas=1)
    titulo("1.2 Descrição e critérios de aceite — Features e PBIs")
    rotulos = []
    for _, feature in features():
        rotulos.append((f"{feature['titulo']} (Feature)", feature["id"]))
        rotulos += [(f"{pbi['id']} - {pbi['titulo']} (PBI)", pbi["id"]) for pbi in feature["pbis"]]
    grade(rotulos)

    # 2. Plano de testes
    quebra()
    titulo("2. Plano de Testes")
    texto("Casos funcionais de sistema, caixa-preta, positivos e negativos; uma suíte por Feature e vínculo Tested By com a Feature.")
    grade([("2.1 Boards: Test Cases por Feature (consulta Tested By)", "consulta-1"),
           ("2.1 Continuação da consulta", "consulta-2"),
           ("2.2 Test Plans: plano com as suítes F01–F12", "plano-suites"),
           ("2.2 Detalhe de uma suíte (F01)", "suite-F01")], 6.5, colunas=1)
    titulo("2.3 Sumário e Passos de cada caso de teste")
    rotulos = []
    for _, feature in features():
        for ct in feature["testes"]:
            rotulos += [(f"{ct['id']} [{ct['tipo']}] · Sumário", f"{ct['id']}-sumario"),
                        (f"{ct['id']} · Passos", f"{ct['id']}-passos")]
    grade(rotulos, 5.2)

    # 3. Execução (apoio à apresentação)
    titulo("3. Execução e relatórios")
    grade([("Test Plans > Execute (resultados por caso)", "execucao"),
           ("Progress report / gráfico de resultados", "relatorio")], 6.5, colunas=1)

    caminho = os.path.join(RAIZ, nome_arquivo)
    doc.save(caminho)
    return caminho


if __name__ == "__main__":
    url_api = ler_url_api(sys.argv)
    if url_api:
        EPICOS[:] = aplicar_url_api(EPICOS, url_api)
        print("URL da API aplicada aos casos de teste:", url_api.rstrip("/"))
    print("Backlog:", gerar_backlog_csv())
    for c in gerar_csv_casos():
        print("Casos:", c)
    print("Excel:", gerar_excel())
    print("Markdown:", gerar_markdown())
    print("Modelo do PDF:", gerar_modelo_pdf())
