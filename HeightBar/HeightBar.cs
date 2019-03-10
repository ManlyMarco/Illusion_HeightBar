using System;
using BepInEx;
using KKAPI;
using KKAPI.Maker;
using KKAPI.Maker.UI.Sidebar;
using UniRx;
using UnityEngine;
using Scene = Manager.Scene;

namespace HeightBar
{
    [BepInPlugin("HeightBar", "HeightBarX", Version)]
    public class HeightBar : BaseUnityPlugin
    {
        internal const string Version = "2.0";
        private const float Ratio = 103.092781f;

        private readonly GUIStyle _labelStyle = new GUIStyle();
        private Rect _labelRect = new Rect(400f, 400f, 100f, 100f);

        private bool BarEnabled
        {
            get => _barEnabled && _barObject != null && string.IsNullOrEmpty(Scene.Instance.AddSceneName);
            set
            {
                _barEnabled = value;

                if (_barObject != null)
                {
                    Destroy(_barObject);
                    _barObject = null;
                }
                
                if (!_barEnabled) return;

                _barObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _barObject.transform.SetParent(MakerAPI.GetCharacterControl().objRoot.transform, false);
                _barObject.transform.localScale = new Vector3(0.3f, 0.005f, 0.3f);
                _barObject.transform.localPosition = new Vector3(0f, 0f, 0f);
                _barObject.layer = 12;
                _barObject.name = "HeightBar indicator";

                _targetObject = Camera.main.GetComponent<CameraControl_Ver2>()?.targetObj ?? Camera.main.transform;
            }
        }

        private GameObject _barObject;
        private bool _barEnabled;
        
        private Transform _targetObject;

        private void Awake()
        {
            if(!KoikatuAPI.CheckRequiredPlugin(this, KoikatuAPI.GUID, new Version(KoikatuAPI.VersionConst)))
                return;

            _labelStyle.fontSize = 20;
            _labelStyle.normal.textColor = Color.white;

            MakerAPI.RegisterCustomSubCategories += MakerAPI_Enter;
            MakerAPI.MakerExiting += MakerAPI_Exit;
        }

        private void MakerAPI_Exit(object sender, EventArgs e)
        {
            BarEnabled = false;
        }

        private void MakerAPI_Enter(object sender, RegisterSubCategoriesEvent e)
        {
            e.AddSidebarControl(new SidebarToggle("Show height measure bar", false, this)).ValueChanged.Subscribe(b => BarEnabled = b);
        }

        private void OnGUI()
        {
            if (!BarEnabled)
                return;

            var transformPosition = _barObject.transform.position;
            transformPosition.y = _targetObject.position.y;
            _barObject.transform.position = transformPosition;

            var vector = Camera.main.WorldToScreenPoint(_barObject.transform.position + new Vector3(0.1f, 0f));
            _labelRect.x = vector.x;
            _labelRect.y = Screen.height - vector.y;

            GUI.Label(_labelRect, (_barObject.transform.localPosition.y * Ratio).ToString("F1") + "cm", _labelStyle);
        }
    }
}
