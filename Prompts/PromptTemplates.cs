namespace WorshipSearchWeb.Prompts;

public static class PromptTemplates
{
    public const string ParagraphEnrichmentTemplate = @"Você é um indexador de músicas cristã/evangélicas de louvor e adoração em português (pt-BR).

Música: ""{title}"" — {artist}

Tags já identificadas (NÃO repita nenhuma):
- temas: {existing_themes}
- emoções: {existing_moods}
- contextos: {existing_contexts}
- palavras-chave: {existing_keywords}
- tópicos bíblicos: {existing_biblical_topics}
- referências bíblicas: {existing_biblical_references}
- livros bíblicos: {existing_biblical_books}
- personagens bíblicos: {existing_biblical_characters}
- sinônimos: {existing_synonyms}
- estilo: ""{existing_worship_style}""
- energia: ""{existing_energy_level}""
- ocasião: ""{existing_occasion}""
- momentos do culto: {existing_worship_moments}

Analise esta estrofe e retorne APENAS tags NOVAS não listadas acima:
---
{paragraph}
---

REGRAS:
- Responda SOMENTE em português (pt-BR)
- Tags devem ser curtas (1-3 palavras), pesquisáveis e variadas
- Sempre dentro do contexto cristão/evangélico
- Sem textos longos, apenas termos de busca objetivos
- Não repita tags já listadas
- worship_moments: use apenas valores desta lista exata: Adoração, Santa Ceia, Apelo, Intercessão, Ofertório, Abertura, Encerramento
nota: para definiçãodo worship_moments, considere o momento do culto em que a estrofe se encaixda ao sacrifício de cristo, ressureição de cristo, partir do pão e beber do calice, um contexto do culto onde iremos celebrar a ceia.

Retorne um objeto JSON com estes campos exatos. Use [] ou """" para campos sem novidades:
themes, moods, contexts, keywords, biblical_topics, biblical_references, biblical_books, biblical_characters, synonyms, worship_style, energy_level, occasion, worship_moments

Retorne APENAS o JSON, sem texto extra.";

    public const string QueryExpansionTemplate = @"Você é um assistente de busca de músicas cristã/evangélicas em português (pt-BR).

O usuário quer encontrar músicas relacionadas a:
{criteria_list}

Para CADA critério acima, gere de 4 a 6 termos alternativos (sinônimos, conceitos bíblicos, emoções ou contextos relacionados) que possam localizar músicas cristãs sobre esse assunto.

REGRAS:
- Apenas português (pt-BR)
- Contexto cristão/evangélico
- Termos curtos e pesquisáveis (1-3 palavras)
- Não repita o critério original

Retorne APENAS um JSON com este formato exato, sem texto extra:
{{
  ""description"": ""frase curta em português descrevendo os critérios buscados (ex: 'libertação divina e Salmos 21')"",
  ""criteria"": [
    {{ ""original"": ""critério original"", ""terms"": [""termo1"", ""termo2"", ""termo3""] }},
    ...
  ]
}}";

    public const string CriterionClassificationTemplate = @"Você é um classificador de critérios de busca de músicas cristãs em português (pt-BR).

Critério: ""{input}""

Classifique este critério em um dos tipos abaixo e gere tags nos campos corretos para enriquecer a busca:

Tipos possíveis:
- biblical_reference: referência bíblica específica (ex: ""Romanos 8"", ""Salmo 91"", ""João 3:16"")
- worship_moment: momento ou ocasião do culto (ex: ""Santa Ceia"", ""abertura"", ""ofertório"", ""encerramento"")
- mood: emoção ou sentimento espiritual (ex: ""intimidade"", ""reverência"", ""lamento"", ""alegria"")
- context: contexto litúrgico específico (ex: ""jovens"", ""Natal"", ""Páscoa"", ""batismo"")
- theme: tema espiritual ou teológico (ex: ""graça"", ""esperança"", ""cura"", ""libertação"")
- keyword: termo de busca geral

REGRAS:
- Apenas português (pt-BR)
- Contexto cristão/evangélico
- 4 a 8 tags por campo relevante, 1-3 palavras cada
- Preencha apenas os campos que fazem sentido para o tipo identificado

Retorne APENAS este JSON, sem texto extra:
{{
  ""type"": ""..."",
  ""tags"": {{
    ""themes"": [],
    ""contexts"": [],
    ""keywords"": [],
    ""biblical_topics"": [],
    ""biblical_references"": [],
    ""biblical_books"": [],
    ""biblical_characters"": [],
    ""moods"": [],
    ""synonyms"": [],
    ""worship_moments"": [],
    ""worship_style"": """",
    ""energy_level"": """",
    ""occasion"": """"
  }}
}}";

    public const string SummaryTemplate = @"Você é especialista em música cristã e teologia evangélica. Analise a música ""{title}"" de {artist}.

Tags identificadas:
{tags_json}

Letra completa:
{lyrics}

Escreva em português (pt-BR), de forma concisa e pesquisável:
- summary: resumo espiritual da música (máximo 2 frases curtas)
- explanation: explicação teológica cristã (máximo 3 frases)
- practical_application: como usar no culto (máximo 2 frases)
- worship_style: estilo de adoração (ex: ""contemplativo"", ""celebração"", ""intercessão"") — 1-3 palavras
- energy_level: nível de energia (ex: ""suave"", ""moderado"", ""vibrante"") — 1-2 palavras
- occasion: ocasião ideal (ex: ""abertura do culto"", ""Santa Ceia"", ""encerramento"") — 1-4 palavras
- worship_moments: lista dos momentos do culto em que esta música se encaixa. Use APENAS valores desta lista: Celebração, Adoração, Santa Ceia, Apelo, Intercessão, Ofertório, Abertura, Encerramento

Contexto sempre cristão/evangélico. Textos curtos e diretos.

Retorne APENAS um JSON com exatamente estes sete campos: summary, explanation, practical_application, worship_style, energy_level, occasion, worship_moments
Sem texto extra.";
}
