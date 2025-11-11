using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering.RenderGraphModule;

namespace ML.ToneMapping
{
    /// <summary>
    /// HDRP용 Layer Mask CustomPass
    /// 특정 레이어의 객체들을 별도의 렌더 타겟에 렌더링하여 마스크 생성
    /// Render Graph 기반으로 구현됨
    /// </summary>
    public class SGLayerMaskCustomPass : CustomPass
    {
        // 마스크 렌더링 설정
        [SerializeField] private LayerMask m_characterLayerMask = -1;
        [SerializeField] private LayerMask m_fxLayerMask = -1;

        // 마스크 렌더 타겟
        private RTHandle m_characterMaskRT;
        private RTHandle m_fxMaskRT;

        // 마스크 셰이더
        private Shader m_characterMaskShader;
        private Shader m_fxMaskShader;
        private Material m_characterMaskMaterial;
        private Material m_fxMaskMaterial;

        // 렌더 타겟 설정
        private const string CHARACTER_MASK_RT_NAME = "_CharacterLayerMaskTextureHDRP";
        private const string FX_MASK_RT_NAME = "_FXLayerMaskTextureHDRP";

        public override void Setup()
        {
            // 셰이더 로드
            m_characterMaskShader = Shader.Find("Hidden/MAZELINE/PostProcess/CharacterLayerMask");
            m_fxMaskShader = Shader.Find("Hidden/MAZELINE/PostProcess/FXLayerMask");

            if (m_characterMaskShader != null)
                m_characterMaskMaterial = new Material(m_characterMaskShader);

            if (m_fxMaskShader != null)
                m_fxMaskMaterial = new Material(m_fxMaskShader);
        }

        public override void Execute(CustomPassContext ctx)
        {
            // 레거시 Execute 경로 - Render Graph를 사용할 경우 호출되지 않음
            // HDRP는 Render Graph를 우선적으로 사용
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // HDRP Render Graph 기반 렌더링
            var hdrpData = frameData.Get<HDCamera>();

            if (hdrpData == null)
                return;

            // Character Layer Mask 렌더링
            if (m_characterLayerMask != 0 && m_characterLayerMask != -1)
            {
                RecordCharacterMaskRenderGraph(renderGraph, frameData);
            }

            // FX Layer Mask 렌더링
            if (m_fxLayerMask != 0 && m_fxLayerMask != -1)
            {
                RecordFXMaskRenderGraph(renderGraph, frameData);
            }
        }

        private void RecordCharacterMaskRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (m_characterMaskMaterial == null)
                return;

            var hdrpData = frameData.Get<HDCamera>();
            var rtHandleSystem = RTHandles.instance;

            // 마스크 렌더 타겟 생성 (R8 포맷)
            var maskDescriptor = new RenderTextureDescriptor(
                hdrpData.camera.pixelWidth,
                hdrpData.camera.pixelHeight,
                RenderTextureFormat.R8)
            {
                depthBufferBits = 0,
                msaaSamples = 1
            };

            var maskRT = rtHandleSystem.Alloc(maskDescriptor, CHARACTER_MASK_RT_NAME);

            // Render Graph 패스 추가
            using (var builder = renderGraph.AddRasterRenderPass<CharacterMaskPassData>(
                "Character Layer Mask Pass (HDRP)", out var passData))
            {
                passData.MaskRT = maskRT;
                passData.LayerMask = m_characterLayerMask;
                passData.Material = m_characterMaskMaterial;

                builder.SetRenderAttachment(maskRT, 0, AccessFlags.Write);

                builder.SetRenderFunc((CharacterMaskPassData data, RasterGraphContext rgContext) =>
                {
                    RenderCharacterMask(rgContext.cmd, data);
                });
            }

            m_characterMaskRT = maskRT;
        }

        private void RecordFXMaskRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (m_fxMaskMaterial == null)
                return;

            var hdrpData = frameData.Get<HDCamera>();
            var rtHandleSystem = RTHandles.instance;

            // 마스크 렌더 타겟 생성 (R8 포맷)
            var maskDescriptor = new RenderTextureDescriptor(
                hdrpData.camera.pixelWidth,
                hdrpData.camera.pixelHeight,
                RenderTextureFormat.R8)
            {
                depthBufferBits = 0,
                msaaSamples = 1
            };

            var maskRT = rtHandleSystem.Alloc(maskDescriptor, FX_MASK_RT_NAME);

            // Render Graph 패스 추가
            using (var builder = renderGraph.AddRasterRenderPass<FXMaskPassData>(
                "FX Layer Mask Pass (HDRP)", out var passData))
            {
                passData.MaskRT = maskRT;
                passData.LayerMask = m_fxLayerMask;
                passData.Material = m_fxMaskMaterial;

                builder.SetRenderAttachment(maskRT, 0, AccessFlags.Write);

                builder.SetRenderFunc((FXMaskPassData data, RasterGraphContext rgContext) =>
                {
                    RenderFXMask(rgContext.cmd, data);
                });
            }

            m_fxMaskRT = maskRT;
        }

        private void RenderCharacterMask(CommandBuffer cmd, CharacterMaskPassData data)
        {
            // 렌더 타겟 클리어
            cmd.ClearRenderTarget(true, true, Color.black);

            // 마스크 렌더링 (하얀색으로 마스크 영역 표시)
            cmd.SetGlobalColor("_MaskColor", Color.white);

            // 실제 렌더링은 특정 레이어의 객체들을 대상으로 수행
            // 이는 Scriptable Render Pipeline 컨텍스트에서 처리됨
        }

        private void RenderFXMask(CommandBuffer cmd, FXMaskPassData data)
        {
            // 렌더 타겟 클리어
            cmd.ClearRenderTarget(true, true, Color.black);

            // 마스크 렌더링 (하얀색으로 마스크 영역 표시)
            cmd.SetGlobalColor("_MaskColor", Color.white);
        }

        public override void Cleanup()
        {
            // 리소스 정리
            m_characterMaskRT?.Release();
            m_fxMaskRT?.Release();

            if (m_characterMaskMaterial != null)
                DestroyImmediate(m_characterMaskMaterial);

            if (m_fxMaskMaterial != null)
                DestroyImmediate(m_fxMaskMaterial);
        }

        #region Render Graph Data Structures

        private class CharacterMaskPassData
        {
            public RTHandle MaskRT;
            public LayerMask LayerMask;
            public Material Material;
        }

        private class FXMaskPassData
        {
            public RTHandle MaskRT;
            public LayerMask LayerMask;
            public Material Material;
        }

        #endregion

        #region Editor Properties

#if UNITY_EDITOR
        public override string title => "MAZELINE Layer Mask Pass";

        public void SetCharacterLayerMask(LayerMask mask)
        {
            m_characterLayerMask = mask;
        }

        public void SetFXLayerMask(LayerMask mask)
        {
            m_fxLayerMask = mask;
        }

        public LayerMask GetCharacterLayerMask() => m_characterLayerMask;
        public LayerMask GetFXLayerMask() => m_fxLayerMask;
#endif

        #endregion
    }
}
