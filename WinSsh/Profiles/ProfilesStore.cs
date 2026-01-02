using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;

namespace WinSsh.Profiles;

public sealed class ProfilesStore
{
    private static readonly JsonSerializerOptions _jsonSerializationOPtions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string FilePath { get; }

    private ProfilesStore(string filePath) => FilePath = filePath;

    public static ProfilesStore CreateDefault()
    {
        var localFolder = ApplicationData.Current.LocalFolder.Path;
        var filePath = Path.Combine(localFolder, "profiles.json");
        return new ProfilesStore(filePath);
    }

    public async Task<List<HostProfile>> LoadAsync()
    {
        if (!File.Exists(FilePath))
            return new List<HostProfile>();

        await using var fs = File.OpenRead(FilePath);
        return (await JsonSerializer.DeserializeAsync<List<HostProfile>>(fs, _jsonSerializationOPtions)) ?? new List<HostProfile>();
    }

    public async Task AddProfile(HostProfile profile)
    {
        var profiles = await LoadAsync();
        profiles.Add(profile);
        await SaveAsync(profiles);
    }

    public async Task DeleteProfile(HostProfile profile)
    {
        var profiles = await LoadAsync();
        profiles.RemoveAll(p => p.Id == profile.Id);
        
        // Clean up associated SSH key
        if (!string.IsNullOrWhiteSpace(profile.IdentityFile))
        {
            await SshKeyManager.DeleteKeyAsync(profile.IdentityFile);
        }
        
        await SaveAsync(profiles);
    }

    public async Task SaveAsync(List<HostProfile> profiles)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

        var tmp = FilePath + ".tmp";
        await using (var fs = File.Create(tmp))
            await JsonSerializer.SerializeAsync(fs, profiles, _jsonSerializationOPtions);

        File.Copy(tmp, FilePath, overwrite: true);
        File.Delete(tmp);
    }

    internal async Task UpdateProfile(HostProfile profile)
    {
        var profiles = await LoadAsync();
        profiles.Remove(profiles.First(_ => _.Id == profile.Id));
        profiles.Add(profile);

        await SaveAsync(profiles);
    }
}