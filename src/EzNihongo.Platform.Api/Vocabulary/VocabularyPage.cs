namespace EzNihongo.Platform.Api.Vocabulary;

public sealed record VocabularyPage(
    int Total,
    int Offset,
    int Limit,
    IReadOnlyList<VocabularyEntry> Words);
