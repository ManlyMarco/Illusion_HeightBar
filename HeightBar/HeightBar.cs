using System;
using System.ComponentModel;
using BepInEx;
using KKAPI;
using KKAPI.Maker;
using KKAPI.Maker.UI.Sidebar;
using KKAPI.Studio;
using UniRx;
using UnityEngine;

namespace HeightBar
{
    [BepInPlugin("HeightBar", "HeightBarX", Version)]
    [BepInDependency(KoikatuAPI.GUID)]
    [BepInDependency(ConfigurationManager.ConfigurationManager.GUID)]
    public class HeightBar : BaseUnityPlugin
    {
        internal const string Version = "2.0.3";
        private const float Ratio = 103.092781f;

        private readonly GUIStyle _labelStyle = new GUIStyle();
        private Rect _labelRect = new Rect(400f, 400f, 100f, 100f);

        private Camera mainCamera;
        
        private GameObject _barObject;
        private GameObject _zeroBarObject;
        
        private Transform _targetObject;

        private Material barMaterial;
        private Material zeroBarMaterial;

        [DisplayName("Show height bar at zero position")]
        private ConfigWrapper<bool> showZeroBar { get; set; }
        
        [DisplayName("Opacity of the height measuring bar")]
        [AcceptableValueRange(0f, 1f, false)]
        private ConfigWrapper<float> barAlpha { get; set; }
        
        [DisplayName("Opacity of the zero height position bar")]
        [AcceptableValueRange(0f, 1f, false)]
        private ConfigWrapper<float> zeroBarAlpha { get; set; }
        
        private void DestroyBars()
        {
            if (_barObject != null)
            {
                Destroy(_barObject);
                _barObject = null;
            }

            if (_zeroBarObject != null)
            {
                Destroy(_zeroBarObject);
                _zeroBarObject = null;
            }
        }

        private void Awake()
        {
            if (!KoikatuAPI.CheckRequiredPlugin(this, KoikatuAPI.GUID, new Version("1.4")))
                return;

            if(StudioAPI.InsideStudio)
            {
                enabled = false;
                return;
            }

            showZeroBar = new ConfigWrapper<bool>("show-zero-position", this, true);
            barAlpha = new ConfigWrapper<float>("bar-alpha", 0.5f);
            zeroBarAlpha = new ConfigWrapper<float>("zero-bar-alpha", 0.25f);
            
            showZeroBar.SettingChanged += delegate
            {
                if (_zeroBarObject != null) 
                    _zeroBarObject.SetActive(showZeroBar.Value);
            };
            
            barAlpha.SettingChanged += delegate
            {
                if (barMaterial != null)
                    barMaterial.color = new Color(1, 1, 1, barAlpha.Value);
            };
            
            zeroBarAlpha.SettingChanged += delegate
            {
                if (zeroBarMaterial != null)
                    zeroBarMaterial.color = new Color(1, 1, 1, zeroBarAlpha.Value);
            };

            _labelStyle.fontSize = 20;
            _labelStyle.normal.textColor = Color.white;

            MakerAPI.RegisterCustomSubCategories += MakerAPI_Enter;
            MakerAPI.MakerExiting += MakerAPI_Exit;
        }

        private void MakerAPI_Exit(object sender, EventArgs e)
        {
            DestroyBars();
        }

        private void MakerAPI_Enter(object sender, RegisterSubCategoriesEvent e)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
                return;
            
            var cam = mainCamera.GetComponent<CameraControl_Ver2>();
            if (cam != null && cam.targetObj != null)
                _targetObject = cam.targetObj;
            else
                _targetObject = mainCamera.transform;
            
            _barObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _barObject.transform.SetParent(MakerAPI.GetCharacterControl().objRoot.transform, false);
            _barObject.transform.localScale = new Vector3(0.3f, 0.005f, 0.3f);
            _barObject.transform.localPosition = new Vector3(0f, 0f, 0f);
            _barObject.layer = 12;
            _barObject.name = "HeightBar indicator";

            _zeroBarObject = Instantiate(_barObject);
            _zeroBarObject.name = "zero HeightBar indicator";

            barMaterial = new Material(Shader.Find("Standard"));
            if (barMaterial != null)
            {
                barMaterial.SetOverrideTag("RenderType", "Transparent");
                barMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                barMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                barMaterial.SetInt("_ZWrite", 0);
                barMaterial.DisableKeyword("_ALPHATEST_ON");
                barMaterial.EnableKeyword("_ALPHABLEND_ON");
                barMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                barMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                
                zeroBarMaterial = Instantiate(barMaterial);
                
                barMaterial.color = new Color(1, 1, 1, barAlpha.Value);
                _barObject.GetComponent<Renderer>().material = barMaterial;
                
                zeroBarMaterial.color = new Color(1, 1, 1, zeroBarAlpha.Value);
                _zeroBarObject.GetComponent<Renderer>().material = zeroBarMaterial;
            }
            
            _barObject.SetActive(false);
            _zeroBarObject.SetActive(showZeroBar.Value);
            
            e.AddSidebarControl(new SidebarToggle("Show height measure bar", false, this)).ValueChanged.Subscribe(b => _barObject.SetActive(b));
        }

        private void OnGUI()
        {
            if (_barObject == null || !_barObject.activeSelf || !MakerAPI.IsInterfaceVisible())
                return;

            var barPosition = _barObject.transform.position;
            barPosition = new Vector3(barPosition.x, _targetObject.position.y, barPosition.x);
            _barObject.transform.position = barPosition;

            var vector = mainCamera.WorldToScreenPoint(barPosition + new Vector3(0.1f, 0f));
            _labelRect.x = vector.x;
            _labelRect.y = Screen.height - vector.y;

            GUI.Label(_labelRect, (_barObject.transform.localPosition.y * Ratio).ToString("F1") + "cm", _labelStyle);
        }
    }
}
