using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    public interface IVoiceGoalProfileProvider
    {
        VoiceGoalProfile? GetProfile(int userId = 1);
        void SaveProfile(VoiceGoalProfile profile);
    }
}
