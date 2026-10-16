using System.Text.Json;

namespace EzNihongo.Platform.Api.Kanji;

public sealed class KanjiCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IReadOnlyList<KanjiRecord> _entries;
    private readonly IReadOnlyDictionary<string, KanjiRecord> _entriesById;

    public KanjiCatalog(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configuredDirectory = configuration["Kanji:DataDirectory"] ?? "db/kanji";
        var dataDirectory = Path.IsPathRooted(configuredDirectory)
            ? configuredDirectory
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredDirectory));

        var entries = new List<KanjiRecord>();
        for (var level = 1; level <= 5; level++)
        {
            var filePath = Path.Combine(dataDirectory, $"n{level}.json");
            var catalog = ReadCatalog(filePath);
            entries.AddRange(catalog.Kanjis.Values.Where(entry => entry.Jlpt == level));
        }

        entries.AddRange(ReadCatalog(Path.Combine(dataDirectory, "extra.json")).Kanjis.Values
            .Where(entry => entry.Jlpt is null));

        _entries = entries;
        _entriesById = entries
            .GroupBy(entry => entry.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
    }

    public KanjiCatalogCounts GetCounts() => new(
        _entries.Where(entry => entry.Jlpt.HasValue)
            .GroupBy(entry => entry.Jlpt!.Value)
            .OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.Count()),
        _entries.Count(entry => entry.Jlpt is null));

    public KanjiPage GetPage(int? level, string? collection, int offset, int limit)
    {
        IEnumerable<KanjiRecord> filtered = _entries;
        if (collection == "jlpt") filtered = filtered.Where(entry => entry.Jlpt.HasValue);
        if (collection == "extra") filtered = filtered.Where(entry => entry.Jlpt is null);
        if (level.HasValue) filtered = filtered.Where(entry => entry.Jlpt == level.Value);
        var matches = filtered.ToArray();
        return new KanjiPage(matches.Length, offset, limit, matches.Skip(offset).Take(limit).ToArray());
    }

    public KanjiRecord? Find(string id) => _entriesById.GetValueOrDefault(id);

    private static KanjiCatalogFile ReadCatalog(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Kanji catalog data was not found.", filePath);

        return JsonSerializer.Deserialize<KanjiCatalogFile>(File.ReadAllText(filePath), JsonOptions)
            ?? throw new InvalidDataException($"Kanji catalog file '{filePath}' is empty or invalid.");
    }

    private sealed record KanjiCatalogFile
    {
        public Dictionary<string, KanjiRecord> Kanjis { get; init; } = new(StringComparer.Ordinal);
    }
}
