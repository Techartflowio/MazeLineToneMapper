using System;
using NRP.Common;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace NRP.ToneMapping
{
    [DisallowMultipleRendererFeature("NRP ToneMapping")]
    public class NRPToneMappingFeature : ScriptableRendererFeature
    {
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;
        private Material _material;
        private NRPToneMappingPass _mNrpToneMapPass;

        /// <inheritdoc/>
        public override void Create()
        {
            _mNrpToneMapPass = new NRPToneMappingPass(name);
        }

        /// <inheritdoc/>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview ||
                renderingData.cameraData.cameraType == CameraType.Reflection)
                return;

            if (_material == null)
            {
                var defaultShader = Shader.Find("Hidden/NTRANCE/PostProcess/ToneMapping");
                if (defaultShader != null)
                {
                    _material = new Material(defaultShader);
                }

                return;
            }

            _mNrpToneMapPass.renderPassEvent = (RenderPassEvent)injectionPoint;
            _mNrpToneMapPass.ConfigureInput(ScriptableRenderPassInput.None);
            _mNrpToneMapPass.SetupMembers(_material);

            renderer.EnqueuePass(_mNrpToneMapPass);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            _mNrpToneMapPass.Dispose();
        }

        public class NRPToneMappingPass : ScriptableRenderPass
        {
            private Material m_toneMapMaterial;
            private RTHandle m_copiedColor;
            private NRPToneMap m_toneMapComponent;

            public NRPToneMappingPass(string passName)
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

                m_toneMapComponent = VolumeManager.instance.stack.GetComponent<NRPToneMap>();
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
                        data.Material.SetVector("_Lut_Params",
                            new Vector4(1f / passData.LutWidth, 1f / passData.LutHeight, passData.LutHeight - 1f, 0));
                        data.Material.SetVector("_BlitScaleBias", Vector2.one);
                        data.Material.SetTexture("_BaseMap", passData.Source);
                        data.Material.SetFloat("_ToneMapLowerBound",
                            (m_toneMapComponent.CharacterPixelsToneMapStrength.value));
                        data.Material.SetFloat("_Exposure", m_toneMapComponent.Exposure.value);
                        data.Material.SetFloat("_IgnoreCharacterPixels",
                            m_toneMapComponent.IgnoreCharacterPixels.value ? 1.0f : 0);
						// NRPToneMappingFeature.cs에서
						int shaderPass = (int)m_toneMapComponent.ToneMapType.value;
						// GT 스타일 톤매핑은 0번 패스를 사용
						if (m_toneMapComponent.ToneMapType.value == ToneMapCurveType.GranTurismo)
							shaderPass = 0;
                        DrawTriangle(rgContext.cmd, m_toneMapMaterial, shaderPass);
                    });
                }
            }
        }
    }
}