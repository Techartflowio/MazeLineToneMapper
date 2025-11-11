using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace ML.ToneMapping.HDRP
{
    [Serializable, VolumeComponentMenu("Post-processing/SGE/Tone Mapping")]
    [SupportedOnRenderPipeline(typeof(HDRenderPipelineAsset))]
    public sealed class SGToneMappingHDRP : CustomPostProcessVolumeComponent, IPostProcessComponent
    {
        // Injection point - After TAA and Motion Blur, before final post-processing
        public override CustomPostProcessInjectionPoint injectionPoint => 
            CustomPostProcessInjectionPoint.AfterPostProcess;

        // Tone mapping parameters
        [SerializeField]
        public ToneMapCurveTypeParameter toneMapType = 
            new ToneMapCurveTypeParameter(ToneMapCurveType.None);
        
        public ClampedFloatParameter exposure = new ClampedFloatParameter(1.0f, 0.2f, 7f);
        public ClampedFloatParameter agxGamma = new ClampedFloatParameter(1.0f, 0f, 2.0f);
        public ClampedFloatParameter agxGammaPivot = new ClampedFloatParameter(0.5f, 0.01f, 1.0f);
        
        // Layer mask weights
        public ClampedFloatParameter layerMaskApplyWeight = new ClampedFloatParameter(1.0f, 0f, 1.0f);
        public ClampedFloatParameter fxLayerMaskApplyWeight = new ClampedFloatParameter(1.0f, 0f, 1.0f);
        
        // Material for tone mapping shader
        Material m_Material;
        
        // RTHandles for layer masks - stored externally
        static RTHandle s_CharacterMaskTexture;
        static RTHandle s_FXMaskTexture;
        
        // Shader keywords
        const string k_ShaderName = "Hidden/HDRP/SGE/ToneMapping";
        const string KW_TM_FILMIC = "TM_FILMIC";
        const string KW_TM_NEUTRAL = "TM_NEUTRAL";
        const string KW_TM_GT = "TM_GT";
        const string KW_TM_AGX = "TM_AGX";

        public bool IsActive() => toneMapType.value != ToneMapCurveType.None;

        public override void Setup()
        {
            if (Shader.Find(k_ShaderName) != null)
            {
                m_Material = CoreUtils.CreateEngineMaterial(k_ShaderName);
            }
        }

        public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination)
        {
            if (m_Material == null)
                return;

            if (!IsActive())
            {
                HDUtils.BlitCameraTexture(cmd, source, destination);
                return;
            }

            // Set parameters
            m_Material.SetFloat("_Exposure", exposure.value);
            m_Material.SetFloat("_TonemapAGXGamma", agxGamma.value);
            m_Material.SetFloat("_TonemapAGXGammaPivot", agxGammaPivot.value);
            m_Material.SetFloat("_LayerMaskApplyWeight", layerMaskApplyWeight.value);
            m_Material.SetFloat("_FXLayerMaskApplyWeight", fxLayerMaskApplyWeight.value);

            // Set layer mask textures
            if (s_CharacterMaskTexture != null)
                m_Material.SetTexture("_CharacterLayerMask", s_CharacterMaskTexture);
            else
                m_Material.SetTexture("_CharacterLayerMask", Texture2D.blackTexture);

            if (s_FXMaskTexture != null)
                m_Material.SetTexture("_FXLayerMask", s_FXMaskTexture);
            else
                m_Material.SetTexture("_FXLayerMask", Texture2D.blackTexture);

            // Set keywords based on tone mapping type
            CoreUtils.SetKeyword(m_Material, KW_TM_FILMIC, toneMapType.value == ToneMapCurveType.Filmic);
            CoreUtils.SetKeyword(m_Material, KW_TM_NEUTRAL, toneMapType.value == ToneMapCurveType.KhronosNeutral);
            CoreUtils.SetKeyword(m_Material, KW_TM_GT, toneMapType.value == ToneMapCurveType.GranTurismo);
            CoreUtils.SetKeyword(m_Material, KW_TM_AGX, toneMapType.value == ToneMapCurveType.AGX);

            // Bind input texture
            m_Material.SetTexture("_InputTexture", source);

            int shaderPass = toneMapType.value == ToneMapCurveType.None ? 0 : 1;

            CoreUtils.SetRenderTarget(cmd, destination);
            CoreUtils.DrawFullScreen(cmd, m_Material, shaderPass);
        }

        public override void Cleanup()
        {
            CoreUtils.Destroy(m_Material);
        }
        
        // Static methods to set mask textures from CustomPass
        public static void SetCharacterMask(RTHandle mask)
        {
            s_CharacterMaskTexture = mask;
        }
        
        public static void SetFXMask(RTHandle mask)
        {
            s_FXMaskTexture = mask;
        }
    }

    // Reuse the enum from URP version
    public enum ToneMapCurveType : int
    {
        None = 0,
        Filmic = 1,
        KhronosNeutral = 2,
        GranTurismo = 3,
        AGX = 4
    }

    [Serializable]
    public sealed class ToneMapCurveTypeParameter : VolumeParameter<ToneMapCurveType>
    {
        public ToneMapCurveTypeParameter(ToneMapCurveType value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }
}
