using System.Text.Json;

namespace EzNihongo.Platform.Api.Vocabulary;

public sealed class VocabularyCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IReadOnlyList<VocabularyEntry> _entries;
    private readonly IReadOnlyDictionary<string, VocabularyEntry> _entriesById;

    public VocabularyCatalog(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configuredDirectory = configuration["Vocabulary:DataDirectory"] ?? "db";
        string[] candidateDirectories = Path.IsPathRooted(configuredDirectory)
            ? [configuredDirectory]
            : [
                Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredDirectory)),
                Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "..", configuredDirectory)),
            ];
        var dataDirectory = candidateDirectories.FirstOrDefault(Directory.Exists)
            ?? candidateDirectories[0];

        var entries = new List<VocabularyEntry>();
        for (var level = 1; level <= 5; level++)
        {
            var filePath = Path.Combine(dataDirectory, $"n{level}.json");
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Vocabulary data for JLPT N{level} was not found.", filePath);
            }

            var sourceEntries = JsonSerializer.Deserialize<List<SourceVocabularyEntry>>(
                File.ReadAllText(filePath), JsonOptions)
                ?? throw new InvalidDataException($"Vocabulary data file '{filePath}' is empty or invalid.");

            entries.AddRange(sourceEntries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Word)
                    && !string.IsNullOrWhiteSpace(entry.Romaji)
                    && !string.IsNullOrWhiteSpace(entry.Meaning?.En))
                .Select(entry => new VocabularyEntry(
                    entry.Uuid ?? CreateContentId(entry.Word!, entry.Furigana ?? string.Empty),
                    entry.Word!,
                    entry.Meaning!.En!,
                    entry.Meaning.Es,
                    entry.Furigana ?? string.Empty,
                    entry.Romaji!,
                    level)));
        }

        _entries = entries;
        _entriesById = entries
            .GroupBy(entry => entry.ContentId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
    }

    public IReadOnlyDictionary<int, int> GetLevelCounts() =>
        _entries.GroupBy(entry => entry.Level)
            .OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.Count());

    public VocabularyPage GetPage(int? level, int offset, int limit, string language)
    {
        var filteredEntries = level is null
            ? _entries
            : _entries.Where(entry => entry.Level == level.Value).ToArray();

        return new VocabularyPage(
            filteredEntries.Count,
            offset,
            limit,
            filteredEntries.Skip(offset).Take(limit).Select(entry => entry.ForLocale(language)).ToArray());
    }

    public VocabularyEntry? Find(string contentId, string language) =>
        _entriesById.GetValueOrDefault(contentId)?.ForLocale(language);

    private static string CreateContentId(string word, string furigana) =>
        $"vocabulary:{Uri.EscapeDataString(word.Normalize())}:{Uri.EscapeDataString(furigana.Normalize())}";

    private sealed class SourceVocabularyEntry
    {
        public string? Uuid { get; init; }
        public string? Word { get; init; }
        public SourceMeaning? Meaning { get; init; }
        public string? Furigana { get; init; }
        public string? Romaji { get; init; }
    }

    private sealed class SourceMeaning
    {
        public string? En { get; init; }
        public string? Es { get; init; }
    }
}
