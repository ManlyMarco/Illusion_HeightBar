using BepInEx;
using KKAPI;

namespace HeightBar
{
    [BepInProcess(KoikatuAPI.GameProcessName)]
    [BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
    public partial class HeightBar : BaseUnityPlugin
    {
        private readonly float Ratio = 103.092781f;
    }
}
