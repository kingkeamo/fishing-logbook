namespace FishingLogBook.Db.Migrations;

/// <summary>
/// Orders migration scripts by their <c>YYYYMMDDHHMM_Description.sql</c> filename only,
/// ignoring the numbered folder they live in. This guarantees scripts run in true
/// chronological order across folders (e.g. a seed authored before a later table change
/// still runs first), with the full resource name used as a stable tie-breaker.
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

        // Compare alphabetically (which gives us timestamp order due to the YYYYMMDDHHMI prefix).
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
            var errorMessage = $"Script name '{scriptName}' does not follow the expected naming convention. YEAR:MONTH:DAY:HOUR:MINUTE_DESCRIPTION example - 202306120905_CreateSystemTest.sql ";
            throw new InvalidOperationException(errorMessage);
        }

        return string.Join(".", parts[4..]);
    }
}
