namespace EzNihongo.Platform.Api.Vocabulary;

public sealed record VocabularyEntry(
    string ContentId,
    string Word,
    string Meaning,
    string Furigana,
    string Romaji,
    int Level);
