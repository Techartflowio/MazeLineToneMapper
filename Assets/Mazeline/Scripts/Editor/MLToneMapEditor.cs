using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ML.Editor
{
    // Custom inspector for MLToneMap focusing on curve-specific parameter visibility and migration
    [CustomEditor(typeof(MLToneMap))]
    public class MLToneMapEditor : VolumeComponentEditor
    {
        // Serialized parameters (VolumeParameter wrappers)
        SerializedDataParameter _toneMapType;
        SerializedDataParameter _exposure;
        SerializedDataParameter _ignoreCharacterPixels;
        SerializedDataParameter _characterPixelsToneMapStrength;
        SerializedDataParameter _agxGamma;
        SerializedDataParameter _agxGammaPivot;

        int _lastType = -1;

        // Mazeline logo & link
        Texture2D _logo;
        const string kLogoPath = "Assets/Mazeline/Scripts/Editor/images/ml_logo.png";
        const string kWebsiteUrl = "http://mazeline.tech/";

        public override void OnEnable()
        {
            base.OnEnable();

            var o = new PropertyFetcher<MLToneMap>(serializedObject);
            _toneMapType = Unpack(o.Find(x => x.ToneMapType));
            _exposure = Unpack(o.Find(x => x.Exposure));
            _ignoreCharacterPixels = Unpack(o.Find(x => x.IgnoreCharacterPixels));
            _characterPixelsToneMapStrength = Unpack(o.Find(x => x.CharacterPixelsToneMapStrength));
            _agxGamma = Unpack(o.Find(x => x.AgxGamma));
            _agxGammaPivot = Unpack(o.Find(x => x.AgxGammaPivot));

            // Initialize last selected type
            if (_toneMapType.value != null)
                _lastType = _toneMapType.value.intValue;

            // Load logo asset
            _logo = AssetDatabase.LoadAssetAtPath<Texture2D>(kLogoPath);
        }

        public override void OnInspectorGUI()
        {
            // Title logo (clickable)
            if (_logo != null)
            {
                GUILayout.Space(6);
                // Reserve a full-width rect with the image's native height
                float imgW = _logo.width;
                float imgH = _logo.height;
                Rect full = GUILayoutUtility.GetRect(0, imgH, GUILayout.ExpandWidth(true));
                // Center the image rect horizontally using its native pixel width
                float x = full.x + Mathf.Max(0, (full.width - imgW) * 0.5f);
                var imgRect = new Rect(x, full.y, imgW, imgH);
                // Black background fill behind the image (full width so it shows when inspector is wider)
                EditorGUI.DrawRect(full, new Color(0.08f, 0.08f, 0.08f));
                // Draw the texture at 1:1 pixel size
                GUI.DrawTexture(imgRect, _logo, ScaleMode.StretchToFill, true);
                // Clickable link area
                EditorGUIUtility.AddCursorRect(imgRect, MouseCursor.Link);
                if (GUI.Button(imgRect, GUIContent.none, GUIStyle.none))
                {
                    Application.OpenURL(kWebsiteUrl);
                }
                GUILayout.Space(4);
            }

            // Header/help
            EditorGUILayout.LabelField("MazeLine Tone Mapping", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("선택된 톤매핑 커브에 필요한 파라미터만 표시됩니다. 커브 변경 시 저장된(메타데이터) 값을 그대로 유지합니다.", MessageType.Info);

            // Draw: Curve selector first
            EditorGUI.BeginChangeCheck();
            PropertyField(_toneMapType, new GUIContent("Curve Type"));
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                var currentType = _toneMapType.value != null ? _toneMapType.value.intValue : (int)ToneMapCurveType.None;
                // 기본값 리셋 없이, 직렬화(메타데이터)에 저장된 값을 그대로 유지
                _lastType = currentType;
            }

            var activeType = (ToneMapCurveType)(_toneMapType.value != null ? _toneMapType.value.intValue : (int)ToneMapCurveType.None);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("공통 설정", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                if (activeType != ToneMapCurveType.AGX)
                {
                    PropertyField(_exposure, new GUIContent("Exposure", "씬 노출(Stops) 보정. 1.0은 기준 노출."));
                }
                PropertyField(_ignoreCharacterPixels, new GUIContent("Ignore Character Pixels", "캐릭터 픽셀을 톤매핑 처리에서 제외합니다."));
                PropertyField(_characterPixelsToneMapStrength, new GUIContent("Character Pixels ToneMap Strength", "캐릭터 픽셀에 한해 톤매핑 강도(0=미적용, 1=완전 적용)."));
            }

            // Per-curve parameters
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"{activeType} 파라미터", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                switch (activeType)
                {
                    case ToneMapCurveType.AGX:
                        PropertyField(_agxGamma, new GUIContent("AGX Gamma", "LGG의 Gamma처럼 미드톤을 형상화하는 아티스틱 감마. 1.0=중립."));
                        PropertyField(_agxGammaPivot, new GUIContent("AGX Gamma Pivot", "감마 피벗(기준) 값. 0.5=중간 회색 기준. (고급)"));
                        break;
                    case ToneMapCurveType.Filmic:
                    case ToneMapCurveType.KhronosNeutral:
                    case ToneMapCurveType.GranTurismo:
                    case ToneMapCurveType.None:
                    default:
                        EditorGUILayout.HelpBox("이 커브는 추가로 노출할 사용자 파라미터가 없습니다.", MessageType.None);
                        break;
                }
            }
        }
    }
}
