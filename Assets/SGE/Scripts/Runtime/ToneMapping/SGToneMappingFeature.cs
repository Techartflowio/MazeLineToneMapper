using System;
using ML.Common;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace ML.ToneMapping
{
    [DisallowMultipleRendererFeature("SGE PRJ A ToneMapping")]
    public class SGToneMappingFeature : ScriptableRendererFeature
    {
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;
        
        public LayerMask layerMask = -1;
        public LayerMask fxLayerMask = -1;
        
        private Material _material;
        private SGToneMappingPass _mSgToneMapPass;
        private CharacterLayerMaskPass _characterLayerMaskPass;
        private FXLayerMaskPass _fxLayerMaskPass;

        /// <inheritdoc/>
        public override void Create()
        {
            _mSgToneMapPass = new SGToneMappingPass(name);
            _characterLayerMaskPass = new CharacterLayerMaskPass(name + "_CharacterMask");
            _fxLayerMaskPass = new FXLayerMaskPass(name + "_FXMask");
        }

        /// <inheritdoc/>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview ||
                renderingData.cameraData.cameraType == CameraType.Reflection)
                return;

            // Character Layer Mask Pass 추가 (BeforeRenderingOpaques 시점에 실행)
            if (layerMask != 0 && layerMask != -1) // 유효한 레이어 마스크가 설정된 경우만
            {
                _characterLayerMaskPass.renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
                _characterLayerMaskPass.SetupLayerMask(layerMask);
                _characterLayerMaskPass.SetupToneMappingPass(_mSgToneMapPass);
                renderer.EnqueuePass(_characterLayerMaskPass);
            }

            // FX Layer Mask Pass 추가 (BeforeRenderingTransparents 시점에 실행)
            if (fxLayerMask != 0 && fxLayerMask != -1) // 유효한 FX 레이어 마스크가 설정된 경우만
            {
                _fxLayerMaskPass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
                _fxLayerMaskPass.SetupLayerMask(fxLayerMask);
                _fxLayerMaskPass.SetupToneMappingPass(_mSgToneMapPass);
                renderer.EnqueuePass(_fxLayerMaskPass);
            }

            if (_material == null)
            {
                var defaultShader = Shader.Find("Hidden/MAZELINE/PostProcess/ToneMapping");
                if (defaultShader != null)
                {
                    _material = new Material(defaultShader);
                }

                return;
            }

            _mSgToneMapPass.renderPassEvent = (RenderPassEvent)injectionPoint;
            _mSgToneMapPass.ConfigureInput(ScriptableRenderPassInput.None);
            _mSgToneMapPass.SetupMembers(_material);

            renderer.EnqueuePass(_mSgToneMapPass);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            _mSgToneMapPass.Dispose();
            _characterLayerMaskPass?.Dispose();
            _fxLayerMaskPass?.Dispose();
        }

        public class SGToneMappingPass : ScriptableRenderPass
        {
            private Material m_toneMapMaterial;
            private RTHandle m_copiedColor;
            private SGToneMappingVC _mToneMappingVcComponent;
            private TextureHandle m_characterLayerMaskHandle = TextureHandle.nullHandle;
            private RTHandle m_characterLayerMaskRTHandle; // Execute 방식용
            private TextureHandle m_fxLayerMaskHandle = TextureHandle.nullHandle;
            private RTHandle m_fxLayerMaskRTHandle; // Execute 방식용
            
            // One-hot shader keywords for static paths (future: collapse to a single pass gated by keywords only)
            static readonly string KW_TM_FILMIC = "TM_FILMIC";
            static readonly string KW_TM_NEUTRAL = "TM_NEUTRAL";
            static readonly string KW_TM_GT = "TM_GT";
            static readonly string KW_TM_AGX = "TM_AGX";

            public SGToneMappingPass(string passName)
            {
                profilingSampler = new ProfilingSampler(passName);
            }

            public void SetupMembers(Material material)
            {
                m_toneMapMaterial = material;
            }
            
            public void SetCharacterLayerMask(TextureHandle maskHandle)
            {
                m_characterLayerMaskHandle = maskHandle;
            }
            
            public void SetCharacterLayerMaskRTHandle(RTHandle maskHandle)
            {
                m_characterLayerMaskRTHandle = maskHandle;
            }
            
            public void SetFXLayerMask(TextureHandle maskHandle)
            {
                m_fxLayerMaskHandle = maskHandle;
            }
            
            public void SetFXLayerMaskRTHandle(RTHandle maskHandle)
            {
                m_fxLayerMaskRTHandle = maskHandle;
            }

            public void Dispose()
            {
                m_copiedColor?.Release();
            }

            private void DrawTriangle(CommandBuffer cmd, Material material, int shaderPass)
            {
                if (SystemInfo.graphicsShaderLevel < 30)
                    cmd.DrawMesh(Util.TriangleMesh, Matrix4x4.identity, material, 0, shaderPass);
                else
                    cmd.DrawProcedural(Matrix4x4.identity, material, shaderPass, MeshTopology.Quads, 4, 1);
            }

            private void DrawTriangle(RasterCommandBuffer cmd, Material material, int shaderPass)
            {
                if (SystemInfo.graphicsShaderLevel < 30)
                    cmd.DrawMesh(Util.TriangleMesh, Matrix4x4.identity, material, 0, shaderPass);
                else
                    cmd.DrawProcedural(Matrix4x4.identity, material, shaderPass, MeshTopology.Quads, 4, 1);
            }

            private class CopyPassData
            {
                public TextureHandle Source;
            }

            private class ToneMapPassData
            {
                public TextureHandle Source;
                public TextureHandle Destination;
                public Material Material;
                public int LutHeight;
                public int LutWidth;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (m_toneMapMaterial == null)
                    return;

                _mToneMappingVcComponent = VolumeManager.instance.stack.GetComponent<SGToneMappingVC>();
                if (_mToneMappingVcComponent == null || 
                    _mToneMappingVcComponent.ToneMapType.value == ToneMapCurveType.None || 
                    !_mToneMappingVcComponent.ToneMapType.overrideState)
                    return;

                var postProcessingData = frameData.Get<UniversalPostProcessingData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                var resourceData = frameData.Get<UniversalResourceData>();

                var colorCopyDescriptor = cameraData.cameraTargetDescriptor;
                colorCopyDescriptor.msaaSamples = 1;
                colorCopyDescriptor.depthBufferBits = (int)DepthBits.None;

                var copiedColorRT = TextureHandle.nullHandle;
                copiedColorRT = UniversalRenderer.CreateRenderGraphTexture(renderGraph, colorCopyDescriptor,
                    "_FullscreenPassColorCopy", false);
                {

                    using (var builder = renderGraph.AddRasterRenderPass<CopyPassData>("SGE ToneMap Pass Copy Color",
                               out var passData, profilingSampler))
                    {
                        passData.Source = resourceData.activeColorTexture;
                        builder.UseTexture(resourceData.activeColorTexture, AccessFlags.Read);

                        //copiedColorRT is now the blit destination
                        builder.SetRenderAttachment(copiedColorRT, 0, AccessFlags.Write);

                        builder.SetRenderFunc((CopyPassData data, RasterGraphContext rgContext) =>
                        {
                            Blitter.BlitTexture(rgContext.cmd, data.Source.IsValid() ? data.Source : null,
                                new Vector4(1, 1, 0, 0), 0.0f, false);
                        });
                    }
                }

                using (var builder =
                       renderGraph.AddRasterRenderPass<ToneMapPassData>("SGE ToneMap Pass Apply", out var passData))
                {
                    builder.UseAllGlobalTextures(true);
                    passData.Source = copiedColorRT;
                    passData.Destination = resourceData.activeColorTexture;
                    passData.Material = m_toneMapMaterial;
                    // bool hdr = postProcessingData.gradingMode == ColorGradingMode.HighDynamicRange;
                    var lutHeight = postProcessingData.lutSize;
                    var lutWidth = lutHeight * lutHeight;
                    passData.LutHeight = lutHeight;
                    passData.LutWidth = lutWidth;

                    builder.UseTexture(passData.Source, AccessFlags.Read);
                    builder.SetRenderAttachment(passData.Destination, 0, AccessFlags.Write);
                    
                    // Character Layer Mask 텍스처 사용
                    if (m_characterLayerMaskHandle.IsValid())
                    {
                        builder.UseTexture(m_characterLayerMaskHandle, AccessFlags.Read);
                    }
                    
                    // FX Layer Mask 텍스처 사용
                    if (m_fxLayerMaskHandle.IsValid())
                    {
                        builder.UseTexture(m_fxLayerMaskHandle, AccessFlags.Read);
                    }
                    
                    //if (m_BindDepthStencilAttachment)
                    //    builder.SetRenderAttachmentDepth(resourcesData.activeDepthTexture, AccessFlags.Write);
                    builder.SetRenderFunc((ToneMapPassData data, RasterGraphContext rgContext) =>
                    {
                        // Color space assumption:
                        // - Input is linear sRGB (scene-linear)
                        // - Output stays linear; sRGB conversion handled by backbuffer/texture formats
                        data.Material.SetVector("_Lut_Params",
                            new Vector4(1f / passData.LutWidth, 1f / passData.LutHeight, passData.LutHeight - 1f, 0));
                        data.Material.SetVector("_BlitScaleBias", Vector2.one);
                        data.Material.SetTexture("_BaseMap", passData.Source);
                        data.Material.SetFloat("_Exposure", _mToneMappingVcComponent.Exposure.value);
                        data.Material.SetFloat("_TonemapAGXGamma", _mToneMappingVcComponent.AgxGamma.value );
                        data.Material.SetFloat("_TonemapAGXGammaPivot", _mToneMappingVcComponent.AgxGammaPivot.value );
                        
                        // Character Layer Mask 텍스처 및 가중치 설정
                        if (m_characterLayerMaskHandle.IsValid())
                        {
                            data.Material.SetTexture("_CharacterLayerMask", m_characterLayerMaskHandle);
                            data.Material.SetFloat("_LayerMaskApplyWeight", _mToneMappingVcComponent.LayerMaskApplyWeight.value);
                        }
                        else if (m_characterLayerMaskRTHandle != null)
                        {
                            // Execute 방식용
                            data.Material.SetTexture("_CharacterLayerMask", m_characterLayerMaskRTHandle);
                            data.Material.SetFloat("_LayerMaskApplyWeight", _mToneMappingVcComponent.LayerMaskApplyWeight.value);
                        }
                        else
                        {
                            // 마스크가 없으면 기본값으로 설정 (모든 영역에 톤매핑 적용)
                            data.Material.SetTexture("_CharacterLayerMask", Texture2D.blackTexture);
                            data.Material.SetFloat("_LayerMaskApplyWeight", 1.0f);
                        }
                        
                        // FX Layer Mask 텍스처 및 가중치 설정
                        if (m_fxLayerMaskHandle.IsValid())
                        {
                            data.Material.SetTexture("_FXLayerMask", m_fxLayerMaskHandle);
                            data.Material.SetFloat("_FXLayerMaskApplyWeight", _mToneMappingVcComponent.FXLayerMaskApplyWeight.value);
                        }
                        else if (m_fxLayerMaskRTHandle != null)
                        {
                            // Execute 방식용
                            data.Material.SetTexture("_FXLayerMask", m_fxLayerMaskRTHandle);
                            data.Material.SetFloat("_FXLayerMaskApplyWeight", _mToneMappingVcComponent.FXLayerMaskApplyWeight.value);
                        }
                        else
                        {
                            // 마스크가 없으면 기본값으로 설정 (모든 영역에 톤매핑 적용)
                            data.Material.SetTexture("_FXLayerMask", Texture2D.blackTexture);
                            data.Material.SetFloat("_FXLayerMaskApplyWeight", 1.0f);
                        }
                        
                        // 키워드 기반 단일 패스 사용
                        data.Material.DisableKeyword(KW_TM_FILMIC);
                        data.Material.DisableKeyword(KW_TM_NEUTRAL);
                        data.Material.DisableKeyword(KW_TM_GT);
                        data.Material.DisableKeyword(KW_TM_AGX);

                        int shaderPass = 0; // 0 = None, 1 = Unified ToneMapping
                        switch (_mToneMappingVcComponent.ToneMapType.value)
                        {
                            case ToneMapCurveType.Filmic:
                                data.Material.EnableKeyword(KW_TM_FILMIC);
                                shaderPass = 1; // 통합된 톤매핑 패스 사용
                                break;
                            case ToneMapCurveType.KhronosNeutral:
                                data.Material.EnableKeyword(KW_TM_NEUTRAL);
                                shaderPass = 1;
                                break;
                            case ToneMapCurveType.GranTurismo:
                                data.Material.EnableKeyword(KW_TM_GT);
                                shaderPass = 1;
                                break;
                            case ToneMapCurveType.AGX:
                                data.Material.EnableKeyword(KW_TM_AGX);
                                shaderPass = 1;
                                break;
                            case ToneMapCurveType.None:
                            default:
                                shaderPass = 0; // None 패스
                                break;
                        }

                        DrawTriangle(rgContext.cmd, m_toneMapMaterial, shaderPass);
                    });
                }
            }
        }

        // Character Layer Mask 렌더 패스 - 특정 레이어만 렌더링하여 마스크 텍스처 생성
        public class CharacterLayerMaskPass : ScriptableRenderPass
        {
            private LayerMask m_layerMask;
            private SGToneMappingPass m_toneMappingPass;
            private Shader m_maskShader;
            private Material m_maskMaterial;
            private RTHandle m_maskTextureHandle;
            private const string k_CharacterLayerMaskTextureName = "_CharacterLayerMaskTexture";
            private const string k_MaskShaderName = "Hidden/MAZELINE/PostProcess/CharacterLayerMask";

            public CharacterLayerMaskPass(string passName)
            {
                profilingSampler = new ProfilingSampler(passName);
            }

            public void SetupLayerMask(LayerMask layerMask)
            {
                m_layerMask = layerMask;
            }

            public void SetupToneMappingPass(SGToneMappingPass toneMappingPass)
            {
                m_toneMappingPass = toneMappingPass;
            }

            public void Dispose()
            {
                if (m_maskMaterial)
                {
                    UnityEngine.Object.DestroyImmediate(m_maskMaterial);
                    m_maskMaterial = null;
                }
                m_maskTextureHandle?.Release();
            }

            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                // 마스크 텍스처 생성
                var descriptor = cameraTextureDescriptor;
                descriptor.colorFormat = RenderTextureFormat.R8;
                descriptor.depthBufferBits = (int)DepthBits.None;
                descriptor.msaaSamples = 1;

                RenderingUtils.ReAllocateIfNeeded(ref m_maskTextureHandle, descriptor,
                    FilterMode.Point, TextureWrapMode.Clamp, name: k_CharacterLayerMaskTextureName);

                ConfigureTarget(m_maskTextureHandle);
                ConfigureClear(ClearFlag.All, Color.black);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (m_maskTextureHandle == null)
                    return;

                // 마스크 셰이더 로드
                if (m_maskShader == null)
                {
                    m_maskShader = Shader.Find(k_MaskShaderName);
                    if (m_maskShader == null)
                    {
                        Debug.LogWarning($"Character Layer Mask Shader not found: {k_MaskShaderName}");
                        return;
                    }
                    m_maskMaterial = new Material(m_maskShader);
                }

                var cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    // SortingSettings: 카메라 기준 정렬
                    var opaqueSortingSettings = new SortingSettings(renderingData.cameraData.camera)
                    {
                        criteria = SortingCriteria.CommonOpaque
                    };
                    var transparentSortingSettings = new SortingSettings(renderingData.cameraData.camera)
                    {
                        criteria = SortingCriteria.CommonTransparent
                    };

                    // 불투명 렌더링
                    var opaqueFilteringSettings = new FilteringSettings(RenderQueueRange.opaque, m_layerMask.value);
                    var opaqueDrawingSettings = new DrawingSettings(
                        new ShaderTagId("UniversalForward"),
                        opaqueSortingSettings
                    )
                    {
                        overrideMaterial = m_maskMaterial,
                        overrideMaterialPassIndex = 0
                    };
                    
                    context.DrawRenderers(renderingData.cullResults, ref opaqueDrawingSettings, ref opaqueFilteringSettings);

                    // 투명 렌더링 (캐릭터 복장/장신구의 투명 부분 포함)
                    var transparentFilteringSettings = new FilteringSettings(RenderQueueRange.transparent, m_layerMask.value);
                    var transparentDrawingSettings = new DrawingSettings(
                        new ShaderTagId("UniversalForward"),
                        transparentSortingSettings
                    )
                    {
                        overrideMaterial = m_maskMaterial,
                        overrideMaterialPassIndex = 0
                    };
                    
                    context.DrawRenderers(renderingData.cullResults, ref transparentDrawingSettings, ref transparentFilteringSettings);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);

                // ToneMapping Pass에 마스크 텍스처 전달 (Execute 방식)
                if (m_toneMappingPass != null && m_maskTextureHandle != null)
                {
                    m_toneMappingPass.SetCharacterLayerMaskRTHandle(m_maskTextureHandle);
                }
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (m_layerMask == 0 || m_layerMask == -1)
                    return;

                var cameraData = frameData.Get<UniversalCameraData>();
                var resourceData = frameData.Get<UniversalResourceData>();

                // 마스크 셰이더 로드
                if (m_maskShader == null)
                {
                    m_maskShader = Shader.Find(k_MaskShaderName);
                    if (m_maskShader == null)
                    {
                        Debug.LogWarning($"Character Layer Mask Shader not found: {k_MaskShaderName}");
                        return;
                    }
                    m_maskMaterial = new Material(m_maskShader);
                }

                // 마스크 텍스처 생성 (1채널 R8)
                var maskDescriptor = cameraData.cameraTargetDescriptor;
                maskDescriptor.colorFormat = RenderTextureFormat.R8;
                maskDescriptor.depthBufferBits = (int)DepthBits.None;
                maskDescriptor.msaaSamples = 1;

                var maskTexture = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph, maskDescriptor, k_CharacterLayerMaskTextureName, false);

                // 별도의 Depth 텍스처 생성 (마스크 렌더링용)
                var depthDescriptor = cameraData.cameraTargetDescriptor;
                depthDescriptor.colorFormat = RenderTextureFormat.Depth;
                depthDescriptor.depthBufferBits = (int)DepthBits.Depth32;
                depthDescriptor.msaaSamples = 1;

                var maskDepthTexture = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph, depthDescriptor, "_CharacterLayerMaskDepth", false);

                // Universal Rendering Data에서 cullResults 가져오기
                var renderingData = frameData.Get<UniversalRenderingData>();

                // 불투명 렌더러 리스트 생성
                var opaqueRendererListParams = new RendererListParams(
                    cullingResults: renderingData.cullResults,
                    drawSettings: new DrawingSettings(
                        new ShaderTagId("UniversalForward"),
                        new SortingSettings(cameraData.camera)
                        {
                            criteria = SortingCriteria.CommonOpaque
                        }
                    )
                    {
                        overrideMaterial = m_maskMaterial,
                        overrideMaterialPassIndex = 0
                    },
                    filteringSettings: new FilteringSettings(RenderQueueRange.opaque, m_layerMask.value)
                );
                var opaqueRendererListHandle = renderGraph.CreateRendererList(opaqueRendererListParams);

                // 투명 렌더러 리스트 생성
                var transparentRendererListParams = new RendererListParams(
                    cullingResults: renderingData.cullResults,
                    drawSettings: new DrawingSettings(
                        new ShaderTagId("UniversalForward"),
                        new SortingSettings(cameraData.camera)
                        {
                            criteria = SortingCriteria.CommonTransparent
                        }
                    )
                    {
                        overrideMaterial = m_maskMaterial,
                        overrideMaterialPassIndex = 0
                    },
                    filteringSettings: new FilteringSettings(RenderQueueRange.transparent, m_layerMask.value)
                );
                var transparentRendererListHandle = renderGraph.CreateRendererList(transparentRendererListParams);

                // 렌더링 패스 추가
                using (var builder = renderGraph.AddRasterRenderPass<CharacterMaskRenderGraphPassData>(
                    "Character Layer Mask Pass (RenderGraph)", out var passData, profilingSampler))
                {
                    passData.OpaqueRendererList = opaqueRendererListHandle;
                    passData.TransparentRendererList = transparentRendererListHandle;
                    passData.MaskTexture = maskTexture;

                    builder.SetRenderAttachment(maskTexture, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(maskDepthTexture, AccessFlags.Write);

                    builder.UseRendererList(opaqueRendererListHandle);
                    builder.UseRendererList(transparentRendererListHandle);

                    builder.SetRenderFunc((CharacterMaskRenderGraphPassData data, RasterGraphContext rgContext) =>
                    {
                        var cmd = rgContext.cmd;

                        // 마스크 텍스처 클리어 (color=black, depth=1.0)
                        cmd.ClearRenderTarget(true, true, Color.black);

                        // 불투명 렌더링
                        cmd.DrawRendererList(data.OpaqueRendererList);

                        // 투명 렌더링
                        cmd.DrawRendererList(data.TransparentRendererList);
                    });
                }

                // ToneMapping Pass에 마스크 텍스처 전달
                if (m_toneMappingPass != null)
                {
                    m_toneMappingPass.SetCharacterLayerMask(maskTexture);
                }
            }

            private class CharacterMaskRenderGraphPassData
            {
                public RendererListHandle OpaqueRendererList;
                public RendererListHandle TransparentRendererList;
                public TextureHandle MaskTexture;
            }
        }

        // FX Layer Mask 렌더 패스 - FX 레이어(파티클/이펙트)를 위한 마스크 텍스처 생성
        public class FXLayerMaskPass : ScriptableRenderPass
        {
            private LayerMask m_layerMask;
            private SGToneMappingPass m_toneMappingPass;
            private Shader m_maskShader;
            private Material m_maskMaterial;
            private RTHandle m_maskTextureHandle;
            private const string k_FXLayerMaskTextureName = "_FXLayerMaskTexture";
            private const string k_MaskShaderName = "Hidden/MAZELINE/PostProcess/FXLayerMask";

            public FXLayerMaskPass(string passName)
            {
                profilingSampler = new ProfilingSampler(passName);
            }

            public void SetupLayerMask(LayerMask layerMask)
            {
                m_layerMask = layerMask;
            }

            public void SetupToneMappingPass(SGToneMappingPass toneMappingPass)
            {
                m_toneMappingPass = toneMappingPass;
            }

            public void Dispose()
            {
                if (m_maskMaterial)
                {
                    UnityEngine.Object.DestroyImmediate(m_maskMaterial);
                    m_maskMaterial = null;
                }
                m_maskTextureHandle?.Release();
            }

            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                // 마스크 텍스처 생성
                var descriptor = cameraTextureDescriptor;
                descriptor.colorFormat = RenderTextureFormat.R8;
                descriptor.depthBufferBits = (int)DepthBits.None;
                descriptor.msaaSamples = 1;

                RenderingUtils.ReAllocateIfNeeded(ref m_maskTextureHandle, descriptor,
                    FilterMode.Point, TextureWrapMode.Clamp, name: k_FXLayerMaskTextureName);

                ConfigureTarget(m_maskTextureHandle);
                ConfigureClear(ClearFlag.All, Color.black);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (m_maskTextureHandle == null)
                    return;

                // 마스크 셰이더 로드
                if (m_maskShader == null)
                {
                    m_maskShader = Shader.Find(k_MaskShaderName);
                    if (m_maskShader == null)
                    {
                        Debug.LogWarning($"FX Layer Mask Shader not found: {k_MaskShaderName}");
                        return;
                    }
                    m_maskMaterial = new Material(m_maskShader);
                }

                var cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    // SortingSettings: 카메라 기준 정렬
                    var transparentSortingSettings = new SortingSettings(renderingData.cameraData.camera)
                    {
                        criteria = SortingCriteria.CommonTransparent
                    };

                    // FX 레이어는 주로 투명 렌더링이므로 투명 렌더링에 집중
                    // Unlit 셰이더 태그도 지원 (UniversalForward, SRPDefaultUnlit)
                    var transparentFilteringSettings = new FilteringSettings(RenderQueueRange.transparent, m_layerMask.value);
                    
                    // UniversalForward 태그 (Lit 셰이더)
                    var transparentDrawingSettings = new DrawingSettings(
                        new ShaderTagId("UniversalForward"),
                        transparentSortingSettings
                    )
                    {
                        overrideMaterial = m_maskMaterial,
                        overrideMaterialPassIndex = 0
                    };
                    
                    // SRPDefaultUnlit 태그 추가 (Unlit 셰이더)
                    transparentDrawingSettings.SetShaderPassName(1, new ShaderTagId("SRPDefaultUnlit"));
                    
                    context.DrawRenderers(renderingData.cullResults, ref transparentDrawingSettings, ref transparentFilteringSettings);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);

                // ToneMapping Pass에 마스크 텍스처 전달 (Execute 방식)
                if (m_toneMappingPass != null && m_maskTextureHandle != null)
                {
                    m_toneMappingPass.SetFXLayerMaskRTHandle(m_maskTextureHandle);
                }
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (m_layerMask == 0 || m_layerMask == -1)
                    return;

                var cameraData = frameData.Get<UniversalCameraData>();
                var resourceData = frameData.Get<UniversalResourceData>();

                // 마스크 셰이더 로드
                if (m_maskShader == null)
                {
                    m_maskShader = Shader.Find(k_MaskShaderName);
                    if (m_maskShader == null)
                    {
                        Debug.LogWarning($"FX Layer Mask Shader not found: {k_MaskShaderName}");
                        return;
                    }
                    m_maskMaterial = new Material(m_maskShader);
                }

                // 마스크 텍스처 생성 (1채널 R8)
                var maskDescriptor = cameraData.cameraTargetDescriptor;
                maskDescriptor.colorFormat = RenderTextureFormat.R8;
                maskDescriptor.depthBufferBits = (int)DepthBits.None;
                maskDescriptor.msaaSamples = 1;

                var maskTexture = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph, maskDescriptor, k_FXLayerMaskTextureName, false);

                // 별도의 Depth 텍스처 생성 (마스크 렌더링용)
                var depthDescriptor = cameraData.cameraTargetDescriptor;
                depthDescriptor.colorFormat = RenderTextureFormat.Depth;
                depthDescriptor.depthBufferBits = (int)DepthBits.Depth32;
                depthDescriptor.msaaSamples = 1;

                var maskDepthTexture = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph, depthDescriptor, "_FXLayerMaskDepth", false);

                // Universal Rendering Data에서 cullResults 가져오기
                var renderingData = frameData.Get<UniversalRenderingData>();

                // 투명 렌더러 리스트 생성 (FX 레이어는 주로 투명 렌더링)
                var transparentDrawingSettings = new DrawingSettings(
                    new ShaderTagId("UniversalForward"),
                    new SortingSettings(cameraData.camera)
                    {
                        criteria = SortingCriteria.CommonTransparent
                    }
                )
                {
                    overrideMaterial = m_maskMaterial,
                    overrideMaterialPassIndex = 0
                };
                
                // Unlit 셰이더 태그 추가
                transparentDrawingSettings.SetShaderPassName(1, new ShaderTagId("SRPDefaultUnlit"));

                var transparentRendererListParams = new RendererListParams(
                    cullingResults: renderingData.cullResults,
                    drawSettings: transparentDrawingSettings,
                    filteringSettings: new FilteringSettings(RenderQueueRange.transparent, m_layerMask.value)
                );
                var transparentRendererListHandle = renderGraph.CreateRendererList(transparentRendererListParams);

                // 렌더링 패스 추가
                using (var builder = renderGraph.AddRasterRenderPass<FXMaskRenderGraphPassData>(
                    "FX Layer Mask Pass (RenderGraph)", out var passData, profilingSampler))
                {
                    passData.TransparentRendererList = transparentRendererListHandle;
                    passData.MaskTexture = maskTexture;

                    builder.SetRenderAttachment(maskTexture, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(maskDepthTexture, AccessFlags.Write);

                    builder.UseRendererList(transparentRendererListHandle);

                    builder.SetRenderFunc((FXMaskRenderGraphPassData data, RasterGraphContext rgContext) =>
                    {
                        var cmd = rgContext.cmd;

                        // 마스크 텍스처 클리어 (color=black, depth=1.0)
                        cmd.ClearRenderTarget(true, true, Color.black);

                        // 투명 렌더링 (Additive 블렌딩 이펙트 포함)
                        cmd.DrawRendererList(data.TransparentRendererList);
                    });
                }

                // ToneMapping Pass에 마스크 텍스처 전달
                if (m_toneMappingPass != null)
                {
                    m_toneMappingPass.SetFXLayerMask(maskTexture);
                }
            }

            private class FXMaskRenderGraphPassData
            {
                public RendererListHandle TransparentRendererList;
                public TextureHandle MaskTexture;
            }
        }
    }
}