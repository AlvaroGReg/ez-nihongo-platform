using System.Globalization;
using System.Text;

namespace EzNihongo.Platform.Api.KnownContent;

public static class KnownContentNormalization
{
    public static string NormalizePresented(string value) => value.Trim().Normalize(NormalizationForm.FormC);

    public static string NormalizeWriting(string value) =>
        NormalizePresented(value).Normalize(NormalizationForm.FormKC);

    public static string NormalizeReading(string value)
    {
        var normalized = NormalizeWriting(value);
        var result = new StringBuilder(normalized.Length);
        foreach (var rune in normalized.EnumerateRunes())
        {
            var scalar = rune.Value switch
            {
                >= 0x30A1 and <= 0x30F6 => rune.Value - 0x60,
                0x30FD => 0x309D,
                0x30FE => 0x309E,
                _ => rune.Value
            };
            result.Append(new Rune(scalar));
        }

        return result.ToString();
    }

    public static int ScalarLength(string value) => value.EnumerateRunes().Count();

    public static bool IsKanaReading(string value) => value.EnumerateRunes().All(rune =>
        rune.Value is >= 0x3041 and <= 0x3096 or 0x309D or 0x309E or
            >= 0x30A1 and <= 0x30FA or 0x30FD or 0x30FE or 0x30FC);

    public static bool IsKanjiCharacter(string value)
    {
        if (ScalarLength(value) != 1)
            return false;

        var category = Rune.GetUnicodeCategory(value.EnumerateRunes().Single());
        var scalar = value.EnumerateRunes().Single().Value;
        var isHan = scalar is >= 0x3400 and <= 0x4DBF or >= 0x4E00 and <= 0x9FFF or
            >= 0x20000 and <= 0x323AF or >= 0xF900 and <= 0xFAFF or >= 0x2F800 and <= 0x2FA1F;
        return category == UnicodeCategory.OtherLetter && isHan;
    }
}
