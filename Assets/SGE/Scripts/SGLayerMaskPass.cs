using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RendererUtils;

namespace ML.ToneMapping.HDRP
{
    /// <summary>
    /// Custom pass for rendering character and FX layer masks in HDRP 17.x
    /// </summary>
    public class SGLayerMaskPass : CustomPass
    {
        public LayerMask characterLayerMask = -1;
        public LayerMask fxLayerMask = -1;
        
        Material m_CharacterMaskMaterial;
        Material m_FXMaskMaterial;
        
        RTHandle m_CharacterMaskBuffer;
        RTHandle m_FXMaskBuffer;
        
        const string k_CharacterMaskShader = "Hidden/HDRP/SGE/CharacterLayerMask";
        const string k_FXMaskShader = "Hidden/HDRP/SGE/FXLayerMask";
        
        ShaderTagId[] m_ShaderTags;

        protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            // Initialize shader tags for forward rendering
            m_ShaderTags = new ShaderTagId[]
            {
                new ShaderTagId("Forward"),
                new ShaderTagId("ForwardOnly"),
                new ShaderTagId("SRPDefaultUnlit"),
                new ShaderTagId("FirstPass")
            };
            
            // Create materials for mask rendering
            var characterShader = Shader.Find(k_CharacterMaskShader);
            if (characterShader != null)
            {
                m_CharacterMaskMaterial = CoreUtils.CreateEngineMaterial(characterShader);
            }
            
            var fxShader = Shader.Find(k_FXMaskShader);
            if (fxShader != null)
            {
                m_FXMaskMaterial = CoreUtils.CreateEngineMaterial(fxShader);
            }
            
            // Allocate render targets for masks with proper parameters for HDRP 17.x
            m_CharacterMaskBuffer = RTHandles.Alloc(
                scaleFactor: Vector2.one, 
                slices: TextureXR.slices, 
                dimension: TextureXR.dimension,
                colorFormat: GraphicsFormat.R8_UNorm,
                useDynamicScale: true, 
                name: "_CharacterLayerMask",
                wrapMode: TextureWrapMode.Clamp,
                filterMode: FilterMode.Point
            );
            
            m_FXMaskBuffer = RTHandles.Alloc(
                scaleFactor: Vector2.one,
                slices: TextureXR.slices,
                dimension: TextureXR.dimension,
                colorFormat: GraphicsFormat.R8_UNorm,
                useDynamicScale: true,
                name: "_FXLayerMask",
                wrapMode: TextureWrapMode.Clamp,
                filterMode: FilterMode.Point
            );
        }

        protected override void Execute(CustomPassContext ctx)
        {
            // Render Character Mask
            if (characterLayerMask != 0 && m_CharacterMaskMaterial != null)
            {
                RenderCharacterMask(ctx);
            }
            
            if (fxLayerMask != 0 && m_FXMaskMaterial != null)
            {
                RenderFXMask(ctx);
            }
            
            PassMasksToToneMapping();
        }
        
        void RenderCharacterMask(CustomPassContext ctx)
        {
            CoreUtils.SetRenderTarget(ctx.cmd, m_CharacterMaskBuffer, ClearFlag.Color, Color.black);
            
            var result = new RendererListDesc(m_ShaderTags, ctx.cullingResults, ctx.hdCamera.camera)
            {
                rendererConfiguration = PerObjectData.None,
                renderQueueRange = RenderQueueRange.all,
                sortingCriteria = SortingCriteria.CommonOpaque | SortingCriteria.CommonTransparent,
                overrideMaterial = m_CharacterMaskMaterial,
                overrideMaterialPassIndex = 0,
                layerMask = characterLayerMask
            };
            
            var rendererList = ctx.renderContext.CreateRendererList(result);
            CoreUtils.DrawRendererList(ctx.renderContext, ctx.cmd, rendererList);
        }
        
        void RenderFXMask(CustomPassContext ctx)
        {
            CoreUtils.SetRenderTarget(ctx.cmd, m_FXMaskBuffer, ClearFlag.Color, Color.black);
            
            var result = new RendererListDesc(m_ShaderTags, ctx.cullingResults, ctx.hdCamera.camera)
            {
                rendererConfiguration = PerObjectData.None,
                renderQueueRange = RenderQueueRange.transparent,
                sortingCriteria = SortingCriteria.CommonTransparent,
                overrideMaterial = m_FXMaskMaterial,
                overrideMaterialPassIndex = 0,
                layerMask = fxLayerMask
            };
            
            var rendererList = ctx.renderContext.CreateRendererList(result);
            CoreUtils.DrawRendererList(ctx.renderContext, ctx.cmd, rendererList);
        }
        
        void PassMasksToToneMapping()
        {
            // Pass masks to tone mapping using static methods
            SGToneMappingHDRP.SetCharacterMask(m_CharacterMaskBuffer);
            SGToneMappingHDRP.SetFXMask(m_FXMaskBuffer);
        }

        protected override void Cleanup()
        {
            CoreUtils.Destroy(m_CharacterMaskMaterial);
            CoreUtils.Destroy(m_FXMaskMaterial);
            m_CharacterMaskBuffer?.Release();
            m_FXMaskBuffer?.Release();
        }
    }
}
