using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEditor;
using UnityEditor.Rendering;

namespace ML.ToneMapping
{
#if UNITY_EDITOR
    /// <summary>
    /// HDRP Tone Mapping Volume Component 에디터
    /// </summary>
    [CustomEditor(typeof(SGToneMappingHDRP))]
    public class SGToneMappingHDRPEditor : VolumeComponentEditor
    {
        private SerializedDataParameter m_ToneMapType;
        private SerializedDataParameter m_Exposure;
        private SerializedDataParameter m_AgxGamma;
        private SerializedDataParameter m_AgxGammaPivot;
        private SerializedDataParameter m_LayerMaskApplyWeight;
        private SerializedDataParameter m_FXLayerMaskApplyWeight;

        public override void OnEnable()
        {
            base.OnEnable();

            var o = new PropertyFetcher<SGToneMappingHDRP>(serializedObject);
            m_ToneMapType = Fetch(o.Find(x => x.ToneMapType));
            m_Exposure = Fetch(o.Find(x => x.Exposure));
            m_AgxGamma = Fetch(o.Find(x => x.AgxGamma));
            m_AgxGammaPivot = Fetch(o.Find(x => x.AgxGammaPivot));
            m_LayerMaskApplyWeight = Fetch(o.Find(x => x.LayerMaskApplyWeight));
            m_FXLayerMaskApplyWeight = Fetch(o.Find(x => x.FXLayerMaskApplyWeight));
        }

        public override void OnInspectorGUI()
        {
            PropertyField(m_ToneMapType, new GUIContent("Tone Mapping Type", "Select tone mapping algorithm"));

            PropertyField(m_Exposure, new GUIContent("Exposure", "Exposure compensation factor"));

            var toneMappingType = (ToneMapCurveType)m_ToneMapType.value.enumValueIndex;

            if (toneMappingType == ToneMapCurveType.AGX)
            {
                PropertyField(m_AgxGamma, new GUIContent("AGX Gamma", "AGX gamma correction factor"));
                PropertyField(m_AgxGammaPivot, new GUIContent("AGX Gamma Pivot", "AGX gamma pivot point"));
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Layer Mask Settings", EditorStyles.boldLabel);

            PropertyField(m_LayerMaskApplyWeight, new GUIContent("Character Mask Weight", "Blend weight for character layer mask (0-1)"));
            PropertyField(m_FXLayerMaskApplyWeight, new GUIContent("FX Mask Weight", "Blend weight for FX layer mask (0-1)"));
        }
    }

    /// <summary>
    /// HDRP Layer Mask Volume Component 에디터
    /// </summary>
    [CustomEditor(typeof(SGLayerMaskVolumeComponent))]
    public class SGLayerMaskVolumeComponentEditor : VolumeComponentEditor
    {
        private SerializedDataParameter m_CharacterLayerMask;
        private SerializedDataParameter m_FXLayerMask;
        private SerializedDataParameter m_EnableCharacterMask;
        private SerializedDataParameter m_EnableFXMask;

        public override void OnEnable()
        {
            base.OnEnable();

            var o = new PropertyFetcher<SGLayerMaskVolumeComponent>(serializedObject);
            m_CharacterLayerMask = Fetch(o.Find(x => x.CharacterLayerMask));
            m_FXLayerMask = Fetch(o.Find(x => x.FXLayerMask));
            m_EnableCharacterMask = Fetch(o.Find(x => x.EnableCharacterMask));
            m_EnableFXMask = Fetch(o.Find(x => x.EnableFXMask));
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("Character Layer", EditorStyles.boldLabel);
            PropertyField(m_EnableCharacterMask, new GUIContent("Enable Character Mask"));
            PropertyField(m_CharacterLayerMask, new GUIContent("Character Layers"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("FX Layer", EditorStyles.boldLabel);
            PropertyField(m_EnableFXMask, new GUIContent("Enable FX Mask"));
            PropertyField(m_FXLayerMask, new GUIContent("FX Layers"));

            EditorGUILayout.HelpBox("Enable the layer mask you want to render. These layers will be rendered to separate texture and used for tone mapping blending.", MessageType.Info);
        }
    }
#endif
}
