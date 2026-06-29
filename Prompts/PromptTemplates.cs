namespace WorshipSearchWeb.Prompts;

public static class PromptTemplates
{
    public const string ParagraphEnrichmentTemplate = @"Você é um indexador de músicas cristãs de louvor e adoração em português (pt-BR).

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

Retorne um objeto JSON com estes campos exatos. Use [] ou """" para campos sem novidades:
themes, moods, contexts, keywords, biblical_topics, biblical_references, biblical_books, biblical_characters, synonyms, worship_style, energy_level, occasion

Retorne APENAS o JSON, sem texto extra.";

    public const string SummaryTemplate = @"Você é especialista em música cristã e teologia evangélica. Analise a música ""{title}"" de {artist}.

Tags identificadas:
{tags_json}

Letra completa:
{lyrics}

Escreva em português (pt-BR), de forma concisa e pesquisável:
- summary: resumo espiritual da música (máximo 2 frases curtas)
- explanation: explicação teológica cristã (máximo 3 frases)
- practical_application: como usar no culto (máximo 2 frases)

Contexto sempre cristão/evangélico. Textos curtos e diretos.

Retorne APENAS um JSON com exatamente estes três campos: summary, explanation, practical_application
Sem texto extra.";
}
