using System.Collections.Generic;
using SpookyGame.Core;
using UnityEngine;

namespace SpookyGame.Player
{
    /// <summary>
    /// Cinematic flashlight instability, modelled as a failing bulb: a supply level
    /// stepped through authored fault segments and a filament that chases it. Cuts snap
    /// hard (a break in current is instant); only the recovery glow is smoothed. Fault
    /// timing is heavy-tailed rather than uniform - mostly near-instant events with the
    /// occasional long one - because evenly-spaced blinks read as a metronome.
    ///
    /// Every RollInterval seconds it rolls against FlickerChance; a living hostile inside
    /// VillainDetectionRadius multiplies the odds (2x by default) and biases the roll
    /// toward the nastier failures. While sputtering, the beam bleeds warm, loses throw
    /// and its cone narrows to half.
    ///
    /// This component owns the light's intensity, colour, range and spot angle while
    /// enabled. Muzzle flashes compose through <see cref="Pulse"/> instead of writing
    /// intensity directly. Lives on the held flashlight next to <see cref="HeldLightVisual"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlashlightFlicker : MonoBehaviour
    {
        [Tooltip("Optional. Found in children if left empty.")]
        [SerializeField] private Light _light;

        [Header("Odds")]
        [Tooltip("Seconds between flicker rolls.")]
        [SerializeField, Min(0.05f)] private float _rollInterval = 1.5f;
        [Tooltip("Chance per roll that the flashlight falters, with no villain around.")]
        [SerializeField, Range(0f, 1f)] private float _flickerChance = 0.08f;
        [Tooltip("Chance is multiplied by this while a villain is nearby, which also biases failures toward the severe ones.")]
        [SerializeField, Min(1f)] private float _villainChanceMultiplier = 2f;

        [Header("Villain proximity")]
        [SerializeField, Min(0f)] private float _villainDetectionRadius = 12f;
        [Tooltip("Layers hostiles live on. Auto-defaults to the enemy layer if left as Nothing.")]
        [SerializeField] private LayerMask _villainMask;

        [Header("Bulb response")]
        [Tooltip("How fast the filament recovers when current returns. Cuts are instant regardless.")]
        [SerializeField, Min(1f)] private float _reheatRate = 22f;
        [Tooltip("How fast partial sags (not hard cuts) pull the filament down.")]
        [SerializeField, Min(1f)] private float _sagRate = 70f;

        [Header("Idle instability")]
        [Tooltip("Depth of the constant, subtle wander in the beam (0 = perfectly steady).")]
        [SerializeField, Range(0f, 0.3f)] private float _idleNoiseDepth = 0.05f;
        [Tooltip("Depth of the noise riding on top of an active failure.")]
        [SerializeField, Range(0f, 0.6f)] private float _faultNoiseDepth = 0.3f;
        [SerializeField, Min(0.1f)] private float _noiseSpeed = 11f;

        [Header("Dying-bulb look")]
        [Tooltip("Colour the beam bleeds toward as the filament cools.")]
        [SerializeField] private Color _coolingColor = new Color(1f, 0.55f, 0.22f, 1f);
        [Tooltip("Spot angle at full dim, as a fraction of the authored angle. 0.5 = cone halves during a sputter.")]
        [SerializeField, Range(0.3f, 1f)] private float _dimSpotAngleScale = 0.5f;
        [Tooltip("Range at full dim, as a fraction of the authored range.")]
        [SerializeField, Range(0.1f, 1f)] private float _dimRangeScale = 0.6f;

        // Enemy prefabs sit on layer 6, which is unnamed in the project settings.
        private const int DefaultEnemyLayer = 6;

        /// <summary>
        /// One step of the supply feeding the bulb. Hard segments snap the filament down
        /// on entry (a broken contact has no ramp); soft ones let it sag.
        /// </summary>
        private struct Segment
        {
            public float Level;
            public float Duration;
            public bool Noisy;
            public bool Hard;

            public Segment(float level, float duration, bool noisy, bool hard)
            {
                Level = level;
                Duration = duration;
                Noisy = noisy;
                Hard = hard;
            }
        }

        private readonly Collider[] _overlapBuffer = new Collider[8];
        private readonly Queue<Segment> _segments = new Queue<Segment>();

        private BatteryLight _source;
        private Segment _current;
        private float _segmentTimer;
        private float _rollTimer;

        private float _supply = 1f;     // where the electrics are
        private float _emission = 1f;   // where the filament actually is
        private float _noiseSeed;

        private float _pulseMultiplier = 1f;
        private float _pulseUntil;

        private float _baseIntensity;
        private Color _baseColor;
        private float _baseSpotAngle;
        private float _baseRange;

        private void Awake()
        {
            if (_light == null)
                _light = GetComponentInChildren<Light>(true);
            if (_villainMask == 0)
                _villainMask = 1 << DefaultEnemyLayer;

            if (_light != null)
            {
                _baseIntensity = _light.intensity;
                _baseColor = _light.color;
                _baseSpotAngle = _light.spotAngle;
                _baseRange = _light.range;
            }

            _noiseSeed = Random.value * 1000f;
        }

        private void OnEnable()
        {
            Player player = GetComponentInParent<Player>();
            _source = player != null ? player.Flashlight : null;
            _rollTimer = _rollInterval;
            ResetToSteady();
        }

        private void OnDisable()
        {
            ResetToSteady();
            RestoreLight();
            _source = null;
        }

        /// <summary>
        /// Momentarily overdrives the beam (muzzle flash). Multiplies whatever the bulb is
        /// currently managing, so a shot fired mid-failure stays dim and sickly.
        /// </summary>
        public void Pulse(float multiplier, float seconds)
        {
            _pulseMultiplier = Mathf.Max(1f, multiplier);
            _pulseUntil = Time.time + Mathf.Max(0f, seconds);
        }

        /// <summary>Forces a failure now, for scripted scares.</summary>
        public void TriggerFlicker() => BuildFault(IsVillainNearby());

        private void LateUpdate()
        {
            if (_light == null)
                return;

            bool lit = _source != null && _source.IsOn;
            if (!lit)
            {
                // Nothing to drive while the light is off; be ready for the next switch-on.
                if (_segments.Count > 0 || _supply < 1f)
                    ResetToSteady();

                // A shot still pops the bulb with the light switched off, as it did before.
                if (Time.time < _pulseUntil)
                {
                    _light.intensity = _baseIntensity * _pulseMultiplier;
                    _light.color = _baseColor;
                    _light.spotAngle = _baseSpotAngle;
                    _light.range = _baseRange;
                    _light.enabled = true;
                }
                else
                {
                    RestoreLight();
                }
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            AdvanceFault(dt);

            // Recovery glow is smoothed; sags pull down much faster. Hard cuts already
            // snapped in AdvanceFault - that frame-level snap is what sells the sputter.
            float rate = _supply < _emission ? _sagRate : _reheatRate;
            _emission = Mathf.Lerp(_emission, _supply, 1f - Mathf.Exp(-rate * dt));

            // Idle wander always rides the beam; a fault segment authored as unstable
            // swaps in the deeper, uglier noise.
            bool faulting = _segments.Count > 0 || _segmentTimer > 0f;
            float noiseDepth = faulting && _current.Noisy ? _faultNoiseDepth : _idleNoiseDepth;
            float noise = Mathf.PerlinNoise(_noiseSeed, Time.time * _noiseSpeed) * 2f - 1f;
            float factor = Mathf.Clamp01(_emission * (1f + noise * noiseDepth));

            ApplyToLight(factor);
            RollForFault(dt);
        }

        private void RollForFault(float dt)
        {
            if (_segments.Count > 0 || _segmentTimer > 0f)
                return;

            _rollTimer -= dt;
            if (_rollTimer > 0f)
                return;

            _rollTimer = _rollInterval;
            bool villainNear = IsVillainNearby();
            float chance = _flickerChance * (villainNear ? _villainChanceMultiplier : 1f);
            if (Random.value < chance)
                BuildFault(villainNear);
        }

        private void AdvanceFault(float dt)
        {
            if (_segmentTimer > 0f)
            {
                _segmentTimer -= dt;
                if (_segmentTimer > 0f)
                    return;
            }

            if (_segments.Count == 0)
            {
                _supply = 1f;
                return;
            }

            _current = _segments.Dequeue();
            _supply = _current.Level;
            _segmentTimer = _current.Duration;

            // A broken contact does not ramp - the filament is simply cut.
            if (_current.Hard && _current.Level < _emission)
                _emission = _current.Level;
        }

        /// <summary>
        /// Heavy-tailed duration: clusters near min with the occasional straggler out
        /// toward max. Uniform ranges read as a metronome; faults never do.
        /// </summary>
        private static float FaultSeconds(float min, float mean, float max)
        {
            float sample = min - (mean - min) * Mathf.Log(1f - Random.value * 0.98f);
            return Mathf.Min(sample, max);
        }

        /// <summary>
        /// Queues one of three failures. Near a villain the roll leans toward the worse
        /// ones, so a stalked player gets brownouts and blackouts rather than stutters.
        /// </summary>
        private void BuildFault(bool villainNear)
        {
            _segments.Clear();
            float roll = Random.value;
            float severe = villainNear ? 0.35f : 0.6f;   // below this = stutter
            float worst = villainNear ? 0.75f : 0.9f;    // above this = blackout

            if (roll < severe)
                BuildStutter();
            else if (roll < worst)
                BuildBrownout(villainNear);
            else
                BuildBlackout(villainNear);
        }

        /// <summary>
        /// Loose contact: a rapid, irregular burst of hard cuts. Some relights barely
        /// happen before the next cut lands (the double-hit), some hold a beat.
        /// </summary>
        private void BuildStutter()
        {
            int cuts = Random.Range(3, 8);
            for (int i = 0; i < cuts; i++)
            {
                _segments.Enqueue(new Segment(Random.Range(0f, 0.15f),
                    FaultSeconds(0.015f, 0.035f, 0.1f), false, true));
                _segments.Enqueue(new Segment(Random.Range(0.6f, 1f),
                    FaultSeconds(0.02f, 0.06f, 0.28f), true, false));

                // Double-hit: a second cut arrives before the relight registers.
                if (Random.value < 0.35f)
                {
                    _segments.Enqueue(new Segment(Random.Range(0f, 0.1f),
                        FaultSeconds(0.015f, 0.025f, 0.05f), false, true));
                    _segments.Enqueue(new Segment(Random.Range(0.7f, 1f),
                        FaultSeconds(0.02f, 0.05f, 0.15f), true, false));
                }
            }
            _segments.Enqueue(new Segment(1f, FaultSeconds(0.08f, 0.15f, 0.3f), true, false));
        }

        /// <summary>
        /// Failing cells: the beam sags and wallows, with hard micro-dips punched through
        /// it so the sag itself never feels smooth.
        /// </summary>
        private void BuildBrownout(bool villainNear)
        {
            float floor = villainNear ? Random.Range(0.14f, 0.32f) : Random.Range(0.28f, 0.5f);

            _segments.Enqueue(new Segment(floor, FaultSeconds(0.15f, 0.3f, 0.6f), true, false));
            int dips = Random.Range(1, 4);
            for (int i = 0; i < dips; i++)
            {
                _segments.Enqueue(new Segment(Random.Range(0f, floor * 0.5f),
                    FaultSeconds(0.02f, 0.04f, 0.09f), false, true));
                _segments.Enqueue(new Segment(floor * Random.Range(0.8f, 1.3f),
                    FaultSeconds(0.08f, 0.2f, 0.5f), true, false));
            }
            _segments.Enqueue(new Segment(1f, FaultSeconds(0.2f, 0.4f, 0.7f), true, false));
        }

        /// <summary>Full cut, then the bulb flutters trying to catch, fails, and finally holds.</summary>
        private void BuildBlackout(bool villainNear)
        {
            _segments.Enqueue(new Segment(0f,
                FaultSeconds(villainNear ? 0.25f : 0.15f, villainNear ? 0.45f : 0.3f, 0.8f), false, true));

            int attempts = Random.Range(2, 5);
            for (int i = 0; i < attempts; i++)
            {
                _segments.Enqueue(new Segment(Random.Range(0.3f, 0.8f),
                    FaultSeconds(0.025f, 0.05f, 0.12f), true, false));
                _segments.Enqueue(new Segment(Random.Range(0f, 0.05f),
                    FaultSeconds(0.03f, 0.08f, 0.25f), false, true));
            }

            // The final catch flutters once more before it holds.
            _segments.Enqueue(new Segment(Random.Range(0.7f, 1f), FaultSeconds(0.03f, 0.05f, 0.1f), true, false));
            _segments.Enqueue(new Segment(Random.Range(0.1f, 0.3f), FaultSeconds(0.02f, 0.03f, 0.06f), false, true));
            _segments.Enqueue(new Segment(1f, FaultSeconds(0.25f, 0.5f, 0.8f), true, false));
        }

        private void ApplyToLight(float factor)
        {
            float pulse = Time.time < _pulseUntil ? _pulseMultiplier : 1f;

            _light.intensity = _baseIntensity * factor * pulse;
            // A cooling filament loses its blue end, its throw and its spread before it
            // loses everything - the cone visibly collapses toward half width.
            _light.color = Color.Lerp(_coolingColor, _baseColor, Mathf.Clamp01(factor * 1.35f));
            _light.spotAngle = Mathf.Lerp(_baseSpotAngle * _dimSpotAngleScale, _baseSpotAngle, factor);
            _light.range = Mathf.Lerp(_baseRange * _dimRangeScale, _baseRange, factor);
            _light.enabled = true;
        }

        private void ResetToSteady()
        {
            _segments.Clear();
            _segmentTimer = 0f;
            _current = new Segment(1f, 0f, false, false);
            _supply = 1f;
            _emission = 1f;
        }

        /// <summary>Any living hostile inside the radius counts.</summary>
        private bool IsVillainNearby()
        {
            if (_villainDetectionRadius <= 0f)
                return false;

            int count = Physics.OverlapSphereNonAlloc(transform.position, _villainDetectionRadius,
                _overlapBuffer, _villainMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                ActorHealth health = _overlapBuffer[i].GetComponentInParent<ActorHealth>();
                if (health != null && !health.IsDead)
                    return true;
            }
            return false;
        }

        private void RestoreLight()
        {
            if (_light == null)
                return;

            _light.intensity = _baseIntensity;
            _light.color = _baseColor;
            _light.spotAngle = _baseSpotAngle;
            _light.range = _baseRange;
            _light.enabled = _source != null && _source.IsOn;
        }
    }
}