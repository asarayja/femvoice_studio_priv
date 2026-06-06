using System;
using System.IO;
using System.Text.Json;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    public sealed class LocalVoiceGoalProfileStore : IVoiceGoalProfileProvider
    {
        private readonly string _directory;

        public LocalVoiceGoalProfileStore(string? directory = null)
        {
            _directory = directory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FemVoiceStudio",
                "VoiceGoalProfiles");
        }

        public VoiceGoalProfile? GetProfile(int userId = 1)
        {
            var path = GetPath(userId);
            if (!File.Exists(path))
                return null;

            try
            {
                return JsonSerializer.Deserialize<VoiceGoalProfile>(File.ReadAllText(path));
            }
            catch
            {
                return null;
            }
        }

        public void SaveProfile(VoiceGoalProfile profile)
        {
            Directory.CreateDirectory(_directory);
            profile.UpdatedAt = DateTime.UtcNow;
            if (profile.CreatedAt == default)
                profile.CreatedAt = profile.UpdatedAt;

            var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(GetPath(profile.UserId), json);
        }

        private string GetPath(int userId) => Path.Combine(_directory, $"user-{userId}.json");
    }
}
