using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using Illusion.Extensions;
using KKAPI.Maker;
using KKAPI.Maker.UI.Sidebar;
using KKAPI.Utilities;
using UniRx;
using UnityEngine;

namespace HeightBar
{
    [BepInPlugin("HeightBar", "HeightBarX", Version)]
    public partial class HeightBar
    {
        internal const string Version = "3.5";

        private readonly GUIStyle _labelStyle = new GUIStyle();
        private Rect _labelRect = new Rect(400f, 400f, 100f, 100f);

        private Camera _mainCamera;

        private GameObject _barObject;
        private GameObject _zeroBarObject;

        private Transform _targetObject;

        private Material _barMaterial;
        private Material _zeroBarMaterial;
        private SidebarToggle _sidebarToggle;

        private ConfigEntry<bool> _showZeroBar;
        private ConfigEntry<bool> _useFeet;
        private ConfigEntry<float> _barAlpha;
        private ConfigEntry<float> _zeroBarAlpha;

        private bool _showBar;
        private ConfigEntry<KeyboardShortcut> _barHotkey;

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
            _barHotkey = Config.Bind("General", "Toggle height measure bar", KeyboardShortcut.Empty, "Hotkey to toggle the height measurement bar in maker.");
            _showZeroBar = Config.Bind("General", "Show floor bar at character`s feet", true, "Shows the position of the floor. Helps prevent floating characters when using yellow sliders.");
            _useFeet = Config.Bind("General", "Use freedom units", false, "A foot to the face.");

            _barAlpha = Config.Bind("Appearance", "Opacity of the measuring bar", 0.6f, new ConfigDescription("", new AcceptableValueRange<float>(0, 1)));
            _zeroBarAlpha = Config.Bind("Appearance", "Opacity of the floor bar", 0.5f, new ConfigDescription("", new AcceptableValueRange<float>(0, 1)));

            _barAlpha.SettingChanged += delegate
            {
                if (_barMaterial != null)
                    _barMaterial.color = new Color(0, 0, 0, _barAlpha.Value);
            };

            _zeroBarAlpha.SettingChanged += delegate
            {
                if (_zeroBarMaterial != null)
                    _zeroBarMaterial.color = new Color(0, 0, 0, _zeroBarAlpha.Value);
            };

            _showZeroBar.SettingChanged += delegate
            {
                if (_zeroBarObject != null)
                    _zeroBarObject.SetActive(_showZeroBar.Value);
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

        private string CentimetresToFeet(float centimetres) {
            float feetAndInches = centimetres * 0.0328084f;
            //int feet = (int) Math.Truncate(feetAndInches);
            //int inches = (int) Math.Truncate((feetAndInches - feet) * 12);

            //Console.WriteLine($"{centimetres}cm");
            //Console.WriteLine($"{feetAndInches}'");
            //Console.WriteLine($"{feet}'");
            //Console.WriteLine($"{inches}\"");

            //return $"{feet}' {inches}\"";
            return $"{(int)Math.Truncate(feetAndInches)}' {(int)Math.Truncate((feetAndInches - feet) * 12)}\"";
        }

        private void MakerAPI_Enter(object sender, RegisterCustomControlsEvent e)
        {
            _showBar = false;
            _mainCamera = Camera.main;

            var camControl = _mainCamera.GetComponent<CameraControl_Ver2>();
#if KK || KKS
            if (camControl != null && camControl.targetObj != null)
                _targetObject = camControl.targetObj;
#elif AI || EC || HS2
            if (camControl != null && camControl.targetTex != null)
                _targetObject = camControl.targetTex;
#endif
            else
                _targetObject = _mainCamera.transform;

            var res = ResourceUtils.GetEmbeddedResource("flat_color_cube.unity3d");
            var ab = AssetBundle.LoadFromMemory(res);
            var origCube = ab.LoadAsset<GameObject>("FlatColorCube");

            _barObject = Instantiate(origCube);
            _barObject.transform.SetParent(MakerAPI.GetCharacterControl().objRoot.transform, false);
            _barObject.transform.localPosition = new Vector3(0f, 0f, 0f);
#if KK
            _barObject.transform.localScale = new Vector3(0.3f, 0.005f, 0.3f);
            _barObject.layer = 12;
#elif EC || KKS
            _barObject.transform.localScale = new Vector3(0.3f, 0.005f, 0.3f);
            _barObject.layer = 10;
#elif AI || HS2
            _barObject.transform.localScale = new Vector3(3f, 0.02f, 3f);
            _barObject.layer = 10;
#endif
            _barObject.name = "Height bar indicator";

            _zeroBarObject = Instantiate(_barObject);
            _zeroBarObject.name = "Floor bar indicator";

            _barMaterial = _barObject.GetComponent<Renderer>().material;
            _barMaterial.color = new Color(0, 0, 0, _barAlpha.Value);

            _zeroBarMaterial = _zeroBarObject.GetComponent<Renderer>().material;
            _zeroBarMaterial.color = new Color(0, 0, 0, _zeroBarAlpha.Value);

            _barObject.SetActive(false);
            _zeroBarObject.SetActive(_showZeroBar.Value);

            ab.Unload(false);

            _sidebarToggle = e.AddSidebarControl(new SidebarToggle("Show height measure bar", false, this));
            _sidebarToggle.Value = _showBar;
            _sidebarToggle.ValueChanged.Subscribe(b => _showBar = b);
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
            if (_barObject != null)
            {
                if (_barHotkey.Value.IsDown()) _showBar = !_showBar;
                var visible = MakerAPI.IsInterfaceVisible() && !ForceHideBars;
                _zeroBarObject.SetActiveIfDifferent(visible && _showZeroBar.Value);
                _barObject.SetActiveIfDifferent(visible && _showBar);
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
#if KK || EC || KKS
            _labelRect.x = vector.x;
            _labelRect.y = Screen.height - vector.y;
#else
            // Clamp the last digit to deal with BetterAA shaking the screen
            _labelRect.x = (int)(vector.x / 10) * 10;
            _labelRect.y = (int)((Screen.height - vector.y) / 10) * 10;
#endif

            var cmHeight = _barObject.transform.localPosition.y * Ratio;
            var value = _useFeet.Value
                ? CentimetresToFeet(cmHeight)
                : cmHeight.ToString("F1") + "cm";

            ShadowAndOutline.DrawOutline(_labelRect, value, _labelStyle, Color.white, Color.black, 1);
        }
    }
}
