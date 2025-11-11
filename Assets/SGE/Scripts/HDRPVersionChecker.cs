using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Experimental.Rendering;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
#endif

namespace ML.ToneMapping.HDRP
{
    /// <summary>
    /// Version compatibility checker for Unity 6.2 and HDRP 17.2
    /// </summary>
    public static class HDRPVersionChecker
    {
        public const string MINIMUM_UNITY_VERSION = "6.2.0";
        public const string MINIMUM_HDRP_VERSION = "17.2.0";
        
        public static bool IsUnity6OrNewer()
        {
            #if UNITY_6_0_OR_NEWER
                return true;
            #else
                return false;
            #endif
        }
        
        public static bool IsHDRP17OrNewer()
        {
            #if HDRP_17_OR_NEWER
                return true;
            #else
                return false;
            #endif
        }
        
        public static bool IsRenderGraphEnabled()
        {
            var hdAsset = GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;
            if (hdAsset == null)
                return false;
                
            // RenderGraph is mandatory in HDRP 17.x
            #if HDRP_17_OR_NEWER
                return true;
            #else
                // For older versions, check the asset setting
                return hdAsset.currentPlatformRenderPipelineSettings.supportRenderGraph;
            #endif
        }
        
        public static string GetVersionInfo()
        {
            string info = "SGE Tone Mapping System\n";
            info += "=======================\n";
            info += $"Unity Version: {Application.unityVersion}\n";
            
            #if UNITY_6_2_OR_NEWER
                info += "✓ Unity 6.2+ detected\n";
            #else
                info += "✗ Unity 6.2+ required\n";
            #endif
            
            var hdAsset = GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;
            if (hdAsset != null)
            {
                info += $"HDRP Asset: {hdAsset.name}\n";
                
                #if HDRP_17_OR_NEWER
                    info += "✓ HDRP 17+ detected\n";
                #else
                    info += "✗ HDRP 17+ required\n";
                #endif
                
                info += $"Render Graph: {(IsRenderGraphEnabled() ? "Enabled" : "Disabled")}\n";
            }
            else
            {
                info += "✗ No HDRP Asset found\n";
            }
            
            return info;
        }
        
        #if UNITY_EDITOR
        [MenuItem("SGE/Tone Mapping/Check Version Compatibility")]
        public static void CheckVersionCompatibility()
        {
            string info = GetVersionInfo();
            
            bool compatible = IsUnity6OrNewer() && IsHDRP17OrNewer() && IsRenderGraphEnabled();
            
            if (compatible)
            {
                EditorUtility.DisplayDialog("Version Check", 
                    info + "\n✓ All requirements met!", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Version Check - Issues Found", 
                    info + "\n✗ Some requirements not met.\n\n" +
                    "Required:\n" +
                    "- Unity 6.2.0 or newer\n" +
                    "- HDRP 17.2.0 or newer\n" +
                    "- Render Graph enabled", "OK");
            }
        }
        
        [InitializeOnLoadMethod]
        static void InitializeVersionCheck()
        {
            if (!IsUnity6OrNewer() || !IsHDRP17OrNewer())
            {
                Debug.LogWarning("[SGE Tone Mapping] This version requires Unity 6.2+ and HDRP 17.2+. " +
                    "Current setup may not work correctly.");
            }
            
            if (!IsRenderGraphEnabled())
            {
                Debug.LogWarning("[SGE Tone Mapping] Render Graph is not enabled. " +
                    "Please enable it in your HDRP Asset for optimal performance.");
            }
        }
        #endif
        
        /// <summary>
        /// Runtime compatibility check
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void RuntimeVersionCheck()
        {
            if (!IsRenderGraphEnabled())
            {
                Debug.LogError("[SGE Tone Mapping] Render Graph is required for HDRP 17.x. " +
                    "Please enable it in your HDRP Asset.");
            }
        }
    }
    
    /// <summary>
    /// Conditional compilation helpers
    /// </summary>
    public static class HDRPCompat
    {
        /// <summary>
        /// Helper method for conditional RenderGraph usage
        /// </summary>
        public static bool UseRenderGraph()
        {
            #if HDRP_17_OR_NEWER
                return true; // Always use RenderGraph in HDRP 17+
            #else
                return HDRPVersionChecker.IsRenderGraphEnabled();
            #endif
        }
        
        /// <summary>
        /// Helper for texture format compatibility
        /// </summary>
        public static GraphicsFormat GetR8Format()
        {
            #if UNITY_6_2_OR_NEWER
                return GraphicsFormat.R8_UNorm;
            #else
                return GraphicsFormat.R8_UNorm; // Same in older versions
            #endif
        }
    }
}
