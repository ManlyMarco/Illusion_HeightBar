using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using Illusion.Extensions;
using KKAPI;
using KKAPI.Maker;
using KKAPI.Maker.UI.Sidebar;
using KKAPI.Studio;
using UniRx;
using UnityEngine;

namespace HeightBar
{
    [BepInPlugin("HeightBar", "HeightBarX", Version)]
    [BepInDependency(KoikatuAPI.GUID, "1.8")]
    public class HeightBar : BaseUnityPlugin
    {
        internal const string Version = "3.2";
        private const float Ratio = 103.092781f;

        private readonly GUIStyle _labelStyle = new GUIStyle();
        private Rect _labelRect = new Rect(400f, 400f, 100f, 100f);

        private Camera _mainCamera;

        private GameObject _barObject;
        private GameObject _zeroBarObject;

        private Transform _targetObject;

        private Material _barMaterial;
        private Material _zeroBarMaterial;
        private SidebarToggle _sidebarToggle;

        private ConfigEntry<bool> ShowZeroBar { get; set; }
        private ConfigEntry<float> BarAlpha { get; set; }
        private ConfigEntry<float> ZeroBarAlpha { get; set; }

        private bool _forceHideBars;
        public bool ForceHideBars
        {
            get => _forceHideBars;
            set
            {
                if (_forceHideBars != value)
                {
                    _forceHideBars = value;
                    Update();
                }
            }
        }

        private void Awake()
        {
            if (!KoikatuAPI.CheckRequiredPlugin(this, KoikatuAPI.GUID, new Version("1.4")) || StudioAPI.InsideStudio)
            {
                enabled = false;
                return;
            }

            ShowZeroBar = Config.Bind("Measure bars", "Show floor bar at character's feet", true, "Shows the position of the floor. Helps prevent floating characters when using yellow sliders.");
            BarAlpha = Config.Bind("Measure bars", "Opacity of the measuring bar", 0.6f, new ConfigDescription("", new AcceptableValueRange<float>(0, 1)));
            ZeroBarAlpha = Config.Bind("Measure bars", "Opacity of the floor bar", 0.5f, new ConfigDescription("", new AcceptableValueRange<float>(0, 1)));

            BarAlpha.SettingChanged += delegate
            {
                if (_barMaterial != null)
                    _barMaterial.color = new Color(1, 1, 1, BarAlpha.Value);
            };

            ZeroBarAlpha.SettingChanged += delegate
            {
                if (_zeroBarMaterial != null)
                    _zeroBarMaterial.color = new Color(1, 1, 1, ZeroBarAlpha.Value);
            };

            ShowZeroBar.SettingChanged += delegate
            {
                if (_zeroBarObject != null)
                    _zeroBarObject.SetActive(ShowZeroBar.Value);
            };

            _labelStyle.fontSize = 20;
            _labelStyle.normal.textColor = Color.white;

            MakerAPI.MakerBaseLoaded += MakerAPI_Enter;
            MakerAPI.MakerExiting += (_, __) => OnDestroy();

            var t = Type.GetType("Screencap.ScreenshotManager, Screencap");
            if (t != null)
            {
                t.GetEvent("OnPreCapture", BindingFlags.Static | BindingFlags.Public)?.AddEventHandler(null, new Action(() => ForceHideBars = true));
                t.GetEvent("OnPostCapture", BindingFlags.Static | BindingFlags.Public)?.AddEventHandler(null, new Action(() => ForceHideBars = false));
            }
        }

        private void MakerAPI_Enter(object sender, RegisterCustomControlsEvent e)
        {
            _mainCamera = Camera.main;

            var camControl = _mainCamera.GetComponent<CameraControl_Ver2>();
            if (camControl != null && camControl.targetObj != null)
                _targetObject = camControl.targetObj;
            else
                _targetObject = _mainCamera.transform;

            _barObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _barObject.transform.SetParent(MakerAPI.GetCharacterControl().objRoot.transform, false);
            _barObject.transform.localScale = new Vector3(0.3f, 0.005f, 0.3f);
            _barObject.transform.localPosition = new Vector3(0f, 0f, 0f);
            _barObject.layer = 12;
            _barObject.name = "Height bar indicator";

            _zeroBarObject = Instantiate(_barObject);
            _zeroBarObject.name = "Floor bar indicator";

            _barMaterial = new Material(Shader.Find("Standard"));
            if (_barMaterial != null)
            {
                _barMaterial.SetOverrideTag("RenderType", "Transparent");
                _barMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _barMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _barMaterial.SetInt("_ZWrite", 0);
                _barMaterial.DisableKeyword("_ALPHATEST_ON");
                _barMaterial.EnableKeyword("_ALPHABLEND_ON");
                _barMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                _barMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

                _zeroBarMaterial = Instantiate(_barMaterial);

                _barMaterial.color = new Color(1, 1, 1, BarAlpha.Value);
                _barObject.GetComponent<Renderer>().material = _barMaterial;

                _zeroBarMaterial.color = new Color(1, 1, 1, ZeroBarAlpha.Value);
                _zeroBarObject.GetComponent<Renderer>().material = _zeroBarMaterial;
            }

            _barObject.SetActive(false);
            _zeroBarObject.SetActive(ShowZeroBar.Value);

            _sidebarToggle = e.AddSidebarControl(new SidebarToggle("Show height measure bar", false, this));
            _sidebarToggle.ValueChanged.Subscribe(b => _barObject.SetActive(b));
        }

        private void OnDestroy()
        {
            _sidebarToggle = null;

            Destroy(_barObject);
            _barObject = null;
            _barMaterial = null;

            Destroy(_zeroBarObject);
            _zeroBarObject = null;
            _zeroBarMaterial = null;
        }

        private void Update()
        {
            if (_sidebarToggle != null)
            {
                var visible = IsInterfaceVisible() && !ForceHideBars;
                _zeroBarObject.SetActiveIfDifferent(visible && ShowZeroBar.Value);
                _barObject.SetActiveIfDifferent(visible && _sidebarToggle.Value);
            }
        }

        private void OnGUI()
        {
            if (_barObject == null || !_barObject.activeSelf)
                return;

            var barPosition = _barObject.transform.position;
            barPosition = new Vector3(barPosition.x, _targetObject.position.y, barPosition.z);
            _barObject.transform.position = barPosition;

            var vector = _mainCamera.WorldToScreenPoint(barPosition + new Vector3(0.1f, 0f));
            _labelRect.x = vector.x;
            _labelRect.y = Screen.height - vector.y;

            ShadowAndOutline.DrawOutline(_labelRect, (_barObject.transform.localPosition.y * Ratio).ToString("F1") + "cm", _labelStyle, Color.white, Color.black, 1);
        }

        private static bool IsInterfaceVisible()
        {
            // Check if maker is loaded
            if (!MakerAPI.InsideMaker)
                return false;
            var mbase = MakerAPI.GetMakerBase();
            if (mbase == null || mbase.chaCtrl == null)
                return false;

            // Check if the loading screen is currently visible
            if (Manager.Scene.Instance.IsNowLoadingFade)
                return false;

            // Check if UI is hidden (by pressing space)
            if (mbase.customCtrl.hideFrontUI)
                return false;

            // Check if settings screen, game exit message box or similar are on top of the maker UI
            // In KK class maker the AddSceneName is set to CustomScene, but in normal maker it's empty
            if (!string.IsNullOrEmpty(Manager.Scene.Instance.AddSceneName) && Manager.Scene.Instance.AddSceneName != "CustomScene")
                return false;

            return true;
        }
    }
}
