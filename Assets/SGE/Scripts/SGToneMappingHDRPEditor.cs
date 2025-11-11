#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.HighDefinition;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;

namespace ML.ToneMapping.HDRP.Editor
{
    [CustomEditor(typeof(SGToneMappingHDRP))]
    sealed class SGToneMappingHDRPEditor : VolumeComponentEditor
    {
        SerializedDataParameter m_ToneMapType;
        SerializedDataParameter m_Exposure;
        SerializedDataParameter m_AgxGamma;
        SerializedDataParameter m_AgxGammaPivot;
        SerializedDataParameter m_LayerMaskApplyWeight;
        SerializedDataParameter m_FXLayerMaskApplyWeight;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<SGToneMappingHDRP>(serializedObject);
            
            m_ToneMapType = Unpack(o.Find(x => x.toneMapType));
            m_Exposure = Unpack(o.Find(x => x.exposure));
            m_AgxGamma = Unpack(o.Find(x => x.agxGamma));
            m_AgxGammaPivot = Unpack(o.Find(x => x.agxGammaPivot));
            m_LayerMaskApplyWeight = Unpack(o.Find(x => x.layerMaskApplyWeight));
            m_FXLayerMaskApplyWeight = Unpack(o.Find(x => x.fxLayerMaskApplyWeight));
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("SGE Tone Mapping (HDRP)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "선택된 톤매핑 커브에 필요한 파라미터만 표시됩니다. CustomPass Volume을 추가하여 레이어 마스크를 설정하세요.", 
                MessageType.Info);

            EditorGUILayout.Space();
            
            // Tone Map Type
            PropertyField(m_ToneMapType, new GUIContent("Tone Map Type", "톤매핑 커브 타입"));
            
            // Get current type
            var currentType = (ToneMapCurveType)(m_ToneMapType.value.intValue);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Common Parameters", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.VerticalScope("box"))
            {
                // Exposure (not used for AGX)
                if (currentType != ToneMapCurveType.AGX)
                {
                    PropertyField(m_Exposure, new GUIContent("Exposure", "노출 조정 (Stops)"));
                }
            }
            
            // Curve-specific parameters
            if (currentType != ToneMapCurveType.None)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"{currentType} Parameters", EditorStyles.boldLabel);
                
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    switch (currentType)
                    {
                        case ToneMapCurveType.AGX:
                            PropertyField(m_AgxGamma, new GUIContent("AGX Gamma", "미드톤 형상화 감마"));
                            PropertyField(m_AgxGammaPivot, new GUIContent("AGX Gamma Pivot", "감마 피벗 값"));
                            break;
                        
                        case ToneMapCurveType.Filmic:
                        case ToneMapCurveType.KhronosNeutral:
                        case ToneMapCurveType.GranTurismo:
                        default:
                            EditorGUILayout.HelpBox("이 커브는 추가 파라미터가 없습니다.", MessageType.None);
                            break;
                    }
                }
            }
            
            // Layer Mask Weights
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Layer Mask Weights", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.VerticalScope("box"))
            {
                PropertyField(m_LayerMaskApplyWeight, 
                    new GUIContent("Character Weight", "캐릭터 레이어 톤매핑 가중치 (0=미적용, 1=완전적용)"));
                PropertyField(m_FXLayerMaskApplyWeight, 
                    new GUIContent("FX Weight", "FX 레이어 톤매핑 가중치 (0=미적용, 1=완전적용)"));
                
                EditorGUILayout.Space(5);
                
                if (GUILayout.Button("Setup CustomPass Volume"))
                {
                    SetupCustomPassVolume();
                }
                
                EditorGUILayout.HelpBox(
                    "CustomPass Volume이 필요합니다. 위 버튼으로 자동 설정하거나 수동으로 추가하세요.", 
                    MessageType.Info);
            }
        }
        
        void SetupCustomPassVolume()
        {
            // Find or create CustomPass Volume
            var customPassVolume = GameObject.FindObjectOfType<CustomPassVolume>();
            
            if (customPassVolume == null)
            {
                var go = new GameObject("SGE Tone Mapping CustomPass");
                customPassVolume = go.AddComponent<CustomPassVolume>();
            }
            
            // Check if our pass already exists
            bool hasLayerMaskPass = false;
            foreach (var pass in customPassVolume.customPasses)
            {
                if (pass is SGLayerMaskPass)
                {
                    hasLayerMaskPass = true;
                    break;
                }
            }
            
            // Add our pass if not present
            if (!hasLayerMaskPass)
            {
                var layerMaskPass = customPassVolume.AddPassOfType<SGLayerMaskPass>();
                layerMaskPass.name = "SGE Layer Mask Pass";
                layerMaskPass.enabled = true;
                
                // Set default injection point
                customPassVolume.injectionPoint = CustomPassInjectionPoint.BeforeTransparent;
                
                Debug.Log("SGE Layer Mask Pass added to CustomPass Volume");
            }
            
            Selection.activeGameObject = customPassVolume.gameObject;
        }
    }
    
    // Custom Pass Editor
    [CustomPassDrawer(typeof(SGLayerMaskPass))]
    class SGLayerMaskPassDrawer : CustomPassDrawer
    {
        protected override PassUIFlag commonPassUIFlags => PassUIFlag.Name;
        
        protected override void Initialize(SerializedProperty customPass)
        {
            // Nothing to initialize
        }
        
        protected override void DoPassGUI(SerializedProperty customPassProperty, Rect rect)
        {
            var characterMaskProperty = customPassProperty.FindPropertyRelative("characterLayerMask");
            var fxMaskProperty = customPassProperty.FindPropertyRelative("fxLayerMask");
            
            EditorGUI.BeginProperty(rect, GUIContent.none, customPassProperty);
            
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            
            // Character Layer Mask
            Rect characterRect = new Rect(rect.x, rect.y, rect.width, lineHeight);
            EditorGUI.PropertyField(characterRect, characterMaskProperty, 
                new GUIContent("Character Layer Mask", "캐릭터 레이어 마스크"));
            
            // FX Layer Mask
            Rect fxRect = new Rect(rect.x, rect.y + lineHeight + spacing, rect.width, lineHeight);
            EditorGUI.PropertyField(fxRect, fxMaskProperty, 
                new GUIContent("FX Layer Mask", "FX 이펙트 레이어 마스크"));
            
            EditorGUI.EndProperty();
        }
        
        protected override float GetPassHeight(SerializedProperty customPassProperty)
        {
            return EditorGUIUtility.singleLineHeight * 2 + EditorGUIUtility.standardVerticalSpacing;
        }
    }
}
#endif
