using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering.RenderGraphModule;

namespace ML.ToneMapping
{
    /// <summary>
    /// HDRP용 ToneMapping CustomPostProcess
    /// Render Graph 기반으로 톤매핑 처리를 수행
    /// CustomPostProcessVolumeComponent를 통해 Volume 시스템과 연동
    /// </summary>
    public class SGToneMappingCustomPostProcess : CustomPostProcess
    {
        // 셰이더 및 머티리얼
        private Material m_toneMappingMaterial;
        private Shader m_toneMappingShader;

        // 마스크 텍스처 핸들
        private RTHandle m_characterMaskRT;
        private RTHandle m_fxMaskRT;

        // Shader Keywords
        private static readonly string KW_TM_FILMIC = "TM_FILMIC";
        private static readonly string KW_TM_NEUTRAL = "TM_NEUTRAL";
        private static readonly string KW_TM_GT = "TM_GT";
        private static readonly string KW_TM_AGX = "TM_AGX";

        public SGToneMappingCustomPostProcess()
        {
            name = "MAZELINE Tone Mapping (HDRP)";
        }

        public override void Setup()
        {
            // 셰이더 로드
            m_toneMappingShader = Shader.Find("Hidden/MAZELINE/PostProcess/ToneMapping");
            if (m_toneMappingShader != null)
            {
                m_toneMappingMaterial = new Material(m_toneMappingShader);
            }
        }

        public override void Render(CommandBuffer cmd, HDCamera hdCamera, RTHandle srcRT, RTHandle dstRT)
        {
            // 레거시 경로 - Render Graph 방식에서는 호출되지 않음
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer contextContainer)
        {
            if (m_toneMappingMaterial == null)
                return;

            // Volume Component에서 설정 가져오기
            var volumeStack = VolumeManager.instance.stack;
            var toneMappingComponent = volumeStack.GetComponent<SGToneMappingHDRP>();

            if (toneMappingComponent == null || !toneMappingComponent.IsActive())
                return;

            // Render Graph 패스 추가
            using (var builder = renderGraph.AddRasterRenderPass<ToneMappingPassData>(
                "MAZELINE Tone Mapping (HDRP)", out var passData))
            {
                var cameraData = contextContainer.Get<HDCamera>();

                passData.Material = m_toneMappingMaterial;
                passData.ToneMappingComponent = toneMappingComponent;

                // 입출력 텍스처 설정
                var resourceData = contextContainer.Get<HDResourceData>();
                passData.InputRT = resourceData.GetTexture(HDShaderIDs._ColorBufferRT);
                passData.OutputRT = resourceData.GetTexture(HDShaderIDs._ColorBufferRT);

                // 텍스처 의존성 설정
                builder.UseTexture(passData.InputRT, AccessFlags.Read);
                builder.SetRenderAttachment(passData.OutputRT, 0, AccessFlags.Write);

                // 마스크 텍스처 설정 (있으면 읽기 설정)
                if (m_characterMaskRT != null)
                    builder.UseTexture(m_characterMaskRT, AccessFlags.Read);

                if (m_fxMaskRT != null)
                    builder.UseTexture(m_fxMaskRT, AccessFlags.Read);

                // 렌더 함수 설정
                builder.SetRenderFunc((ToneMappingPassData data, RasterGraphContext rgContext) =>
                {
                    RenderToneMapping(rgContext.cmd, data);
                });
            }
        }

        private void RenderToneMapping(CommandBuffer cmd, ToneMappingPassData passData)
        {
            if (passData.Material == null || passData.ToneMappingComponent == null)
                return;

            var component = passData.ToneMappingComponent;

            // 셰이더 파라미터 설정
            passData.Material.SetFloat("_Exposure", component.Exposure.value);
            passData.Material.SetFloat("_TonemapAGXGamma", component.AgxGamma.value);
            passData.Material.SetFloat("_TonemapAGXGammaPivot", component.AgxGammaPivot.value);

            // 레이어 마스크 가중치 설정
            passData.Material.SetFloat("_LayerMaskApplyWeight", component.LayerMaskApplyWeight.value);
            passData.Material.SetFloat("_FXLayerMaskApplyWeight", component.FXLayerMaskApplyWeight.value);

            // 마스크 텍스처 설정
            if (m_characterMaskRT != null)
            {
                passData.Material.SetTexture("_CharacterLayerMask", m_characterMaskRT);
            }
            else
            {
                passData.Material.SetTexture("_CharacterLayerMask", Texture2D.blackTexture);
            }

            if (m_fxMaskRT != null)
            {
                passData.Material.SetTexture("_FXLayerMask", m_fxMaskRT);
            }
            else
            {
                passData.Material.SetTexture("_FXLayerMask", Texture2D.blackTexture);
            }

            // 입력 텍스처 설정
            passData.Material.SetTexture("_BaseMap", passData.InputRT);

            // 톤매핑 타입별 키워드 설정 및 패스 선택
            DisableToneMappingKeywords(passData.Material);

            int shaderPass = 0;
            switch (component.ToneMapType.value)
            {
                case ToneMapCurveType.Filmic:
                    passData.Material.EnableKeyword(KW_TM_FILMIC);
                    shaderPass = 1;
                    break;
                case ToneMapCurveType.KhronosNeutral:
                    passData.Material.EnableKeyword(KW_TM_NEUTRAL);
                    shaderPass = 1;
                    break;
                case ToneMapCurveType.GranTurismo:
                    passData.Material.EnableKeyword(KW_TM_GT);
                    shaderPass = 1;
                    break;
                case ToneMapCurveType.AGX:
                    passData.Material.EnableKeyword(KW_TM_AGX);
                    shaderPass = 1;
                    break;
                case ToneMapCurveType.None:
                default:
                    shaderPass = 0;
                    break;
            }

            // 풀스크린 드로우
            Blitter.BlitTexture(cmd, passData.InputRT, passData.OutputRT,
                new Vector4(1, 1, 0, 0), shaderPass, false);
        }

        private void DisableToneMappingKeywords(Material material)
        {
            material.DisableKeyword(KW_TM_FILMIC);
            material.DisableKeyword(KW_TM_NEUTRAL);
            material.DisableKeyword(KW_TM_GT);
            material.DisableKeyword(KW_TM_AGX);
        }

        public override void Cleanup()
        {
            if (m_toneMappingMaterial != null)
            {
                DestroyImmediate(m_toneMappingMaterial);
                m_toneMappingMaterial = null;
            }

            m_characterMaskRT?.Release();
            m_fxMaskRT?.Release();
        }

        public void SetCharacterMaskRT(RTHandle maskRT)
        {
            m_characterMaskRT = maskRT;
        }

        public void SetFXMaskRT(RTHandle maskRT)
        {
            m_fxMaskRT = maskRT;
        }

        #region Render Graph Data Structures

        private class ToneMappingPassData
        {
            public Material Material;
            public SGToneMappingHDRP ToneMappingComponent;
            public RTHandle InputRT;
            public RTHandle OutputRT;
        }

        #endregion
    }
}
