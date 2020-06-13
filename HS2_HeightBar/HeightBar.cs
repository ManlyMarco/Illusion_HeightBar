using BepInEx;
using KKAPI;

namespace HeightBar
{
    [BepInProcess("HoneySelect2")]
    [BepInDependency(KoikatuAPI.GUID, "1.10")]
    public partial class HeightBar : BaseUnityPlugin
    {
        private readonly float Ratio = 10.5f;
    }
}
