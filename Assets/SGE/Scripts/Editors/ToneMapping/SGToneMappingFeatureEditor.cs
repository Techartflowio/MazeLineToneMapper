using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using ML.ToneMapping;

namespace ML.Editor
{
    [CustomEditor(typeof(SGToneMappingFeature))]
    public class SgToneMappingFeatureEditor : UnityEditor.Editor
    {
        SerializedProperty _injectionPoint;
        SerializedProperty _LayerMask;
        SerializedProperty _FXLayerMask;

        void OnEnable()
        {
            _injectionPoint = serializedObject.FindProperty("injectionPoint");
            _LayerMask = serializedObject.FindProperty("layerMask");
            _FXLayerMask = serializedObject.FindProperty("fxLayerMask");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            EditorGUILayout.PropertyField(_injectionPoint);
            
            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(_LayerMask);
            EditorGUILayout.HelpBox("캐릭터를 분리하기 위한 레이어 마스크", MessageType.Info);
            
            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(_FXLayerMask);
            EditorGUILayout.HelpBox("FX 이펙트(파티클/이펙트)를 분리하기 위한 레이어 마스크. Additive 블렌딩을 사용하는 이펙트에 사용됩니다.", MessageType.Info);
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}

