import sys
import subprocess
import importlib
import json
import re
from urllib.parse import quote


def ensure_package(package_name, import_name=None):
    import_name = import_name or package_name
    try:
        importlib.import_module(import_name)
    except ImportError:
        print(f"Instalando {package_name}...")
        subprocess.check_call(
            [sys.executable, "-m", "pip", "install", package_name]
        )


ensure_package("requests")

import requests

BASE_URL = "https://solr.sscdn.co/letras/m1/"


def pesquisar(texto):
    url = (
        f"{BASE_URL}"
        f"?q={quote(texto)}"
        f"&wt=json"
        f"&callback=LetrasSug"
    )

    headers = {
        "User-Agent": "Mozilla/5.0"
    }

    resposta = requests.get(url, headers=headers, timeout=15)
    resposta.raise_for_status()

    conteudo = resposta.text

    # Remove LetrasSug(...)
    match = re.search(r"LetrasSug\((.*)\)\s*$", conteudo, re.S)

    if not match:
        raise Exception("Não foi possível interpretar o retorno.")

    dados = json.loads(match.group(1))

    return dados["response"]["docs"]


def mostrar_resultados(docs):

    musicas = [d for d in docs if d.get("t") == "2"]

    print(f"\nForam encontradas {len(musicas)} músicas.\n")

    for i, m in enumerate(musicas, 1):

        artista = m.get("art", "")
        titulo = m.get("txt", "")
        genero = m.get("g", "")
        hits = m.get("h", 0)
        slug_artista = m.get("dns", "")
        slug_musica = m.get("url", "")
        imagem = m.get("imgm", "")

        link = (
            f"https://www.letras.mus.br/"
            f"{slug_artista}/{slug_musica}/"
        )

        print("=" * 70)
        print(f"Resultado {i}")
        print("=" * 70)
        print("Título      :", titulo)
        print("Artista     :", artista)
        print("Gênero      :", genero)
        print("Popularidade:", hits)
        print("Imagem      :", imagem)
        print("Link        :", link)
        print()


if __name__ == "__main__":

    termo = input("Pesquisar: ").strip()

    try:

        docs = pesquisar(termo)

        mostrar_resultados(docs)

    except Exception as e:
        print("\nErro:", e)