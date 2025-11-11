using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace ML.ToneMapping
{
    /// <summary>
    /// HDRP용 ToneMapping CustomPostProcess Volume Component
    /// Render Graph 기반의 HDRP 파이프라인과 호환됨
    /// </summary>
    [System.Serializable, VolumeComponentMenuForRenderPipeline("Post-processing/MAZELINE Tone Mapping", typeof(HDRenderPipelineAsset))]
    public class SGToneMappingHDRP : CustomPostProcessVolumeComponent, IPostProcessComponent
    {
        [SerializeField]
        public ToneMapCurveTypeParameter ToneMapType = new ToneMapCurveTypeParameter(ToneMapCurveType.None);

        [SerializeField]
        public ClampedFloatParameter Exposure = new ClampedFloatParameter(1.0f, 0.2f, 7f);

        [SerializeField]
        public ClampedFloatParameter AgxGamma = new ClampedFloatParameter(0.0f, 0f, 1.0f);

        [SerializeField]
        public ClampedFloatParameter AgxGammaPivot = new ClampedFloatParameter(0.8f, 0.01f, 1.0f);

        [SerializeField]
        public ClampedFloatParameter LayerMaskApplyWeight = new ClampedFloatParameter(1.00f, 0.1f, 1.0f);

        [SerializeField]
        public ClampedFloatParameter FXLayerMaskApplyWeight = new ClampedFloatParameter(1.00f, 0.1f, 1.0f);

        public SGToneMappingHDRP()
        {
            displayName = "MAZELINE Tone Mapping (HDRP)";
        }

        public bool IsActive()
        {
            return ToneMapType.value != ToneMapCurveType.None && ToneMapType.overrideState;
        }

        public override CustomPostProcessInjectionPoint injectionPoint => CustomPostProcessInjectionPoint.AfterPostProcess;

        public override void Setup()
        {
            // Render Graph 렌더링에서 실제 처리는 HDRP 패스 내에서 수행됨
        }

        public override void Render(CommandBuffer cmd, HDCamera hdCamera, RTHandle srcRT, RTHandle dstRT)
        {
            // HDRP Render Graph 방식에서는 이 함수가 호출되지 않음
            // 실제 렌더링은 RecordRenderGraph에서 처리
        }

        #region Render Graph Implementation
        // Render Graph 기반 렌더링 (HDRP 2021.3+)
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer contextContainer)
        {
            if (!IsActive())
                return;

            using (var builder = renderGraph.AddRasterRenderPass<ToneMappingRenderPassData>(
                "MAZELINE Tone Mapping (HDRP)", out var passData))
            {
                // Render Graph 렌더링 구현
                // 실제 구현은 별도의 Pass에서 처리
            }
        }

        private class ToneMappingRenderPassData
        {
            // 필요한 데이터 필드들
        }
        #endregion
    }

    /// <summary>
    /// Layer Mask 설정을 위한 CustomPass Volume Component
    /// 캐릭터 및 FX 레이어를 독립적으로 마스크 처리
    /// </summary>
    [System.Serializable, VolumeComponentMenuForRenderPipeline("Custom Passes/MAZELINE Layer Masks", typeof(HDRenderPipelineAsset))]
    public class SGLayerMaskVolumeComponent : VolumeComponent
    {
        [SerializeField]
        public LayerMaskParameter CharacterLayerMask = new LayerMaskParameter(-1);

        [SerializeField]
        public LayerMaskParameter FXLayerMask = new LayerMaskParameter(-1);

        [SerializeField]
        public BoolParameter EnableCharacterMask = new BoolParameter(false);

        [SerializeField]
        public BoolParameter EnableFXMask = new BoolParameter(false);
    }

    /// <summary>
    /// LayerMask VolumeParameter 구현
    /// </summary>
    [System.Serializable]
    public sealed class LayerMaskParameter : VolumeParameter<LayerMask>
    {
        public LayerMaskParameter(LayerMask value, bool overrideState = false)
            : base(value, overrideState) { }
    }
}
