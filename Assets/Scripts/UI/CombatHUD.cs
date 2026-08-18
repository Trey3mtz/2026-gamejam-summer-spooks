using SpookyGame.Core;
using SpookyGame.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpookyGame.UI
{
    /// <summary>Responsive first-person combat HUD built from native UGUI primitives.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Player.Player))]
    public sealed class CombatHUD : MonoBehaviour
    {
        private static readonly Color PanelColor = new Color(0.018f, 0.035f, 0.045f, 0.92f);
        private static readonly Color TrackColor = new Color(0.03f, 0.065f, 0.075f, 0.96f);
        private static readonly Color Cyan = new Color(0.16f, 0.95f, 0.88f, 1f);
        private static readonly Color SoftCyan = new Color(0.68f, 0.94f, 0.93f, 1f);
        private static readonly Color HealthRed = new Color(0.95f, 0.16f, 0.2f, 1f);
        private static readonly Color HealthGhost = new Color(1f, 0.62f, 0.14f, 0.75f);

        private Player.Player _player;
        private ActorHealth _health;
        private FlashlightWeaponState _weapon;
        private Canvas _canvas;
        private RectTransform _healthFill;
        private RectTransform _healthGhost;
        private RectTransform _batteryFill;
        private RectTransform _reloadFill;
        private TextMeshProUGUI _healthValue;
        private TextMeshProUGUI _ammoValue;
        private TextMeshProUGUI _batteryValue;
        private TextMeshProUGUI _weaponStatus;
        private Image _crosshair;
        private Image _damageOverlay;
        private float _shownHealth = 1f;
        private float _statusUntil;
        private float _hitMarkerUntil;
        private float _damageOverlayAlpha;

        private void Start()
        {
            _player = GetComponent<Player.Player>();
            _health = _player.Health;
            _weapon = _player.FlashlightWeapon;
            BuildHud();
            Subscribe();
            RefreshHealth(_health.CurrentHp, _health.MaxHp);
            RefreshWeapon();
            RefreshBattery(_player.Flashlight.BatteryLife);
        }

        private void OnDestroy()
        {
            Unsubscribe();

            if (_canvas != null)
                Destroy(_canvas.gameObject);
        }

        private void Update()
        {
            if (_healthGhost != null)
            {
                _shownHealth = Mathf.MoveTowards(_shownHealth, _health.HealthPercentage,
                    Time.unscaledDeltaTime * 0.35f);
                SetHorizontalFill(_healthGhost, _shownHealth);
            }

            if (_damageOverlay != null)
            {
                _damageOverlayAlpha = Mathf.MoveTowards(_damageOverlayAlpha, 0f,
                    Time.unscaledDeltaTime * 0.5f);
                Color color = _damageOverlay.color;
                color.a = _damageOverlayAlpha;
                _damageOverlay.color = color;
            }

            if (_weapon != null && _reloadFill != null)
            {
                _reloadFill.gameObject.SetActive(_weapon.IsReloading);
                if (_weapon.IsReloading)
                    SetHorizontalFill(_reloadFill, _weapon.ReloadProgress);
            }

            if (_weaponStatus != null && Time.unscaledTime > _statusUntil &&
                (_weapon == null || !_weapon.IsReloading))
                _weaponStatus.text = "R  RELOAD    •    F  LIGHT";

            if (_crosshair != null)
                _crosshair.color = Time.unscaledTime < _hitMarkerUntil ? Color.white : Cyan;
        }

        private void Subscribe()
        {
            _health.HealthChanged += RefreshHealth;
            _health.Damaged += HandlePlayerDamaged;
            _weapon.StateChanged += RefreshWeapon;
            _weapon.ReloadStarted += HandleReloadStarted;
            _weapon.ReloadCompleted += HandleReloadCompleted;
            _weapon.DryFired += HandleDryFire;
            _weapon.PowerBlocked += HandlePowerBlocked;
            _weapon.ShotResolved += HandleShotResolved;
            _player.Flashlight.BatteryChanged += RefreshBattery;
        }

        private void Unsubscribe()
        {
            if (_health != null)
            {
                _health.HealthChanged -= RefreshHealth;
                _health.Damaged -= HandlePlayerDamaged;
            }

            if (_weapon != null)
            {
                _weapon.StateChanged -= RefreshWeapon;
                _weapon.ReloadStarted -= HandleReloadStarted;
                _weapon.ReloadCompleted -= HandleReloadCompleted;
                _weapon.DryFired -= HandleDryFire;
                _weapon.PowerBlocked -= HandlePowerBlocked;
                _weapon.ShotResolved -= HandleShotResolved;
            }

            if (_player != null)
                _player.Flashlight.BatteryChanged -= RefreshBattery;
        }

        private void RefreshHealth(int current, int maximum)
        {
            float normalized = maximum <= 0 ? 0f : (float)current / maximum;
            SetHorizontalFill(_healthFill, normalized);
            if (_healthValue != null)
                _healthValue.text = $"{current:000}  /  {maximum:000}";
        }

        private void RefreshWeapon()
        {
            if (_ammoValue != null)
            {
                _ammoValue.text = $"{_weapon.RoundsInMagazine:00}  <size=58%><color=#83A6A8>/  {_weapon.ReserveAmmo:00}</color></size>";
                _ammoValue.color = _weapon.RoundsInMagazine == 0 ? HealthRed : Color.white;
            }
        }

        private void RefreshBattery(int charge)
        {
            SetHorizontalFill(_batteryFill, charge / 100f);
            if (_batteryValue != null)
                _batteryValue.text = $"POWER  {charge:000}%";
        }

        private void HandlePlayerDamaged(int amount)
        {
            _shownHealth = Mathf.Max(_shownHealth, _health.HealthPercentage + amount / (float)_health.MaxHp);
            _damageOverlayAlpha = Mathf.Clamp(0.08f + amount / 150f, 0.1f, 0.28f);
        }

        private void HandleReloadStarted() => SetStatus("RECHARGING MAGAZINE…", 2f);
        private void HandleReloadCompleted() => SetStatus("MAGAZINE READY", 0.75f);
        private void HandleDryFire() => SetStatus(_weapon.ReserveAmmo > 0 ? "EMPTY — PRESS R" : "NO UV CELLS", 1.2f);
        private void HandlePowerBlocked() => SetStatus("NO POWER — FIND A UV CELL", 1.4f);

        private void HandleShotResolved(bool hitDamageable)
        {
            if (hitDamageable)
                _hitMarkerUntil = Time.unscaledTime + 0.12f;
        }

        private void SetStatus(string message, float seconds)
        {
            if (_weaponStatus != null)
                _weaponStatus.text = message;
            _statusUntil = Time.unscaledTime + seconds;
        }

        private void BuildHud()
        {
            GameObject canvasObject = new GameObject("Combat HUD", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler));
            // A screen-space HUD must not inherit the player's world transform.
            // Keeping this Canvas at the scene root gives its corner anchors the
            // actual Game view rectangle at every resolution and aspect ratio.
            canvasObject.transform.SetParent(null, false);
            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 120;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform gameplayBounds = CreateGameplayBounds(canvasObject.transform);
            BuildHealthPanel(gameplayBounds);
            BuildWeaponPanel(gameplayBounds);
            BuildCrosshair(canvasObject.transform);

            _damageOverlay = CreateImage("Damage Vignette", canvasObject.transform,
                new Color(0.6f, 0f, 0f, 0f));
            Stretch(_damageOverlay.rectTransform, Vector2.zero, Vector2.zero);
            _damageOverlay.raycastTarget = false;
            _damageOverlay.transform.SetAsFirstSibling();
        }

        private static RectTransform CreateGameplayBounds(Transform parent)
        {
            GameObject boundsObject = new GameObject("Gameplay HUD Bounds", typeof(RectTransform),
                typeof(AspectRatioFitter));
            boundsObject.transform.SetParent(parent, false);

            RectTransform rect = boundsObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            AspectRatioFitter fitter = boundsObject.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 4f / 3f;
            return rect;
        }

        private void BuildHealthPanel(Transform parent)
        {
            Image panel = CreateImage("Vitals Panel", parent, PanelColor);
            SetCorner(panel.rectTransform, new Vector2(0f, 0f), new Vector2(24f, 24f), new Vector2(320f, 88f));
            AddOutline(panel, Cyan, new Vector2(2f, 2f));

            CreateText("Vitals Label", panel.transform, "VITALS  //  PLAYER", 14f, SoftCyan,
                TextAlignmentOptions.Left, new Vector2(16f, -10f), new Vector2(210f, 22f), new Vector2(0f, 1f));
            _healthValue = CreateText("Health Value", panel.transform, "100  /  100", 20f, Color.white,
                TextAlignmentOptions.Right, new Vector2(-16f, -9f), new Vector2(160f, 26f), new Vector2(1f, 1f));

            Image track = CreateImage("Health Track", panel.transform, TrackColor);
            SetCorner(track.rectTransform, new Vector2(0f, 0f), new Vector2(16f, 16f), new Vector2(288f, 21f));

            Image ghost = CreateImage("Health Damage Memory", track.transform, HealthGhost);
            Stretch(ghost.rectTransform, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            ghost.rectTransform.pivot = new Vector2(0f, 0.5f);
            _healthGhost = ghost.rectTransform;

            Image fill = CreateImage("Health Fill", track.transform, HealthRed);
            Stretch(fill.rectTransform, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            _healthFill = fill.rectTransform;
        }

        private void BuildWeaponPanel(Transform parent)
        {
            Image panel = CreateImage("Flashlight Panel", parent, PanelColor);
            SetCorner(panel.rectTransform, new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(340f, 112f));
            AddOutline(panel, Cyan, new Vector2(-2f, 2f));

            CreateText("Weapon Label", panel.transform, "UV FLASHLIGHT  //  MK-I", 14f, SoftCyan,
                TextAlignmentOptions.Left, new Vector2(16f, -10f), new Vector2(280f, 22f), new Vector2(0f, 1f));
            _ammoValue = CreateText("Ammo Value", panel.transform, "06  /  24", 30f, Color.white,
                TextAlignmentOptions.Right, new Vector2(-16f, -27f), new Vector2(210f, 40f), new Vector2(1f, 1f));

            Image track = CreateImage("Power Track", panel.transform, TrackColor);
            SetCorner(track.rectTransform, new Vector2(0f, 0f), new Vector2(16f, 37f), new Vector2(308f, 12f));
            Image fill = CreateImage("Power Fill", track.transform, Cyan);
            Stretch(fill.rectTransform, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            _batteryFill = fill.rectTransform;

            Image reload = CreateImage("Reload Progress", track.transform, Color.white);
            Stretch(reload.rectTransform, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            reload.rectTransform.pivot = new Vector2(0f, 0.5f);
            _reloadFill = reload.rectTransform;

            _batteryValue = CreateText("Battery Value", panel.transform, "POWER  100%", 12f, SoftCyan,
                TextAlignmentOptions.Left, new Vector2(16f, 19f), new Vector2(140f, 18f), new Vector2(0f, 0f));
            _weaponStatus = CreateText("Weapon Status", panel.transform, "R  RELOAD    •    F  LIGHT", 11f,
                new Color(0.58f, 0.72f, 0.73f, 1f), TextAlignmentOptions.Right,
                new Vector2(-16f, 19f), new Vector2(190f, 18f), new Vector2(1f, 0f));
        }

        private void BuildCrosshair(Transform parent)
        {
            _crosshair = CreateImage("UV Reticle", parent, Cyan);
            RectTransform rect = _crosshair.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(5f, 5f);
            _crosshair.raycastTarget = false;

            for (int i = 0; i < 4; i++)
            {
                Image tick = CreateImage($"Reticle Tick {i}", _crosshair.transform, Cyan);
                RectTransform tickRect = tick.rectTransform;
                tickRect.anchorMin = tickRect.anchorMax = tickRect.pivot = new Vector2(0.5f, 0.5f);
                bool horizontal = i < 2;
                tickRect.sizeDelta = horizontal ? new Vector2(8f, 2f) : new Vector2(2f, 8f);
                tickRect.anchoredPosition = i switch
                {
                    0 => new Vector2(-11f, 0f),
                    1 => new Vector2(11f, 0f),
                    2 => new Vector2(0f, -11f),
                    _ => new Vector2(0f, 11f)
                };
                tick.raycastTarget = false;
            }
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string text, float size,
            Color color, TextAlignmentOptions alignment, Vector2 position, Vector2 dimensions, Vector2 anchor)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI), typeof(Shadow));
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;

            TextMeshProUGUI label = gameObject.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = size;
            label.fontStyle = FontStyles.Bold;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;

            Shadow shadow = gameObject.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return label;
        }

        private static void AddOutline(Graphic graphic, Color color, Vector2 distance)
        {
            Outline outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(color.r, color.g, color.b, 0.75f);
            outline.effectDistance = distance;
        }

        private static void SetCorner(RectTransform rect, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, Vector2 minimumOffset, Vector2 maximumOffset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = minimumOffset;
            rect.offsetMax = maximumOffset;
        }

        private static void SetHorizontalFill(RectTransform rect, float normalized)
        {
            if (rect == null)
                return;
            rect.localScale = new Vector3(Mathf.Clamp01(normalized), 1f, 1f);
        }
    }
}
