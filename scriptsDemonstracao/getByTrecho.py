import sys
import subprocess
import importlib


def ensure_package(package_name, import_name=None):
    """
    Verifica se um pacote está instalado.
    Caso não esteja, instala automaticamente via pip.
    """
    import_name = import_name or package_name

    try:
        importlib.import_module(import_name)
    except ImportError:
        print(f"[INFO] Instalando {package_name}...")
        subprocess.check_call(
            [sys.executable, "-m", "pip", "install", package_name]
        )


# Dependências
ensure_package("requests")
ensure_package("beautifulsoup4", "bs4")

import requests
from bs4 import BeautifulSoup
from urllib.parse import quote

BASE_URL = "https://www.letras.mus.br/"


def search(query: str):
    url = f"{BASE_URL}?q={quote(query)}"

    headers = {
        "User-Agent": (
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
            "AppleWebKit/537.36 Chrome/137.0 Safari/537.36"
        )
    }

    print(f"\nPesquisando: {query}")

    response = requests.get(url, headers=headers, timeout=15)
    response.raise_for_status()

    soup = BeautifulSoup(response.text, "html.parser")

    results = []

    for a in soup.find_all("a", href=True):
        href = a["href"]

        if href.startswith("/") and href.count("/") >= 2:
            title = a.get_text(" ", strip=True)

            if not title:
                continue

            results.append({
                "title": title,
                "artist": "",
                "url": BASE_URL.rstrip("/") + href,
                "snippet": ""
            })

    # Remove duplicados
    unique = []
    seen = set()

    for item in results:
        if item["url"] not in seen:
            seen.add(item["url"])
            unique.append(item)

    return unique


if __name__ == "__main__":
    termo = input("Trecho da música: ")

    try:
        musicas = search(termo)

        if not musicas:
            print("\nNenhum resultado encontrado.")
        else:
            print(f"\n{len(musicas)} resultado(s):")

            for i, musica in enumerate(musicas, 1):
                print("-" * 60)
                print(f"Resultado {i}")
                print(f"Título : {musica['title']}")
                print(f"Artista: {musica['artist']}")
                print(f"Link   : {musica['url']}")
                print(f"Trecho : {musica['snippet']}")

    except Exception as ex:
        print("\nErro:", ex)