using System.Text;
using System.Text.RegularExpressions;

namespace Dorosak.Application.Features.Catalog;

public static partial class SearchTextNormalizer
{
    public static string ArabicVersion => "ar-v1";

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"[^\w\s]")]
    private static partial Regex PunctuationRegex();

    [GeneratedRegex(@"[\u064B-\u065F\u0670\u0640]")]
    private static partial Regex ArabicDiacriticsAndTatweelRegex();

    public static string Normalize(string input, string locale)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var text = input;
        
        if (locale.StartsWith("ar", StringComparison.OrdinalIgnoreCase))
        {
            text = ArabicDiacriticsAndTatweelRegex().Replace(text, string.Empty);
            text = Regex.Replace(text, "[أإآ]", "ا");
            text = text.Replace("ى", "ي");
        }

        text = PunctuationRegex().Replace(text, " ");
        text = WhitespaceRegex().Replace(text, " ");
        return text.Trim().ToLowerInvariant();
    }
}
