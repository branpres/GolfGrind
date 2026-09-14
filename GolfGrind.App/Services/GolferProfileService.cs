using System.Text.Json;
using GolfGrind.Core.Models;

namespace GolfGrind.App.Services;

public sealed class GolferProfileService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly AppSettingsService _settings;
    private readonly List<GolferProfile> _profiles;

    public GolferProfileService(AppSettingsService settings)
    {
        _settings = settings;
        _profiles = LoadProfiles();
        if (_profiles.Count == 0)
        {
            _profiles.Add(new GolferProfile { Name = "Primary golfer" });
            Save();
        }

        if (!_profiles.Any(profile => profile.Id == ActiveProfileId))
            SetActive(_profiles[0].Id);
    }

    public IReadOnlyList<GolferProfile> GetProfiles() => _profiles;

    public Guid ActiveProfileId
    {
        get
        {
            return _settings.ActiveProfileId is { } id ? id : _profiles[0].Id;
        }
    }

    public GolferProfile Add(string? name)
    {
        var profile = new GolferProfile { Name = CleanName(name, $"Golfer {_profiles.Count + 1}") };
        _profiles.Add(profile);
        Save();
        return profile;
    }

    public void SaveNames()
    {
        for (var index = 0; index < _profiles.Count; index++)
            _profiles[index].Name = CleanName(_profiles[index].Name, $"Golfer {index + 1}");
        Save();
    }

    public void SetActive(Guid profileId)
    {
        if (!_profiles.Any(profile => profile.Id == profileId))
            throw new InvalidOperationException("The selected golfer profile does not exist.");
        _settings.SetActiveProfile(profileId);
    }

    public bool Remove(Guid profileId)
    {
        if (_profiles.Count <= 1)
            return false;
        var removed = _profiles.RemoveAll(profile => profile.Id == profileId) > 0;
        if (removed)
            Save();
        return removed;
    }

    public void ReplaceAll(IEnumerable<GolferProfile> profiles, Guid? activeProfileId)
    {
        var replacements = profiles
            .Where(profile => profile.Id != Guid.Empty)
            .GroupBy(profile => profile.Id)
            .Select(group => group.First())
            .ToList();
        if (replacements.Count == 0)
            throw new InvalidOperationException("A backup must contain at least one golfer profile.");

        _profiles.Clear();
        _profiles.AddRange(replacements);
        SaveNames();
        SetActive(activeProfileId is { } requested && _profiles.Any(profile => profile.Id == requested)
            ? requested
            : _profiles[0].Id);
    }

    private void Save() => AtomicJsonFileStore.Write(
        GolfGrindDataPaths.ProfilesFile,
        _profiles,
        JsonOptions,
        profiles =>
        {
            if (profiles.Count == 0 || profiles.Any(profile => profile.Id == Guid.Empty || string.IsNullOrWhiteSpace(profile.Name)))
                throw new InvalidDataException("Golfer profiles must contain valid identifiers and names.");
        });

    private static List<GolferProfile> LoadProfiles()
    {
        return AtomicJsonFileStore.TryRead<List<GolferProfile>>(GolfGrindDataPaths.ProfilesFile, JsonOptions, out var profiles)
            ? profiles ?? []
            : [];
    }

    private static string CleanName(string? name, string fallback) =>
        string.IsNullOrWhiteSpace(name) ? fallback : name.Trim();
}
