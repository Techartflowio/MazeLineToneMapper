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

        // Layer mask weights
        public ClampedFloatParameter layerMaskApplyWeight = new ClampedFloatParameter(1.0f, 0f, 1.0f);
        public ClampedFloatParameter fxLayerMaskApplyWeight = new ClampedFloatParameter(1.0f, 0f, 1.0f);
        
        // Material for tone mapping shader
        Material m_ToneMappingMaterial;
        
        // RTHandles for layer masks - stored externally
        static RTHandle s_CharacterMaskHandle;
        static RTHandle s_FxMaskHandle;
        
        // Shader & property metadata
        const string ShaderName = "Hidden/HDRP/SGE/ToneMapping";
        static readonly int ExposurePropertyId = Shader.PropertyToID("_Exposure");
        static readonly int LayerMaskWeightPropertyId = Shader.PropertyToID("_LayerMaskApplyWeight");
        static readonly int FxLayerMaskWeightPropertyId = Shader.PropertyToID("_FXLayerMaskApplyWeight");
        static readonly int CharacterMaskPropertyId = Shader.PropertyToID("_CharacterLayerMask");
        static readonly int FxMaskPropertyId = Shader.PropertyToID("_FXLayerMask");
        static readonly int InputTexturePropertyId = Shader.PropertyToID("_InputTexture");

        const int PassNone = 0;
        const int PassGranTurismo = 1;

        public bool IsActive() => toneMapType.value != ToneMapCurveType.None;

        public override void Setup()
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
                return;

            m_ToneMappingMaterial = CoreUtils.CreateEngineMaterial(shader);
        }

        public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination)
        {
            if (m_ToneMappingMaterial == null)
                return;

            if (!IsActive())
            {
                HDUtils.BlitCameraTexture(cmd, source, destination);
                return;
            }

            // Set parameters
            m_ToneMappingMaterial.SetFloat(ExposurePropertyId, exposure.value);
            m_ToneMappingMaterial.SetFloat(LayerMaskWeightPropertyId, layerMaskApplyWeight.value);
            m_ToneMappingMaterial.SetFloat(FxLayerMaskWeightPropertyId, fxLayerMaskApplyWeight.value);

            // Set layer mask textures
            m_ToneMappingMaterial.SetTexture(CharacterMaskPropertyId, GetValidTextureOrBlack(s_CharacterMaskHandle));
            m_ToneMappingMaterial.SetTexture(FxMaskPropertyId, GetValidTextureOrBlack(s_FxMaskHandle));

            // Bind input texture
            m_ToneMappingMaterial.SetTexture(InputTexturePropertyId, source);

            int shaderPass = toneMapType.value == ToneMapCurveType.None ? PassNone : PassGranTurismo;

            var target = destination ?? source;
            HDUtils.DrawFullScreen(cmd, m_ToneMappingMaterial, target, null, shaderPass);
        }

        static Texture GetValidTextureOrBlack(RTHandle handle)
        {
            if (handle == null)
                return Texture2D.blackTexture;

            var rt = handle.rt;
            if (rt != null)
                return rt;

            return Texture2D.blackTexture;
        }

        // No need for explicit viewport handling – HDUtils.DrawFullScreen binds the RTHandle and viewport appropriately.

        public override void Cleanup()
        {
            CoreUtils.Destroy(m_ToneMappingMaterial);
        }
        
        // Static methods to set mask textures from CustomPass
        public static void SetCharacterMask(RTHandle mask)
        {
            s_CharacterMaskHandle = mask;
        }
        
        public static void SetFXMask(RTHandle mask)
        {
            s_FxMaskHandle = mask;
        }
    }

    // Reuse the enum from URP version
    public enum ToneMapCurveType : int
    {
        None = 0,
        GranTurismo = 1
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
