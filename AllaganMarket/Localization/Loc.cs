using System.Globalization;
using System.Resources;

namespace AllaganMarket.Localization;

public static class Loc
{
    private static readonly ResourceManager ResourceManager =
        new("AllaganMarket.Resources.Strings", typeof(Loc).Assembly);

    private static CultureInfo culture = CultureInfo.InvariantCulture;

    public static void SetCulture(string languageCode)
    {
        culture = languageCode.StartsWith("zh", System.StringComparison.OrdinalIgnoreCase)
                      ? CultureInfo.GetCultureInfo("zh-TW")
                      : CultureInfo.GetCultureInfo(languageCode);
    }

    public static string T(string text)
    {
        return ResourceManager.GetString(text, culture) ?? text;
    }

    public static string F(string text, params object?[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, T(text), args);
    }
}
