using System.Text.Json.Serialization;

namespace EzNihongo.Platform.Api.Vocabulary;

public sealed record VocabularyEntry(
    string ContentId,
    string Word,
    string Meaning,
    [property: JsonIgnore] string? MeaningEs,
    string Furigana,
    string Romaji,
    int Level)
{
    public VocabularyEntry ForLocale(string language) => this with
    {
        Meaning = language == "es" && !string.IsNullOrWhiteSpace(MeaningEs) ? MeaningEs : Meaning,
    };
}
