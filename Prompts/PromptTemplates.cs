namespace WorshipSearchWeb.Prompts;

public static class PromptTemplates
{
    public const string ParagraphEnrichmentTemplate = @"You are building a worship music search index stanza by stanza.

Song: ""{title}"" by {artist}

Tags already identified from previous stanzas (DO NOT repeat any of these):
- themes: {existing_themes}
- moods: {existing_moods}
- contexts: {existing_contexts}
- keywords: {existing_keywords}
- biblical_topics: {existing_biblical_topics}
- biblical_references: {existing_biblical_references}
- biblical_books: {existing_biblical_books}
- biblical_characters: {existing_biblical_characters}
- synonyms: {existing_synonyms}
- worship_style: ""{existing_worship_style}""
- energy_level: ""{existing_energy_level}""
- occasion: ""{existing_occasion}""

Analyze this stanza and return ONLY NEW tags not already listed above:
---
{paragraph}
---

Return a JSON object with these exact fields. Use empty arrays [] or empty string """" for fields with nothing new. NEVER repeat values already listed above.

Fields: themes, moods, contexts, keywords, biblical_topics, biblical_references, biblical_books, biblical_characters, synonyms, worship_style, energy_level, occasion

Return ONLY the JSON object, no other text.";

    public const string SummaryTemplate = @"Based on the complete analysis of the worship song ""{title}"" by {artist}:

Identified tags:
{tags_json}

Full lyrics:
{lyrics}

Write in Portuguese (pt-BR):
- summary: 2-3 sentence summary of the song's spiritual message
- explanation: deeper theological explanation (3-5 sentences)
- practical_application: how a worship leader should use this song in a church service (2-3 sentences)

Return ONLY a JSON object with exactly these three fields: summary, explanation, practical_application
No other text.";
}
