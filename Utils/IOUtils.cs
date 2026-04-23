namespace TechnoartSDK.Utils;

/// <summary>
/// File system utility methods.
/// </summary>
public static class IOUtils
{
    /// <summary>
    /// Removes characters that are invalid in file names.
    /// </summary>
    public static string MakeFileNameSafe(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var safeName = new string(name.Where(ch => !invalidChars.Contains(ch)).ToArray());
        return safeName;
    }
}
