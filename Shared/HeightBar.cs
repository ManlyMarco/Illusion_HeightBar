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
        internal const string Version = "3.4.1";

        private readonly GUIStyle _labelStyle = new GUIStyle();
        private Rect _labelRect = new Rect(400f, 400f, 100f, 100f);
        private Rect _labelWidthRect = new Rect(400f, 400f, 100f, 100f);

        private Camera _mainCamera;

        private GameObject _heightBarObject;
        private GameObject _widthBarObject;
        private GameObject _zeroBarObject;

        private Transform _targetObject;
        private Vector3 _differentialPoint = Vector3.zero;

        private Material _barMaterial;
        private Material _widthBarMaterial;
        private Material _zeroBarMaterial;
        private SidebarToggle _sidebarHeightToggle;
        private SidebarToggle _sidebarWidthToggle;

        private ConfigEntry<bool> _showZeroBar;
        private ConfigEntry<DisplayUnits> _displayUnit;
        private ConfigEntry<float> _barAlpha;
        private ConfigEntry<float> _zeroBarAlpha;

        private bool _showHeightBar;
        private bool _showWidthBar;
        private ConfigEntry<KeyboardShortcut> _barHotkey;
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
            _barHotkey = Config.Bind("General", "Toggle height measure bar", KeyboardShortcut.Empty, "Hotkey to toggle the height measurement bar in maker.");
            _differentialHotkey = Config.Bind("General", "Set Differential Measurement Point", KeyboardShortcut.Empty, "Hotkey to set a point for differential measurements.");
            _showZeroBar = Config.Bind("General", "Show floor bar at character`s feet", true, "Shows the position of the floor. Helps prevent floating characters when using yellow sliders.");

            _displayUnit = Config.Bind("General", "Units", DisplayUnits.Both, "Allows you the change the units in which height is displayed.");

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
            _showHeightBar = false;
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

            _zeroBarObject = Instantiate(_heightBarObject);
            _zeroBarObject.name = "Floor bar indicator";

            _barMaterial = _heightBarObject.GetComponent<Renderer>().material;
            _barMaterial.color = new Color(0, 0, 0, _barAlpha.Value);

            _widthBarMaterial = _widthBarObject.GetComponent<Renderer>().material;
            _widthBarMaterial.color = new Color(0, 0, 0, _barAlpha.Value);

            _zeroBarMaterial = _zeroBarObject.GetComponent<Renderer>().material;
            _zeroBarMaterial.color = new Color(0, 0, 0, _zeroBarAlpha.Value);

            _heightBarObject.SetActive(false);
            _widthBarObject.SetActive(false);
            _zeroBarObject.SetActive(_showZeroBar.Value);

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
            _barMaterial = null;

            Destroy(_zeroBarObject);
            _zeroBarObject = null;
            _zeroBarMaterial = null;

            Destroy(_widthBarObject);
            _widthBarObject = null;
            _widthBarMaterial = null;
        }

        private void Update()
        {
            if (_heightBarObject != null && _widthBarObject != null)
            {
                if (_barHotkey.Value.IsDown()) _showHeightBar = !_showHeightBar;
                var visible = MakerAPI.IsInterfaceVisible() && !ForceHideBars;
                _zeroBarObject.SetActiveIfDifferent(visible && _showZeroBar.Value);
                _heightBarObject.SetActiveIfDifferent(visible && _showHeightBar);
                _widthBarObject.SetActiveIfDifferent(visible && _showWidthBar);


                if (_heightBarObject.activeSelf || _widthBarObject.activeSelf)
                {
                    if (_differentialHotkey.Value.IsDown())
                    {
                        _differentialPoint = _differentialPoint == Vector3.zero ? _targetObject.position : Vector3.zero;
                    }
                }
            }
        }

        private void OnGUI()
        {
            if (_heightBarObject != null && _heightBarObject.activeSelf)
                UpdateHeightBar();

            if (_widthBarObject != null && _widthBarObject.activeSelf)
                UpdateWidthBar();
        }

        private void UpdateHeightBar()
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

            ShadowAndOutline.DrawOutline(_labelRect, value, _labelStyle, Color.white, Color.black, 1);
        }

        private void UpdateWidthBar()
        {
            var barPosition = _widthBarObject.transform.position;
            barPosition = new Vector3(_targetObject.position.x, _targetObject.position.y, barPosition.z);
            _widthBarObject.transform.position = barPosition;

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

            ShadowAndOutline.DrawOutline(_labelWidthRect, value, _labelStyle, Color.white, Color.black, 1);
        }
    }
}