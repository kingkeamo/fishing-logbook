namespace FishingLogBook.Db.Migrations;

/// <summary>
/// Orders scripts by filename only. New files:
/// <c>YYYYMMDDHHMM_{GitHubIssue}_{Description}.sql</c>. Sort key is the timestamp prefix.
/// </summary>
public class FilenameOnlyScriptComparer : IComparer<string>
{
    public int Compare(string? scriptOne, string? scriptTwo)
    {
        if (scriptOne == null && scriptTwo == null)
        {
            return 0;
        }

        if (scriptOne == null)
        {
            return -1;
        }

        if (scriptTwo == null)
        {
            return 1;
        }

        var scriptOneFilename = GetFilename(scriptOne);
        var scriptTwoFilename = GetFilename(scriptTwo);

        // Compare alphabetically (timestamp order from the YYYYMMDDHHMM prefix).
        var filenameComparison = string.Compare(scriptOneFilename, scriptTwoFilename, StringComparison.Ordinal);

        if (filenameComparison == 0)
        {
            return string.Compare(scriptOne, scriptTwo, StringComparison.Ordinal);
        }

        return filenameComparison;
    }

    private static string GetFilename(string scriptName)
    {
        if (!scriptName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Script name '{scriptName}' is not a valid SQL script. Must end with .sql");
        }

        var nameWithoutExtension = scriptName.TrimEnd(".sql".ToCharArray());
        var parts = nameWithoutExtension.Split('.');

        // First 4 parts are: FishingLogBook.Db.Migrations.<FolderName>
        if (parts.Length < 5)
        {
            var errorMessage = $"Script name '{scriptName}' does not follow the expected naming convention. Example: 202608141200_3_AddCatchTable.sql";
            throw new InvalidOperationException(errorMessage);
        }

        return string.Join(".", parts[4..]);
    }
}
