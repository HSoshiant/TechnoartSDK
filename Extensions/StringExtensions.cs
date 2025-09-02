namespace TechnoartSDK.Extensions;

public static class StringExtensions
{
    public static string ReplaceFirst(this string text, string search, string replace, StringComparison stringComparison)
    {
        int pos = text.IndexOf(search, stringComparison);
        if (pos < 0)
            return text;

        return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
    }
}
