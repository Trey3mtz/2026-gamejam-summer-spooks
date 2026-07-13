using UnityEngine;
using UnityEngine.UI;

namespace SpookyGame.Rendering
{
    /// <summary>
    /// Owns the low-resolution internal render target that gives the game its
    /// PS1-era pixel structure. The world camera (plus any stacked overlay
    /// cameras, e.g. the in-fiction HUD) renders into a small RenderTexture;
    /// a point-filtered RawImage on a native-resolution overlay canvas
    /// presents it, pillarboxed to the internal aspect ratio.
    ///
    /// Phase 4: also owns the composite material (color crush + dither) on
    /// the RawImage. An instance is created at runtime so property changes
    /// never dirty the material asset; _InternalRes is pushed whenever the
    /// target is (re)built so the dither pattern stays locked to internal
    /// pixels. Phase 5's CRT parameters ride the same material.
    ///
    /// This is deliberately a leaf system: it knows nothing about the player,
    /// input, or game state. Phase 6 wires GameSettings.VideoChanged to
    /// <see cref="SetInternalResolution"/> and toggles composite features.
    /// </summary>
    public class RetroScreenController : MonoBehaviour
    {
        [Header("Internal Resolution")]
        [Tooltip("Horizontal internal resolution. 320x240 is authentic 4:3; 426x240 fills 16:9.")]
        [SerializeField] private int _internalWidth = 320;
        [Tooltip("Vertical internal resolution.")]
        [SerializeField] private int _internalHeight = 240;

        [Header("Wiring")]
        [Tooltip("The base world camera. Its targetTexture is claimed by this component while enabled.")]
        [SerializeField] private Camera _worldCamera;
        [Tooltip("Fullscreen RawImage on the presentation canvas that displays the internal target.")]
        [SerializeField] private RawImage _output;
        [Tooltip("AspectRatioFitter on the RawImage. Set to Fit In Parent; the ratio is driven from here.")]
        [SerializeField] private AspectRatioFitter _aspectFitter;

        [Header("Composite (Phase 4+)")]
        [Tooltip("Material using SpookyGame/UI/RetroComposite. Optional: if empty, the target displays raw.")]
        [SerializeField] private Material _compositeMaterial;

        private static readonly int InternalResId = Shader.PropertyToID("_InternalRes");

        private RenderTexture _target;
        private Material _runtimeMaterial;

        /// <summary>The internal render target. The Phase 5 CRT pass samples this.</summary>
        public RenderTexture Target => _target;

        /// <summary>Runtime composite material instance, for tuning/settings code. Null if none assigned.</summary>
        public Material Composite => _runtimeMaterial;

        private void OnEnable() => CreateTarget();

        private void OnDisable()
        {
            ReleaseTarget();

            if (_runtimeMaterial != null)
            {
                if (_output != null)
                    _output.material = null;
                Destroy(_runtimeMaterial);
                _runtimeMaterial = null;
            }
        }

        /// <summary>
        /// Rebuilds the internal target at a new resolution. Phase 6 hook:
        /// subscribe a caller of this to GameSettings.VideoChanged.
        /// </summary>
        public void SetInternalResolution(int width, int height)
        {
            _internalWidth = Mathf.Max(64, width);
            _internalHeight = Mathf.Max(64, height);
            if (isActiveAndEnabled)
                CreateTarget();
        }

        private void CreateTarget()
        {
            if (_worldCamera == null || _output == null)
            {
                Debug.LogError("[RetroScreenController] World camera or output RawImage not assigned.", this);
                enabled = false;
                return;
            }

            ReleaseTarget();

            _target = new RenderTexture(_internalWidth, _internalHeight, 24, RenderTextureFormat.Default)
            {
                name = "RetroInternalTarget",
                // Point filtering is what produces hard pixel edges on upscale.
                // Bilinear here would smear the image and defeat the entire pass.
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                antiAliasing = 1 // MSAA would soften exactly the edges we want to keep.
            };
            _target.Create();

            _worldCamera.targetTexture = _target;
            _output.texture = _target;

            if (_aspectFitter != null)
                _aspectFitter.aspectRatio = (float)_internalWidth / _internalHeight;

            ApplyComposite();
        }

        private void ApplyComposite()
        {
            if (_compositeMaterial == null)
                return;

            if (_runtimeMaterial == null)
                _runtimeMaterial = Instantiate(_compositeMaterial);

            _runtimeMaterial.SetVector(InternalResId,
                new Vector4(_internalWidth, _internalHeight, 0f, 0f));
            _output.material = _runtimeMaterial;
        }

        private void ReleaseTarget()
        {
            // Detach consumers before releasing so nothing samples a dead RT.
            if (_worldCamera != null && _worldCamera.targetTexture == _target)
                _worldCamera.targetTexture = null;
            if (_output != null && _output.texture == _target)
                _output.texture = null;

            if (_target != null)
            {
                _target.Release();
                Destroy(_target);
                _target = null;
            }
        }
    }
}