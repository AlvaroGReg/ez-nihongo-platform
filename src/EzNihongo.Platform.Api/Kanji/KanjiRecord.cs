using System.Text.Json.Serialization;

namespace EzNihongo.Platform.Api.Kanji;

/// <summary>A kanji entry in the JLPT or additional kanji catalog.</summary>
public sealed record KanjiRecord(
    [property: JsonPropertyName("grade")] int? Grade,
    [property: JsonPropertyName("heisig_en")] string? HeisigEn,
    [property: JsonPropertyName("jlpt")] int? Jlpt,
    [property: JsonPropertyName("kanji")] string Kanji,
    [property: JsonPropertyName("kun_readings")] IReadOnlyList<string> KunReadings,
    [property: JsonPropertyName("meanings_en")] IReadOnlyList<string> MeaningsEn,
    [property: JsonPropertyName("meanings_es")] IReadOnlyList<string> MeaningsEs,
    [property: JsonPropertyName("name_readings")] IReadOnlyList<string> NameReadings,
    [property: JsonPropertyName("on_readings")] IReadOnlyList<string> OnReadings,
    [property: JsonPropertyName("unicode")] string Unicode,
    [property: JsonPropertyName("id")] string Id);

/// <summary>A page of kanji entries and pagination metadata.</summary>
public sealed record KanjiPage(
    int Total,
    int Offset,
    int Limit,
    IReadOnlyList<KanjiRecord> Kanjis);

/// <summary>Kanji counts grouped by JLPT level and additional collection.</summary>
public sealed record KanjiCatalogCounts(
    IReadOnlyDictionary<int, int> Levels,
    int Extra);
