# -*- coding: utf-8 -*-
"""
Cria no Azure DevOps (processo Scrum), via API REST, TODO o conteúdo de QA da MoveSync:
  1. Epics > Features > Product Backlog Items (com Descrição, Critérios de aceite, Prioridade e Esforço)
  2. Test Plan "Plano de Testes - MoveSync" com uma suíte estática por Feature
  3. Test Cases com Sumário (Objetivo, Preparação, Inputs, Outputs) e Passos (ação → resultado esperado)
  4. Vínculo "Tested By" entre cada Feature e seus Test Cases (aparece na aba Links da Feature no Boards)

Pré-requisitos:
  • Projeto criado no Azure DevOps com o processo SCRUM.
  • Personal Access Token (PAT) com escopos: Work Items (Read & Write) e Test Management (Read & Write),
    OU um token do Microsoft Entra (sem criar PAT), por exemplo no Azure Cloud Shell:
      export AZURE_DEVOPS_PAT=$(az account get-access-token --resource 499b84ac-1321-427f-aa17-267ca6975798 --query accessToken -o tsv)
  • Licença "Basic + Test Plans" (ou o teste gratuito de 30 dias) para quem roda o script: criar Test Plan exige.
  • pip install requests

Uso:
  python qa/fonte/azure_devops_importar.py --org https://dev.azure.com/SUA-ORG --projeto MoveSync --pat SEU_PAT --simular
  python qa/fonte/azure_devops_importar.py --org https://dev.azure.com/SUA-ORG --projeto MoveSync --pat SEU_PAT \
      --url-api https://minha-api.azurewebsites.net

  --simular  mostra o que seria criado, sem chamar a API.
  --url-api  troca o marcador [URL da API] dos passos pela URL real da API publicada.
  Dica: rode primeiro em um projeto de teste. O script não apaga nada; rodar duas vezes duplica os itens.
"""
import argparse
import base64
import html
import os
import sys
import time
from xml.sax.saxutils import escape as escape_xml

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dados_qa import EPICOS  # noqa: E402
from gerar_artefatos_qa import aplicar_url_api, criterios_html, descricao_ct_html, titulo_ct  # noqa: E402

API = "7.1"


class AzureDevOps:
    def __init__(self, org, projeto, pat, simular):
        self.org = org.rstrip("/")
        self.projeto = projeto
        self.simular = simular
        self._proximo_id_simulado = 1000
        if not simular:
            import requests  # importado aqui para --simular funcionar sem a biblioteca
            self.http = requests.Session()
            if pat.startswith("eyJ"):  # token do Microsoft Entra (az account get-access-token): dispensa PAT
                self.http.headers["Authorization"] = f"Bearer {pat}"
            else:
                token = base64.b64encode(f":{pat}".encode()).decode()
                self.http.headers["Authorization"] = f"Basic {token}"

    def _url(self, caminho):
        return f"{self.org}/{self.projeto}/_apis/{caminho}"

    def _enviar(self, metodo, url, **kwargs):
        for tentativa in range(4):
            resposta = self.http.request(metodo, url, timeout=60, **kwargs)
            if resposta.status_code in (429, 503):  # limite do Azure DevOps: espera e tenta de novo
                time.sleep(int(resposta.headers.get("Retry-After", 5)) + tentativa)
                continue
            if resposta.status_code >= 400:
                raise SystemExit(f"Erro {resposta.status_code} em {metodo} {url}\n{resposta.text[:1500]}")
            return resposta.json()
        raise SystemExit(f"Azure DevOps continuou limitando as requisições em {url}")

    def criar_work_item(self, tipo, campos, pai_url=None, relacoes=None):
        if self.simular:
            self._proximo_id_simulado += 1
            print(f"  [simulação] {tipo}: {campos['System.Title']}")
            return {"id": self._proximo_id_simulado, "url": f"simulado/{self._proximo_id_simulado}"}
        operacoes = [{"op": "add", "path": f"/fields/{k}", "value": v} for k, v in campos.items() if v not in (None, "")]
        if pai_url:
            operacoes.append({"op": "add", "path": "/relations/-",
                              "value": {"rel": "System.LinkTypes.Hierarchy-Reverse", "url": pai_url}})
        for rel, url in relacoes or []:
            operacoes.append({"op": "add", "path": "/relations/-", "value": {"rel": rel, "url": url}})
        return self._enviar("POST", self._url(f"wit/workitems/${tipo}?api-version={API}"),
                            json=operacoes, headers={"Content-Type": "application/json-patch+json"})

    def criar_plano(self, nome):
        if self.simular:
            print(f"  [simulação] Test Plan: {nome}")
            return {"id": 1, "rootSuite": {"id": 2}}
        return self._enviar("POST", self._url(f"testplan/plans?api-version={API}"),
                            json={"name": nome, "areaPath": self.projeto, "iteration": self.projeto})

    def criar_suite(self, plano_id, raiz_id, nome):
        if self.simular:
            self._proximo_id_simulado += 1
            print(f"  [simulação] Suíte: {nome}")
            return {"id": self._proximo_id_simulado}
        return self._enviar("POST", self._url(f"testplan/Plans/{plano_id}/suites?api-version={API}"),
                            json={"suiteType": "staticTestSuite", "name": nome, "parentSuite": {"id": raiz_id}})

    def adicionar_casos_na_suite(self, plano_id, suite_id, ids):
        if self.simular or not ids:
            return
        self._enviar("POST", self._url(f"testplan/Plans/{plano_id}/Suites/{suite_id}/TestCase?api-version={API}"),
                     json=[{"workItem": {"id": i}} for i in ids])


def passos_xml(passos):
    """Formato interno do campo Microsoft.VSTS.TCM.Steps (o mesmo gravado pela tela do Azure)."""
    partes = []
    for indice, (acao, esperado) in enumerate(passos, start=2):
        acao_html = escape_xml(f"<P>{html.escape(acao, quote=False)}</P>")
        esperado_html = escape_xml(f"<P>{html.escape(esperado, quote=False)}</P>")
        partes.append(f'<step id="{indice}" type="ValidateStep">'
                      f'<parameterizedString isformatted="true">{acao_html}</parameterizedString>'
                      f'<parameterizedString isformatted="true">{esperado_html}</parameterizedString>'
                      f"<description/></step>")
    return f'<steps id="0" last="{len(passos) + 1}">{"".join(partes)}</steps>'


def main():
    parser = argparse.ArgumentParser(description="Importa backlog e plano de testes da MoveSync no Azure DevOps")
    parser.add_argument("--org", required=True, help="Ex.: https://dev.azure.com/minha-org")
    parser.add_argument("--projeto", required=True)
    parser.add_argument("--pat", default=os.environ.get("AZURE_DEVOPS_PAT", ""), help="PAT ou token do Entra (ou variável AZURE_DEVOPS_PAT)")
    parser.add_argument("--simular", action="store_true")
    parser.add_argument("--url-api", default=os.environ.get("MOVESYNC_URL_API", ""),
                        help="URL da API publicada; substitui [URL da API] nos passos dos casos de teste")
    args = parser.parse_args()
    if args.url_api:
        EPICOS[:] = aplicar_url_api(EPICOS, args.url_api)
        print(f"URL da API aplicada aos casos de teste: {args.url_api.rstrip('/')}")
    if not args.simular and not args.pat:
        raise SystemExit("Informe o PAT com --pat ou na variável de ambiente AZURE_DEVOPS_PAT.")

    az = AzureDevOps(args.org, args.projeto, args.pat, args.simular)
    plano = az.criar_plano("Plano de Testes - MoveSync")
    total_ct = 0

    for epico in EPICOS:
        print(f"\n{epico['titulo']}")
        epico_wi = az.criar_work_item("Epic", {
            "System.Title": epico["titulo"],
            "System.Description": f"<p>{html.escape(epico['descricao'])}</p>",
        })
        for feature in epico["features"]:
            feature_wi = az.criar_work_item("Feature", {
                "System.Title": feature["titulo"],
                "System.Description": f"<p>{html.escape(feature['descricao'])}</p>",
                "Microsoft.VSTS.Common.AcceptanceCriteria": criterios_html(feature["criterios"]),
            }, pai_url=epico_wi["url"])
            for pbi in feature["pbis"]:
                az.criar_work_item("Product Backlog Item", {
                    "System.Title": f"{pbi['id']} - {pbi['titulo']}",
                    "System.Description": f"<p><b>História de usuário:</b> {html.escape(pbi['historia'])}</p>",
                    "Microsoft.VSTS.Common.AcceptanceCriteria": criterios_html(pbi["criterios"]),
                    "Microsoft.VSTS.Common.Priority": pbi["prioridade"],
                    "Microsoft.VSTS.Scheduling.Effort": pbi["esforco"],
                }, pai_url=feature_wi["url"])

            suite = az.criar_suite(plano["id"], plano["rootSuite"]["id"], feature["titulo"])
            ids_casos = []
            for ct in feature["testes"]:
                caso = az.criar_work_item("Test Case", {
                    "System.Title": titulo_ct(ct),
                    "System.Description": descricao_ct_html(ct, feature),
                    "Microsoft.VSTS.TCM.Steps": passos_xml(ct["passos"]),
                    "Microsoft.VSTS.Common.Priority": ct["prioridade"],
                    "System.Tags": f"{ct['tipo']}; {feature['id']}; {ct['pbi']}; Caixa-preta",
                }, relacoes=[("Microsoft.VSTS.Common.TestedBy-Reverse", feature_wi["url"])])
                ids_casos.append(caso["id"])
                total_ct += 1
            az.adicionar_casos_na_suite(plano["id"], suite["id"], ids_casos)

    print(f"\nConcluído: {total_ct} casos de teste criados e vinculados às Features.")


if __name__ == "__main__":
    main()
