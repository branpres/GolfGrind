using GolfGrind.App.Services;
using GolfGrind.Core.Models;

namespace GolfGrind.App.Components.Pages;

public partial class Home
{
    private async Task ProfileChangedAsync()
    {
        if (selectedProfileId == loadedProfileId)
            return;
        SaveActiveBag();
        LoadSelectedProfile();
        await ClubChangedAsync();
    }

    private async Task SetProfileAsync(Guid profileId)
    {
        selectedProfileId = profileId;
        await ProfileChangedAsync();
    }

    private async Task UseProfileAsync(Guid profileId)
    {
        selectedProfileId = profileId;
        await ProfileChangedAsync();
    }

    private async Task AddProfileAsync()
    {
        SaveActiveBag();
        var profile = ProfileService.Add(newProfileName);
        profiles = ProfileService.GetProfiles().ToList();
        selectedProfileId = profile.Id;
        newProfileName = "";
        LoadSelectedProfile();
        profileMessage = $"{profile.Name} was added and is now active.";
        await ClubChangedAsync();
    }

    private async Task AddProfileFromPanelAsync(string name)
    {
        newProfileName = name;
        await AddProfileAsync();
    }

    private void SaveProfileNames()
    {
        ProfileService.SaveNames();
        profiles = ProfileService.GetProfiles().ToList();
        profileMessage = "Golfer names saved.";
    }

    private async Task RequestDeleteProfileAsync(Guid profileId)
    {
        if (profiles.Count <= 1)
            return;
        if (pendingDeleteProfileId != profileId)
        {
            pendingDeleteProfileId = profileId;
            profileMessage = "Click Confirm delete to permanently remove this golfer's bag and sessions.";
            return;
        }

        var deletedName = profiles.First(profile => profile.Id == profileId).Name;
        BagService.Delete(profileId);
        SessionStorage.DeleteProfile(profileId);
        ProfileService.Remove(profileId);
        profiles = ProfileService.GetProfiles().ToList();
        pendingDeleteProfileId = null;
        if (profileId == loadedProfileId)
        {
            selectedProfileId = profiles[0].Id;
            LoadSelectedProfile();
            await ClubChangedAsync();
        }
        profileMessage = $"{deletedName} and that golfer's locally stored data were deleted.";
    }

    private void LoadSelectedProfile()
    {
        ProfileService.SetActive(selectedProfileId);
        loadedProfileId = selectedProfileId;
        clubs = BagService.Load(selectedProfileId);
        SessionStorage.SwitchProfile(selectedProfileId);
        ActivitySessions.Initialize();
        selectedClubId = clubs.FirstOrDefault(club => club.Name.Equals("7 Iron", StringComparison.OrdinalIgnoreCase))?.Id ?? clubs[0].Id;
        selectedSwingType = "Half";
        analyticsClubId = selectedClubId;
        selectedHistorySessionId = null;
        pendingDeleteSessionId = null;
        pendingDeleteShotAt = null;
        practiceGameState = new();
        MonitorWorkspace.ResetProcessing();
        wedgeGuide.Reset();
        bagGuide.ResetSelection(clubs);
        wedgeGuide.ResetSelection(WedgeClubs);
    }

    private void SaveActiveBag() => BagService.Save(loadedProfileId, clubs);

    private List<GolferBackup> BuildGolferBackups() => profiles.Select(profile => new GolferBackup
    {
        Profile = profile,
        Bag = profile.Id == loadedProfileId ? clubs.ToList() : BagService.Load(profile.Id),
        Sessions = (profile.Id == loadedProfileId ? SessionStorage.GetSessions() : SessionStorage.ReadProfile(profile.Id)).ToList()
    }).ToList();

    private void ExportBackup()
    {
        try
        {
            SaveActiveBag();
            var path = BackupService.ExportJson(BuildGolferBackups(), selectedProfileId);
            backupMessage = $"Complete backup saved to {path}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            backupMessage = $"Backup could not be written: {exception.Message}";
        }
    }

    private void ExportCsv()
    {
        try
        {
            SaveActiveBag();
            var path = BackupService.ExportCsv(BuildGolferBackups());
            backupMessage = $"Shot export saved to {path}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            backupMessage = $"CSV could not be written: {exception.Message}";
        }
    }

    private async Task ChooseBackupAsync()
    {
        try
        {
            var result = await Microsoft.Maui.Storage.FilePicker.Default.PickAsync(new Microsoft.Maui.Storage.PickOptions
            {
                PickerTitle = "Choose a Golf Grind JSON backup"
            });
            if (result is null)
                return;
            pendingBackup = BackupService.ReadJson(result.FullPath);
            backupMessage = null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException)
        {
            pendingBackup = null;
            backupMessage = $"Backup could not be opened: {exception.Message}";
        }
    }

    private void CancelBackupImport()
    {
        pendingBackup = null;
        backupMessage = "Restore cancelled; local data was not changed.";
    }

    private void ConfirmBackupImport()
    {
        if (pendingBackup is null)
            return;

        try
        {
            var previousProfileIds = profiles.Select(profile => profile.Id).ToList();
            foreach (var golfer in pendingBackup.Golfers!)
            {
                BagService.Save(golfer.Profile.Id, golfer.Bag);
                SessionStorage.ReplaceProfile(golfer.Profile.Id, golfer.Sessions);
            }
            ProfileService.ReplaceAll(
                pendingBackup.Golfers.Select(golfer => golfer.Profile),
                pendingBackup.ActiveGolferId);
            profiles = ProfileService.GetProfiles().ToList();
            var restoredProfileIds = profiles.Select(profile => profile.Id).ToHashSet();
            foreach (var removedProfileId in previousProfileIds.Where(profileId => !restoredProfileIds.Contains(profileId)))
            {
                BagService.Delete(removedProfileId);
                SessionStorage.DeleteProfile(removedProfileId);
            }
            selectedProfileId = ProfileService.ActiveProfileId;
            LoadSelectedProfile();
            pendingBackup = null;
            backupMessage = "Backup restored for every golfer. A new empty session has been created for the active profile.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            backupMessage = $"Backup could not be restored: {exception.Message}";
        }
    }
}
