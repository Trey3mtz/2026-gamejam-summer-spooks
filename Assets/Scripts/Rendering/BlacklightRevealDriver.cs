using UnityEngine;
using SpookyGame.Core;
using SpookyGame.Player;

namespace SpookyGame.Rendering
{
    /// <summary>
    /// Pushes the blacklight's spot-cone data into global shader parameters that
    /// every "SpookyGame/BlacklightReveal" material reads. Lives on the held
    /// blacklight prefab next to <see cref="HeldLightVisual"/>, following the same
    /// bind-to-BatteryLight pattern: BatteryLight remains the single source of
    /// truth for on/off; this class is a pure view onto it.
    ///
    /// Cone shape (range, inner/outer angle) is read from the Light component each
    /// frame, so the revealed region always matches the visible light cone —
    /// tune the Light in the inspector and the reveal follows for free.
    /// </summary>
    public class BlacklightRevealDriver : MonoBehaviour
    {
        [Tooltip("The spot Light on this held prefab. Defines the reveal cone.")]
        [SerializeField] private Light _spot;

        private static readonly int PosId       = Shader.PropertyToID("_BlacklightPos");
        private static readonly int DirId       = Shader.PropertyToID("_BlacklightDir");
        private static readonly int RangeId     = Shader.PropertyToID("_BlacklightRange");
        private static readonly int CosOuterId  = Shader.PropertyToID("_BlacklightCosOuter");
        private static readonly int CosInnerId  = Shader.PropertyToID("_BlacklightCosInner");
        private static readonly int IntensityId = Shader.PropertyToID("_BlacklightIntensity");

        private BatteryLight _source;

        private void OnEnable()
        {
            var player = GetComponentInParent<Player.Player>();
            if (player == null || _spot == null)
            {
                Debug.LogError("[BlacklightRevealDriver] Missing Player in parents or spot Light reference.", this);
                enabled = false;
                return;
            }

            _source = player.Blacklight;
            _source.Toggled += HandleToggled;
            HandleToggled(_source.IsOn);   // initial sync — spawn while already on
        }

        private void OnDisable()
        {
            if (_source != null)
            {
                _source.Toggled -= HandleToggled;
                _source = null;
            }

            // Safety net: the held prefab is destroyed by HeldItemRig on item swap.
            // Never leave the scene stuck mid-reveal with a stale cone.
            Shader.SetGlobalFloat(IntensityId, 0f);
        }

        private void HandleToggled(bool on)
        {
            Shader.SetGlobalFloat(IntensityId, on ? 1f : 0f);
        }

        // LateUpdate: runs after CameraRig has applied bob/lean offsets, so the
        // cone matches the final rendered orientation of the held light.
        private void LateUpdate()
        {
            if (_source == null || !_source.IsOn) return;

            Transform t = _spot.transform;
            Shader.SetGlobalVector(PosId, t.position);
            Shader.SetGlobalVector(DirId, t.forward);
            Shader.SetGlobalFloat(RangeId, _spot.range);
            Shader.SetGlobalFloat(CosOuterId, Mathf.Cos(_spot.spotAngle      * 0.5f * Mathf.Deg2Rad));
            Shader.SetGlobalFloat(CosInnerId, Mathf.Cos(_spot.innerSpotAngle * 0.5f * Mathf.Deg2Rad));
        }
    }
}
