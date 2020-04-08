using BepInEx;
using KKAPI;

namespace HeightBar
{
    [BepInProcess("Koikatu")]
    [BepInProcess("Koikatsu Party")]
    [BepInDependency(KoikatuAPI.GUID, "1.8")]
    public partial class HeightBar : BaseUnityPlugin
    {
        private readonly float Ratio = 103.092781f;
    }
}
