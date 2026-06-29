import sys
import subprocess
import importlib
import json

import re
import unicodedata


FINALIDADE = "louvor"


def slug(texto):
    texto = unicodedata.normalize("NFKD", texto)
    texto = texto.encode("ascii", "ignore").decode("ascii")
    texto = texto.lower()
    texto = re.sub(r"[^a-z0-9]+", "-", texto)
    texto = re.sub(r"-+", "-", texto)
    return texto.strip("-")

def ensure(pkg, module=None):
    module = module or pkg
    try:
        importlib.import_module(module)
    except ImportError:
        subprocess.check_call([sys.executable, "-m", "pip", "install", pkg])


ensure("requests")
ensure("beautifulsoup4", "bs4")

import requests
from bs4 import BeautifulSoup

URL = "https://www.letras.mus.br/get-worship/um-novo-dia/"


def meta(soup, prop=None, name=None):
    if prop:
        tag = soup.find("meta", property=prop)
    else:
        tag = soup.find("meta", attrs={"name": name})

    return tag.get("content", "").strip() if tag else ""


headers = {
    "User-Agent": "Mozilla/5.0"
}

html = requests.get(URL, headers=headers).text

soup = BeautifulSoup(html, "html.parser")

titulo = meta(soup, "og:title")
descricao = meta(soup, name="description")
imagem = meta(soup, "og:image")
url = meta(soup, "og:url")

# artista
artista = ""

for a in soup.find_all("a"):
    href = a.get("href", "")
    if href.startswith("/") and len(a.text.strip()) > 0:
        if artista == "":
            artista = a.text.strip()

# letra
letra = ""

for div in soup.find_all("div"):
    classes = " ".join(div.get("class", []))

    if "lyric" in classes.lower():
        letra = div.get_text("\n", strip=True)
        break

resultado = {
    "title": titulo,
    "artist": artista,
    "url": url,
    "description": descricao,
    "image": imagem,
    "lyrics": letra
}

print(json.dumps(resultado, indent=4, ensure_ascii=False))

nome_arquivo = f"{FINALIDADE}-{slug(titulo)}.json"

with open(nome_arquivo, "w", encoding="utf8") as f:
    json.dump(resultado, f, indent=4, ensure_ascii=False)

print(f"\nArquivo criado: {nome_arquivo}")
