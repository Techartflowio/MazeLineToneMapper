using System;
using ML.Common;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace ML.ToneMapping
{
    [DisallowMultipleRendererFeature("Mazeline ToneMapping")]
    public class MLToneMappingFeature : ScriptableRendererFeature
    {
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;
        private Material _material;
        private MLToneMappingPass _mMlToneMapPass;

        /// <inheritdoc/>
        public override void Create()
        {
            _mMlToneMapPass = new MLToneMappingPass(name);
        }

        /// <inheritdoc/>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview ||
                renderingData.cameraData.cameraType == CameraType.Reflection)
                return;

            if (_material == null)
            {
                var defaultShader = Shader.Find("Hidden/MAZELINE/PostProcess/ToneMapping");
                if (defaultShader != null)
                {
                    _material = new Material(defaultShader);
                }

                return;
            }

            _mMlToneMapPass.renderPassEvent = (RenderPassEvent)injectionPoint;
            _mMlToneMapPass.ConfigureInput(ScriptableRenderPassInput.None);
            _mMlToneMapPass.SetupMembers(_material);

            renderer.EnqueuePass(_mMlToneMapPass);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            _mMlToneMapPass.Dispose();
        }

        public class MLToneMappingPass : ScriptableRenderPass
        {
            private Material m_toneMapMaterial;
            private RTHandle m_copiedColor;
            private MLToneMap m_toneMapComponent;
            // One-hot shader keywords for static paths (future: consolidate to single-pass if desired)
            static readonly string KW_TM_FILMIC = "TM_FILMIC";
            static readonly string KW_TM_NEUTRAL = "TM_NEUTRAL";
            static readonly string KW_TM_GT = "TM_GT";
            static readonly string KW_TM_AGX = "TM_AGX";

            public MLToneMappingPass(string passName)
            {
                profilingSampler = new ProfilingSampler(passName);
            }

            public void SetupMembers(Material material)
            {
                m_toneMapMaterial = material;
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

                m_toneMapComponent = VolumeManager.instance.stack.GetComponent<MLToneMap>();
                if (m_toneMapComponent == null || 
                    m_toneMapComponent.ToneMapType.value == ToneMapCurveType.None || 
                    !m_toneMapComponent.ToneMapType.overrideState)
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

                    using (var builder = renderGraph.AddRasterRenderPass<CopyPassData>("ASP ToneMap Pass Copy Color",
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
                       renderGraph.AddRasterRenderPass<ToneMapPassData>("ASP ToneMap Pass Apply", out var passData))
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
                        data.Material.SetFloat("_ToneMapLowerBound",
                            (m_toneMapComponent.CharacterPixelsToneMapStrength.value));
                        data.Material.SetFloat("_Exposure", m_toneMapComponent.Exposure.value);
                        data.Material.SetFloat("_IgnoreCharacterPixels",
                            m_toneMapComponent.IgnoreCharacterPixels.value ? 1.0f : 0);
                        data.Material.SetFloat("_TonemapAGXGamma", m_toneMapComponent.AgxGamma.value );
                        data.Material.SetFloat("_TonemapAGXGammaPivot", m_toneMapComponent.AgxGammaPivot.value );
                        
                        // One-hot keyword selection (static compile paths)
                        data.Material.DisableKeyword(KW_TM_FILMIC);
                        data.Material.DisableKeyword(KW_TM_NEUTRAL);
                        data.Material.DisableKeyword(KW_TM_GT);
                        data.Material.DisableKeyword(KW_TM_AGX);

                        int shaderPass = 0; // default to None
                        switch (m_toneMapComponent.ToneMapType.value)
                        {
                            case ToneMapCurveType.Filmic:
                                data.Material.EnableKeyword(KW_TM_FILMIC);
                                shaderPass = (int)ToneMapCurveType.Filmic; // temporary: still using pass index
                                break;
                            case ToneMapCurveType.KhronosNeutral:
                                data.Material.EnableKeyword(KW_TM_NEUTRAL);
                                shaderPass = (int)ToneMapCurveType.KhronosNeutral;
                                break;
                            case ToneMapCurveType.GranTurismo:
                                data.Material.EnableKeyword(KW_TM_GT);
                                shaderPass = (int)ToneMapCurveType.GranTurismo;
                                break;
                            case ToneMapCurveType.AGX:
                                data.Material.EnableKeyword(KW_TM_AGX);
                                shaderPass = (int)ToneMapCurveType.AGX;
                                break;
                            case ToneMapCurveType.None:
                            default:
                                shaderPass = (int)ToneMapCurveType.None;
                                break;
                        }

                        // TODO(T13-followup): collapse to a single pass gated by keywords only.
                        DrawTriangle(rgContext.cmd, m_toneMapMaterial, shaderPass);
                    });
                }
            }
        }
    }
}