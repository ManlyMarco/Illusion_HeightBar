using BepInEx;
using KKAPI;

namespace HeightBar
{
    [BepInPlugin("HeightBar", "HeightBarX", Version)]
    [BepInDependency(KoikatuAPI.GUID, "1.9")]
    public partial class HeightBar
    {
        internal const string Version = "3.2";
    }
}
