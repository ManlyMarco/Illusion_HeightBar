using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using Illusion.Extensions;
using KKAPI;
using KKAPI.Maker;
using KKAPI.Maker.UI.Sidebar;
using KKAPI.Utilities;
using UniRx;
using UnityEngine;

namespace HeightBar
{
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
    [BepInProcess(KoikatuAPI.GameProcessName)]
#if KK
    [BepInProcess(KoikatuAPI.GameProcessNameSteam)]
#endif
    public class HeightBar : BaseUnityPlugin
    {
        public const string Version = "3.4.1";
        public const string GUID = "HeightBar";
        public const string PluginName = "HeightBarX";

#if KK || KKS || EC
        private readonly float Ratio = 103.092781f;
#elif AI || HS2
        private readonly float Ratio = 10.5f;
#else
        TODO FIX
#endif

        private readonly GUIStyle _labelStyle = new GUIStyle();
        private Rect _labelRect = new Rect(400f, 400f, 100f, 100f);
        private Rect _labelWidthRect = new Rect(400f, 400f, 100f, 100f);

        private Camera _mainCamera;

        private GameObject _heightBarObject;
        private GameObject _widthBarObject;
        private GameObject _zeroHeightBarObject;
        private GameObject _zeroWidthBarObject;

        private Transform _targetObject;
        private Vector3 _differentialPoint = Vector3.zero;

        private Material _heightBarMaterial;
        private Material _widthBarMaterial;
        private Material _zeroHeightBarMaterial;
        private Material _zeroWidthBarMaterial;
        private SidebarToggle _sidebarHeightToggle;
        private SidebarToggle _sidebarWidthToggle;

        private ConfigEntry<bool> _showZeroHeightBar;
        private ConfigEntry<bool> _showZeroWidthBar;
        private ConfigEntry<DisplayUnits> _displayUnit;
        private ConfigEntry<Color> _heightBarColor;
        private ConfigEntry<Color> _widthBarColor;
        private ConfigEntry<Color> _zeroHeightBarColor;
        private ConfigEntry<Color> _zeroWidthBarColor;

        private bool _showHeightBar;
        private bool _showWidthBar;
        private ConfigEntry<KeyboardShortcut> _heightBarHotkey;
        private ConfigEntry<KeyboardShortcut> _widthBarHotkey;
        private ConfigEntry<KeyboardShortcut> _differentialHotkey;

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
            _heightBarHotkey = Config.Bind("General", "Toggle height measure bar", KeyboardShortcut.Empty, "Hotkey to toggle the height measurement bar in maker.");
            _widthBarHotkey = Config.Bind("General", "Toggle width measure bar", KeyboardShortcut.Empty, "Hotkey to toggle the width measurement bar in maker.");
            _differentialHotkey = Config.Bind("General", "Set Differential Measurement Point", KeyboardShortcut.Empty, "Hotkey to set a point for differential measurements.");

            _showZeroHeightBar = Config.Bind("General", "Show floor bar at character`s feet", true, "Shows the position of the floor. Helps prevent floating characters when using yellow sliders.");
            _showZeroWidthBar = Config.Bind("General", "Show bar at zero width.", true, "Shows the position of the zero point of width.");

            _displayUnit = Config.Bind("General", "Units", DisplayUnits.Both, "Allows you the change the units in which height is displayed.");

            _heightBarColor = Config.Bind("Appearance", "Color of the height measuring bar", new Color(0, 0, 0, 0.6f));
            _widthBarColor = Config.Bind("Appearance", "Color of the width measuring bar", new Color(0, 0, 0, 0.6f));

            _zeroHeightBarColor = Config.Bind("Appearance", "Color of the floor bar", new Color(0, 0, 0, 0.5f));
            _zeroWidthBarColor = Config.Bind("Appearance", "Color of the zero width bar", new Color(0, 0, 0, 0.5f));

            _heightBarColor.SettingChanged += delegate
            {
                if (_heightBarMaterial != null)
                    _heightBarMaterial.color = _heightBarColor.Value;
            };

            _widthBarColor.SettingChanged += delegate
            {
                if (_widthBarColor != null)
                    _widthBarMaterial.color = _widthBarColor.Value;
            };

            _zeroHeightBarColor.SettingChanged += delegate
            {
                if (_zeroHeightBarMaterial != null)
                    _zeroHeightBarMaterial.color = _zeroHeightBarColor.Value;
            };

            _showZeroHeightBar.SettingChanged += delegate
            {
                if (_zeroHeightBarObject != null)
                    _zeroHeightBarObject.SetActive(_showZeroHeightBar.Value);
            };

            _zeroWidthBarColor.SettingChanged += delegate
            {
                if (_zeroWidthBarMaterial != null)
                    _zeroWidthBarMaterial.color = _zeroWidthBarColor.Value;
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

        private string CentimetresToInches(float centimetres)
        {
            return $"{centimetres / 2.54:F2}\"";
        }

        private string CentimetresToFeet(float centimetres)
        {
            var feetAndInches = centimetres * 0.0328084f;
            var feet = (int)Math.Truncate(feetAndInches);

            return $"{feet}' {(feetAndInches - feet) * 12:F1}\"";
        }

        private void MakerAPI_Enter(object sender, RegisterCustomControlsEvent e)
        {
            _showHeightBar = false;
            _showWidthBar = false;
            _differentialPoint = Vector3.zero;
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

            _heightBarObject = Instantiate(origCube);
            _heightBarObject.transform.SetParent(MakerAPI.GetCharacterControl().objRoot.transform, false);
            _heightBarObject.transform.localPosition = new Vector3(0f, 0f, 0f);

#if KK
            _heightBarObject.transform.localScale = new Vector3(0.3f, 0.005f, 0.3f);
            _heightBarObject.layer = 12;
#elif EC || KKS
            _heightBarObject.transform.localScale = new Vector3(0.3f, 0.005f, 0.3f);
            _heightBarObject.layer = 10;
#elif AI || HS2
            _heightBarObject.transform.localScale = new Vector3(3f, 0.02f, 3f);
            _heightBarObject.layer = 10;
#endif
            _heightBarObject.name = "Height bar indicator";

            _widthBarObject = Instantiate(_heightBarObject);
            _widthBarObject.name = "Width bar indicator";
            _widthBarObject.transform.localEulerAngles = new Vector3(0, 0, 90);

            _zeroHeightBarObject = Instantiate(_heightBarObject);
            _zeroHeightBarObject.name = "Height floor bar indicator";

            _zeroWidthBarObject = Instantiate(_widthBarObject);
            _zeroWidthBarObject.name = "Width floor bar indicator";

            _heightBarMaterial = _heightBarObject.GetComponent<Renderer>().material;
            _heightBarMaterial.color = _heightBarColor.Value;

            _widthBarMaterial = _widthBarObject.GetComponent<Renderer>().material;
            _widthBarMaterial.color = _widthBarColor.Value;

            _zeroHeightBarMaterial = _zeroHeightBarObject.GetComponent<Renderer>().material;
            _zeroHeightBarMaterial.color = _zeroHeightBarColor.Value;

            _zeroWidthBarMaterial = _zeroWidthBarObject.GetComponent<Renderer>().material;
            _zeroWidthBarMaterial.color = _zeroWidthBarColor.Value;

            _heightBarObject.SetActive(false);
            _widthBarObject.SetActive(false);
            _zeroHeightBarObject.SetActive(_showZeroHeightBar.Value);
            _zeroHeightBarObject.SetActive(_showZeroWidthBar.Value);

            ab.Unload(false);

            _sidebarHeightToggle = e.AddSidebarControl(new SidebarToggle("Show height measure bar", false, this));
            _sidebarHeightToggle.Value = _showHeightBar;
            _sidebarHeightToggle.ValueChanged.Subscribe(b => _showHeightBar = b);

            _sidebarWidthToggle = e.AddSidebarControl(new SidebarToggle("Show width measure bar", false, this));
            _sidebarWidthToggle.Value = _showWidthBar;
            _sidebarWidthToggle.ValueChanged.Subscribe(b => _showWidthBar = b);
        }

        private void OnDestroy()
        {
            _sidebarHeightToggle = null;

            Destroy(_heightBarObject);
            _heightBarObject = null;
            _heightBarMaterial = null;

            Destroy(_zeroHeightBarObject);
            _zeroHeightBarObject = null;
            _zeroHeightBarMaterial = null;

            Destroy(_widthBarObject);
            _widthBarObject = null;
            _widthBarMaterial = null;

            Destroy(_zeroWidthBarObject);
            _zeroWidthBarObject = null;
            _zeroWidthBarMaterial = null;
        }

        private void Update()
        {
            if (_heightBarObject == null || _widthBarObject == null)
            {
                return;
            }

            if (_heightBarHotkey.Value.IsDown()) _showHeightBar = !_showHeightBar;
            if (_widthBarHotkey.Value.IsDown()) _showWidthBar = !_showWidthBar;
            var visible = MakerAPI.IsInterfaceVisible() && !ForceHideBars;
            _heightBarObject.SetActiveIfDifferent(visible && _showHeightBar);
            _widthBarObject.SetActiveIfDifferent(visible && _showWidthBar);

            _zeroHeightBarObject.SetActiveIfDifferent(visible && _showZeroHeightBar.Value);
            _zeroWidthBarObject.SetActiveIfDifferent(visible && _showZeroWidthBar.Value && _showWidthBar);


            if (_heightBarObject.activeSelf || _widthBarObject.activeSelf || _differentialPoint != Vector3.zero)
            {
                if (_differentialHotkey.Value.IsDown())
                {
                    _differentialPoint = _differentialPoint == Vector3.zero ? _targetObject.position : Vector3.zero;

                    _zeroHeightBarObject.transform.position = new Vector3(0, _differentialPoint.y, 0);
                    _zeroWidthBarObject.transform.position = new Vector3(_differentialPoint.x, 0, 0);
                }
            }
        }

        private void OnGUI()
        {
            var textColor = _differentialPoint != Vector3.zero ? Color.yellow : Color.white;

            if (_heightBarObject != null && _heightBarObject.activeSelf)
                UpdateHeightBar(textColor);

            if (_widthBarObject != null && _widthBarObject.activeSelf)
                UpdateWidthBar(textColor);
        }

        private void UpdateHeightBar(Color textColor)
        {
            var barPosition = _heightBarObject.transform.position;
            barPosition = new Vector3(barPosition.x, _targetObject.position.y, barPosition.z);
            _heightBarObject.transform.position = barPosition;

            var vector = _mainCamera.WorldToScreenPoint(barPosition + new Vector3(0.1f, 0f));
#if KK || EC || KKS
            _labelRect.x = vector.x;
            _labelRect.y = Screen.height - vector.y;
#else
            // Clamp the last digit to deal with BetterAA shaking the screen
            _labelRect.x = (int)(vector.x / 10) * 10;
            _labelRect.y = (int)((Screen.height - vector.y) / 10) * 10;
#endif

            var cmHeight = (_heightBarObject.transform.localPosition.y - _differentialPoint.y) * Ratio;
            var value = _displayUnit.Value == DisplayUnits.Freedom
                ? CentimetresToFeet(cmHeight)
                : _displayUnit.Value == DisplayUnits.Metric ? cmHeight.ToString("F1") + "cm" : $"{cmHeight:F1}cm\n{CentimetresToFeet(cmHeight)}";

            ShadowAndOutline.DrawOutline(_labelRect, value, _labelStyle, textColor, Color.black, 1);
        }

        private void UpdateWidthBar(Color textColor)
        {
            var barPosition = _widthBarObject.transform.position;
            barPosition = new Vector3(_targetObject.position.x, _targetObject.position.y, barPosition.z);
            _widthBarObject.transform.position = barPosition;

            if (_zeroWidthBarObject.activeSelf)
            {
                _zeroWidthBarObject.transform.position =
                    new Vector3(_differentialPoint.x, barPosition.y, barPosition.z);
            }

            var vector = _mainCamera.WorldToScreenPoint(barPosition + new Vector3(0.0f, -0.1f));
#if KK || EC || KKS
            _labelWidthRect.x = vector.x;
            _labelWidthRect.y = Screen.height - vector.y;
#else
            // Clamp the last digit to deal with BetterAA shaking the screen
            _labelWidthRect.x = (int)(vector.x / 10) * 10;
            _labelWidthRect.y = (int)((Screen.height - vector.y) / 10) * 10;
#endif

            var cmHeight = -((_widthBarObject.transform.localPosition.x - _differentialPoint.x) * Ratio);
            var value = _displayUnit.Value == DisplayUnits.Freedom
                ? $"{CentimetresToInches(cmHeight):F2}\""
                : _displayUnit.Value == DisplayUnits.Metric ? cmHeight.ToString("F1") + "cm" : $"{cmHeight:F1}cm\n{CentimetresToInches(cmHeight)}";

            ShadowAndOutline.DrawOutline(_labelWidthRect, value, _labelStyle, textColor, Color.black, 1);
        }
    }
}