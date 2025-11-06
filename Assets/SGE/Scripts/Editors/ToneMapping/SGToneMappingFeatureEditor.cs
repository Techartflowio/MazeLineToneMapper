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

        void OnEnable()
        {
            _injectionPoint = serializedObject.FindProperty("injectionPoint");
            _LayerMask = serializedObject.FindProperty("layerMask");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            EditorGUILayout.PropertyField(_injectionPoint);
            
            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(_LayerMask);
            EditorGUILayout.HelpBox("캐릭터를 분리하기 위한 레이어 마스크", MessageType.Info);
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}

