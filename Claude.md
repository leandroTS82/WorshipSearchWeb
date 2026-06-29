# CLAUDE.md

# Worship Search

## Visão Geral

Worship Search é uma aplicação web desenvolvida em ASP.NET Core 8 destinada a auxiliar grupos de louvor na escolha rápida de músicas de acordo com:

* tema da pregação;
* textos bíblicos;
* contexto do culto;
* palavras-chave;
* emoções;
* assuntos bíblicos;
* significado da letra;
* repertório previamente aprovado pelo ministério.

O objetivo principal é construir um catálogo inteligente de louvores utilizando arquivos JSON enriquecidos por Inteligência Artificial.

A pesquisa deverá ser rápida, simples e eficiente.

---

# Objetivos

O sistema deverá permitir:

* pesquisar músicas por nome;
* pesquisar músicas por trecho da letra;
* consultar serviços públicos para localizar músicas;
* obter o HTML público da música;
* extrair automaticamente as informações relevantes;
* gerar um JSON estruturado;
* enriquecer esse JSON utilizando LLM;
* editar manualmente qualquer informação;
* salvar definitivamente o JSON;
* reutilizar essas informações em pesquisas futuras.

---

# Tecnologias

## Backend

ASP.NET Core 8

Linguagem:

C#

Todo o projeto deverá ser desenvolvido exclusivamente em C#.

Não utilizar Python.

Não criar dependências externas desnecessárias.

---

## Frontend

* HTML
* Bootstrap
* jQuery
* JavaScript

Objetivo:

Interface simples, rápida e de fácil manutenção.

---

# Estrutura sugerida

```
WorshipSearch.sln

/src

    WorshipSearch.Web

    WorshipSearch.Core

    WorshipSearch.Infrastructure

    WorshipSearch.Search

    WorshipSearch.AI

    WorshipSearch.Domain

    WorshipSearch.Shared

/json

    Todos os documentos indexados

/logs

/cache
```

---

# Arquitetura

A solução será baseada em serviços especializados.

Exemplo:

```
MusicSearchService

MusicDownloadService

MusicHtmlParser

MusicJsonGenerator

MusicRepository

JsonSearchEngine

GroqService

PromptBuilder

SearchRankingService

MetadataEditorService
```

Cada classe deverá possuir responsabilidade única.

---

# Persistência

O projeto utilizará exclusivamente arquivos JSON.

Não utilizar banco de dados enquanto os JSON atenderem às necessidades do projeto.

Cada música será representada por um único arquivo.

Nome:

```
louvor-{slug-title}.json
```

Exemplo

```
louvor-um-novo-dia.json
```

---

# Fluxo de Indexação

Pesquisar

↓

Selecionar música

↓

Download HTML

↓

Extrair metadados

↓

Gerar JSON Base

↓

Enriquecimento usando LLM

↓

Revisão manual

↓

Salvar JSON definitivo

---

# Fluxo de Pesquisa

Pesquisar

↓

Consultar JSONs existentes

↓

Calcular relevância

↓

Exibir ranking

↓

Opcionalmente utilizar LLM caso agregue valor à pesquisa.

---

# Uso do LLM

O projeto utilizará Groq.

O sistema deverá considerar que as chaves pertencem a um plano Tier.

Portanto:

* minimizar chamadas desnecessárias;
* reutilizar informações sempre que possível;
* evitar reprocessamentos já realizados;
* utilizar cache sempre que apropriado.

O LLM poderá ser utilizado em qualquer funcionalidade do sistema quando agregar valor.

Entretanto, sempre deverá existir uma alternativa sem LLM quando possível.

---

# Obtenção das chaves

Nunca armazenar chaves no projeto.

Nunca utilizar chave fixa.

Toda chamada ao LLM deverá obter uma nova chave através da API.

GET

```
http://llmkeys.twobrothermotors.com.br/api/keys/groq/next
```

Authorization

```
Bearer llmkey-super-secret-bearer-2025
```

Uma nova chave deverá ser solicitada para cada requisição ao LLM.

Objetivos:

* distribuir consumo entre chaves;
* reduzir Rate Limit;
* simplificar gerenciamento.

As chaves nunca deverão ser persistidas em:

* arquivos;
* banco;
* cache.

A chave deverá existir apenas durante a execução da requisição.

---

# Estratégia de Prompts

Prompts deverão ser centralizados.

Criar classes específicas para:

```
PromptBuilder

PromptTemplates

PromptContext

PromptResultParser
```

Prompts grandes deverão ser divididos quando necessário.

Evitar enviar contexto irrelevante.

---

# Inteligência

O sistema deverá produzir informações semânticas como:

* temas;
* contextos;
* assuntos bíblicos;
* referências bíblicas;
* livros bíblicos;
* personagens;
* palavras-chave;
* sinônimos;
* emoções;
* ocasião do culto;
* estilo do louvor;
* nível de energia;
* resumo;
* explicação da letra;
* aplicação prática.

O usuário poderá editar qualquer informação posteriormente.

---

# JSON

Estrutura mínima

```json
{
  "id": "get-worship_um-novo-dia",
  "title": "Um Novo Dia",
  "artist": "Get Worship",
  "album": "Um Novo Dia",
  "language": "pt-BR",
  "genre": "Gospel/Religioso",

  "approved": false,

  "search": {
    "themes": [],
    "contexts": [],
    "keywords": [],
    "biblical_topics": [],
    "biblical_references": [],
    "biblical_books": [],
    "biblical_characters": [],
    "moods": [],
    "synonyms": []
  },

  "summary": "",

  "lyrics": "",

  "metadata": {
    "source": "",
    "canonical_url": "",
    "image": "",
    "indexed_at": ""
  }
}
```

O JSON poderá evoluir ao longo do projeto.

Novos campos poderão ser adicionados mantendo compatibilidade com versões anteriores.

---

# Administração

O sistema deverá possuir telas para:

* pesquisar músicas;
* visualizar JSON;
* editar JSON;
* adicionar temas;
* adicionar palavras-chave;
* adicionar contextos;
* adicionar referências bíblicas;
* adicionar sinônimos;
* aprovar ou reprovar músicas;
* reprocessar utilizando novos prompts;
* visualizar histórico.

---

# Pesquisa Inteligente

A pesquisa deverá combinar múltiplos critérios.

Exemplos:

```
esperança

↓

graça

↓

Romanos 8

↓

cura

↓

adoração

↓

abertura

↓

Santa Ceia

↓

Salmo 91
```

A ordenação deverá considerar relevância.

Quando fizer sentido, o LLM poderá ser utilizado para melhorar a classificação ou interpretação da intenção do usuário, respeitando sempre o consumo do plano Tier.

---

# Boas Práticas

* Priorizar simplicidade.
* Código limpo.
* Responsabilidade única.
* Baixo acoplamento.
* Reutilização.
* Serviços pequenos.
* JSON versionável.
* Prompts reutilizáveis.
* Cache quando aplicável.
* Evitar chamadas repetidas ao LLM.
* Toda funcionalidade deve funcionar sem depender de banco de dados.

---

# Objetivos futuros

* Busca vetorial.
* Embeddings.
* Similaridade semântica.
* Sugestão automática de repertório.
* Planejamento completo de cultos.
* Recomendação baseada na pregação.
* Integração com outros provedores de LLM.
* Suporte a múltiplos idiomas.
