#!/usr/bin/env python3
"""
Simulador de sensores IoT da MoveSync (substitui o ESP32 na demonstração).

O que ele faz, igual a um dispositivo real:
  1. Autentica cada sensor (código + segredo) e recebe um JWT de perfil "Sensor".
  2. A cada ciclo envia uma leitura por sensor: veículos contados e tamanho da fila.
  3. Gera um "leituraId" único por leitura e, às vezes, REENVIA a mesma leitura
     (como um ESP32 que não recebeu o ACK) — a API responde 200 + Idempotency-Replayed.
  4. Com --falha-rede, simula queda de internet: guarda as leituras e envia tudo
     em LOTE quando a "rede volta" (POST /api/telemetria/leituras/lote).

Só usa a biblioteca padrão do Python (nada para instalar).

Exemplos:
  python scripts/simulador_sensores.py --url http://localhost:5080 --segredo sensor-demo-local-2026
  python scripts/simulador_sensores.py --url https://<sua-api>.azurewebsites.net --segredo <segredo> \
      --sensores SPO002-V1,SPO002-V2,SPO002-V3 --perfil pico --intervalo 15 --falha-rede
"""
from __future__ import annotations

import argparse
import json
import random
import time
import urllib.error
import urllib.request
import uuid
from datetime import datetime, timezone

# Veículos por intervalo (média) de cada perfil. A 1ª via recebe a carga "principal".
PERFIS = {
    "pico": {"principal": (35, 12), "secundaria": (6, 2)},       # (veículos, fila)
    "equilibrado": {"principal": (15, 4), "secundaria": (14, 4)},
    "madrugada": {"principal": (2, 0), "secundaria": (1, 0)},
}


def requisitar(metodo: str, url: str, corpo: dict | None = None, token: str | None = None):
    dados = json.dumps(corpo).encode() if corpo is not None else None
    req = urllib.request.Request(url, data=dados, method=metodo)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", f"Bearer {token}")
    try:
        with urllib.request.urlopen(req, timeout=10) as resp:
            return resp.status, dict(resp.headers), json.loads(resp.read() or b"null")
    except urllib.error.HTTPError as erro:
        corpo_erro = erro.read()
        try:
            return erro.code, dict(erro.headers), json.loads(corpo_erro)
        except json.JSONDecodeError:
            return erro.code, dict(erro.headers), corpo_erro.decode(errors="ignore")


def autenticar(base: str, codigo: str, segredo: str) -> str:
    status, _, corpo = requisitar("POST", f"{base}/api/auth/sensores/token", {"codigo": codigo, "segredo": segredo})
    if status != 200:
        raise SystemExit(f"[{codigo}] falha na autenticação ({status}): {corpo}")
    print(f"[{codigo}] autenticado — token válido até {corpo['expiraEm']}")
    return corpo["accessToken"]


def gerar_leitura(perfil: str, principal: bool) -> dict:
    media_veiculos, media_fila = PERFIS[perfil]["principal" if principal else "secundaria"]
    return {
        "leituraId": str(uuid.uuid4()),
        "coletadaEm": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "veiculosContados": max(0, int(random.gauss(media_veiculos, media_veiculos * 0.25 + 1))),
        "tamanhoFila": max(0, int(random.gauss(media_fila, media_fila * 0.3 + 0.5))),
    }


def main() -> None:
    parser = argparse.ArgumentParser(description="Simulador de sensores IoT da MoveSync")
    parser.add_argument("--url", default="http://localhost:5080", help="URL base da API")
    parser.add_argument("--segredo", required=True, help="Segredo dos sensores (Seed:SegredoSensores)")
    parser.add_argument("--sensores", default="SPO002-V1,SPO002-V2,SPO002-V3", help="Códigos separados por vírgula")
    parser.add_argument("--perfil", choices=PERFIS.keys(), default="pico")
    parser.add_argument("--intervalo", type=int, default=15, help="Segundos entre leituras")
    parser.add_argument("--ciclos", type=int, default=0, help="0 = infinito")
    parser.add_argument("--chance-reenvio", type=float, default=0.15, help="Probabilidade de reenviar a mesma leitura")
    parser.add_argument("--falha-rede", action="store_true", help="Simula queda de rede e envio em lote")
    args = parser.parse_args()

    base = args.url.rstrip("/")
    codigos = [c.strip() for c in args.sensores.split(",") if c.strip()]
    tokens = {codigo: autenticar(base, codigo, args.segredo) for codigo in codigos}
    buffer_offline: dict[str, list[dict]] = {codigo: [] for codigo in codigos}

    ciclo = 0
    while args.ciclos == 0 or ciclo < args.ciclos:
        ciclo += 1
        sem_rede = args.falha_rede and ciclo % 6 in (3, 4)  # 2 ciclos sem rede a cada 6
        print(f"\n--- ciclo {ciclo} ({args.perfil}){' — SEM REDE, guardando leituras' if sem_rede else ''} ---")

        for indice, codigo in enumerate(codigos):
            leitura = gerar_leitura(args.perfil, principal=(indice == 0))

            if sem_rede:
                buffer_offline[codigo].append(leitura)
                print(f"[{codigo}] offline: {len(buffer_offline[codigo])} leitura(s) no buffer")
                continue

            if buffer_offline[codigo]:
                lote = buffer_offline[codigo] + [leitura]
                status, _, corpo = requisitar("POST", f"{base}/api/telemetria/leituras/lote", {"leituras": lote}, tokens[codigo])
                print(f"[{codigo}] rede voltou → lote {status}: {corpo}")
                buffer_offline[codigo].clear()
                continue

            status, cabecalhos, _ = requisitar("POST", f"{base}/api/telemetria/leituras", leitura, tokens[codigo])
            print(f"[{codigo}] {leitura['veiculosContados']:>3} veíc. / fila {leitura['tamanhoFila']:>2} → HTTP {status}")

            if status == 429:
                print(f"[{codigo}] limite de requisições atingido; aguardando {cabecalhos.get('Retry-After', '?')}s")
            elif random.random() < args.chance_reenvio:
                status2, cab2, _ = requisitar("POST", f"{base}/api/telemetria/leituras", leitura, tokens[codigo])
                print(f"[{codigo}] reenvio da MESMA leitura → HTTP {status2} (Idempotency-Replayed: {cab2.get('Idempotency-Replayed')})")

        time.sleep(args.intervalo)


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\nSimulação encerrada.")
