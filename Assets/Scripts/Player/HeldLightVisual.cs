using UnityEngine;
using SpookyGame.Core;

namespace SpookyGame.Player
{
    /// <summary>
    /// Binds a Light on a held prefab to the matching BatteryLight state on the
    /// owning Player. Purely a view: BatteryLight remains the single source of
    /// truth for on/off and battery drain.
    /// </summary>
    public class HeldLightVisual : MonoBehaviour
    {
        [SerializeField] private LightKind _kind;
        [SerializeField] private Light _light;

        private BatteryLight _source;

        private void OnEnable()
        {
            var player = GetComponentInParent<Player>();
            if (player == null) return;

            _source = _kind == LightKind.Flashlight ? player.Flashlight : player.Blacklight;
            _source.Toggled += SetLit;
            SetLit(_source.IsOn);   // initial sync — spawn while already toggled on
        }

        private void OnDisable()
        {
            if (_source != null) _source.Toggled -= SetLit;
            _source = null;
        }

        private void SetLit(bool on) => _light.enabled = on;
    }
}
