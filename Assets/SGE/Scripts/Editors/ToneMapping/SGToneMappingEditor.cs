using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ML.Editor
{
    // Custom inspector for SGToneMappingVC focusing on curve-specific parameter visibility and migration
    [CustomEditor(typeof(SGToneMappingVC))]
    public class SGToneMappingEditor : VolumeComponentEditor
    {
        // Serialized parameters (VolumeParameter wrappers)
        SerializedDataParameter _toneMapType;
        SerializedDataParameter _exposure;
        SerializedDataParameter _agxGamma;
        SerializedDataParameter _agxGammaPivot;
        SerializedDataParameter _layerMaskApplyWeight;
        SerializedDataParameter _fxLayerMaskApplyWeight;

        int _lastType = -1;

        // ReSharper disable Unity.PerformanceAnalysis
        public override void OnEnable()
        {
            base.OnEnable();

            var o = new PropertyFetcher<SGToneMappingVC>(serializedObject);
            _toneMapType = Unpack(o.Find(x => x.ToneMapType));
            _exposure = Unpack(o.Find(x => x.Exposure));
            _agxGamma = Unpack(o.Find(x => x.AgxGamma));
            _agxGammaPivot = Unpack(o.Find(x => x.AgxGammaPivot));
            _layerMaskApplyWeight = Unpack(o.Find(x => x.LayerMaskApplyWeight));
            _fxLayerMaskApplyWeight = Unpack(o.Find(x => x.FXLayerMaskApplyWeight));

            // Initialize the last selected type
            if (_toneMapType.value != null)
                _lastType = _toneMapType.value.intValue;
        }

        public override void OnInspectorGUI()
        {
            // Header/help
            //EditorGUILayout.LabelField("SGE Project A Tonemapping", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("선택된 톤매핑 커브에 필요한 파라미터만 표시됩니다. 커브 변경 시 저장된(메타데이터) 값을 그대로 유지합니다.", MessageType.Info);

            // Draw: Curve selector first
            EditorGUI.BeginChangeCheck();
            // ReSharper disable once Unity.PerformanceCriticalCodeInvocation
            PropertyField(_toneMapType, new GUIContent("Curve Type"));
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                // null 병합식 사용
                var currentType = _toneMapType.value?.intValue ?? (int)ToneMapCurveType.None;
                // 기본값 리셋 없이, 직렬화(메타데이터)에 저장된 값을 그대로 유지
                _lastType = currentType;
            }
            // null 병합식 사용
            var activeType = (ToneMapCurveType)(_toneMapType.value?.intValue ?? (int)ToneMapCurveType.None);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("공통 설정", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                if (activeType != ToneMapCurveType.AGX)
                {
                    // ReSharper disable once Unity.PerformanceCriticalCodeInvocation
                    PropertyField(_exposure, new GUIContent("Exposure", "씬 노출(Stops) 보정. 1.0은 기준 노출."));
                }
            }

            // Per-curve parameters
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"{activeType} 파라미터", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                switch (activeType)
                {
                    case ToneMapCurveType.AGX:
                        // ReSharper disable once Unity.PerformanceCriticalCodeInvocation
                        PropertyField(_agxGamma, new GUIContent("AGX Gamma", "LGG의 Gamma처럼 미드톤을 형상화하는 아티스틱 감마. 1.0=중립."));
                        // ReSharper disable once Unity.PerformanceCriticalCodeInvocation
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

            // LayerMask 파라미터 섹션
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("레이어 마스크 파라미터", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("캐릭터 레이어 마스크", EditorStyles.miniLabel);
                // ReSharper disable once Unity.PerformanceCriticalCodeInvocation
                PropertyField(_layerMaskApplyWeight, new GUIContent("캐릭터 가중치", "캐릭터 레이어 마스크 적용 가중치 (0=미적용, 1=완전 적용)"));
                
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("FX 레이어 마스크", EditorStyles.miniLabel);
                // ReSharper disable once Unity.PerformanceCriticalCodeInvocation
                PropertyField(_fxLayerMaskApplyWeight, new GUIContent("FX 가중치", "FX 레이어 마스크 적용 가중치 (0=미적용, 1=완전 적용)"));
                
                EditorGUILayout.HelpBox("레이어 마스크 적용 가중치: 0=톤매핑 미적용, 1=톤매핑 완전 적용. 각 레이어별로 독립적으로 제어 가능합니다.", MessageType.Info);
            }
        }
    }
}
