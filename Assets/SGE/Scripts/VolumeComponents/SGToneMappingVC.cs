using System;
using UnityEngine.Rendering.Universal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace ML
{
    public enum ToneMapCurveType : int
    {
        None = 0,
        Filmic = 1,
        KhronosNeutral = 2,
        GranTurismo = 3,
        AGX = 4
    }

    [Serializable, VolumeComponentMenu("SGE/Project A Tone-Mapping")]
    public class SGToneMappingVC : VolumeComponent, IPostProcessComponent
    {
        [SerializeField]public ToneMapCurveTypeParameter ToneMapType =
        new ToneMapCurveTypeParameter(ToneMapCurveType.None);
        
        public ClampedFloatParameter Exposure = new ClampedFloatParameter(1.0f, 0.2f, 7f);
        public ClampedFloatParameter AgxGamma = new ClampedFloatParameter(0.0f, 0f, 1.0f);
        public ClampedFloatParameter AgxGammaPivot = new ClampedFloatParameter(0.8f, 0.01f, 1.0f);
        
        public ClampedFloatParameter LayerMaskApplyWeight = new ClampedFloatParameter(1.00f, 0.1f, 1.0f);
        
        public SGToneMappingVC()
        {
            displayName = "SGE PRJ A Tone Mapping";
        }

        public bool IsActive()
        {
            return (int)ToneMapType.value >= 0 && ToneMapType.overrideState;
        }
    }

    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a <see cref="ToneMapCurveType"/> value.
    /// </summary>
    [Serializable]
    public sealed class ToneMapCurveTypeParameter : VolumeParameter<ToneMapCurveType>
    {
        /// <summary>
        /// Creates a new <see cref="ToneMapCurveTypeParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public ToneMapCurveTypeParameter(ToneMapCurveType value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }
}