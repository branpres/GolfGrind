namespace GolfGrind.App.Services;

public static class GolfGrindDataPaths
{
    public static string RootDirectory { get; } = CreateRootDirectory();
    public static string SettingsFile => Path.Combine(RootDirectory, "settings.json");
    public static string ProfilesFile => Path.Combine(RootDirectory, "profiles.json");
    public static string ProfileDirectory(Guid profileId) =>
        Path.Combine(RootDirectory, "profiles", profileId.ToString("N"));
    public static string BagFile(Guid profileId) => Path.Combine(ProfileDirectory(profileId), "bag.json");
    public static string SessionsDirectory(Guid profileId) => Path.Combine(ProfileDirectory(profileId), "sessions");

    private static string CreateRootDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
            throw new InvalidOperationException("Windows did not provide a local application-data directory.");
        var root = Path.Combine(localAppData, "GolfGrind");
        Directory.CreateDirectory(root);
        return root;
    }
}
