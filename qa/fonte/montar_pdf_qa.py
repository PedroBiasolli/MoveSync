# -*- coding: utf-8 -*-
"""
Monta o PDF de entrega de QA (Plano de Projeto Scrum + Plano de Testes) com os prints reais
do Azure DevOps, capturados pela ferramenta CAPTURAR-PRINTS (pasta movesync-prints).

Uso:
  python qa/fonte/montar_pdf_qa.py --prints C:/Users/voce/source/movesync-prints/prints
Gera em qa/: Plano-de-Projeto-e-Testes-MoveSync.docx e .pdf (o PDF exige LibreOffice no PATH).

O que ele faz:
  1. Corta o espaço vazio no fim das telas altas (backlog e consulta) e divide cada uma em 2 partes,
     para caberem legíveis na largura da página.
  2. Encaixa cada print na célula certa do modelo (gerar_artefatos_qa.gerar_modelo_pdf).
  3. Converte para PDF.
"""
import argparse
import os
import shutil
import subprocess
import sys
import tempfile

from PIL import Image, ImageChops

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gerar_artefatos_qa as g  # noqa: E402


def aparar_fundo(imagem):
    """Remove a faixa branca/vazia no fim da tela (grades altas do Azure DevOps)."""
    fundo = Image.new(imagem.mode, imagem.size, imagem.getpixel((imagem.width - 2, imagem.height - 2)))
    caixa = ImageChops.difference(imagem, fundo).getbbox()
    if not caixa:
        return imagem
    return imagem.crop((0, 0, imagem.width, min(imagem.height, caixa[3] + 24)))


def dividir(origem, destino_base, partes=2):
    imagem = aparar_fundo(Image.open(origem).convert("RGB"))
    altura = imagem.height // partes
    for i in range(partes):
        fim = imagem.height if i == partes - 1 else (i + 1) * altura
        imagem.crop((0, i * altura, imagem.width, fim)).save(f"{destino_base}-{i + 1}.jpg", quality=90)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--prints", required=True, help="pasta com os .jpg gerados pela captura")
    ap.add_argument("--sem-pdf", action="store_true")
    ap.add_argument("--sem-execucao", action="store_true",
                    help="omite a seção 3 (use enquanto nenhum teste foi executado no Test Plans)")
    args = ap.parse_args()

    pasta = tempfile.mkdtemp(prefix="prints-")
    for nome in os.listdir(args.prints):
        if args.sem_execucao and nome in ("execucao.jpg", "relatorio.jpg"):
            continue
        if nome.lower().endswith(".jpg"):
            shutil.copy(os.path.join(args.prints, nome), pasta)
    for alto, base in (("backlog.jpg", "backlog"), ("consulta-tested-by.jpg", "consulta")):
        caminho = os.path.join(pasta, alto)
        if os.path.exists(caminho):
            dividir(caminho, os.path.join(pasta, base))

    docx = g.gerar_modelo_pdf(pasta, "Plano-de-Projeto-e-Testes-MoveSync.docx")
    print("Word:", docx)
    if not args.sem_pdf:
        soffice = shutil.which("soffice") or shutil.which("libreoffice")
        if not soffice:
            raise SystemExit("LibreOffice não encontrado: abra o .docx no Word e use Salvar como > PDF.")
        subprocess.run([soffice, "--headless", "--convert-to", "pdf", "--outdir", os.path.dirname(docx), docx],
                       check=True, stdout=subprocess.DEVNULL)
        print("PDF:", docx[:-5] + ".pdf")


if __name__ == "__main__":
    main()
