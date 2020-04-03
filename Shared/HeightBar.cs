using BepInEx;
using KKAPI;

namespace HeightBar
{
    [BepInPlugin("HeightBar", "HeightBarX", Version)]
    [BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
    public partial class HeightBar
    {
        internal const string Version = "3.3";
    }
}
