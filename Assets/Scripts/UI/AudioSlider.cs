using SpookyGame.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace SpookyGame.UI
{
    /// <summary>
    /// Put on a settings-menu Slider (range 0..1). Writes into GameSettings, which the
    /// AudioManager listens to — the value also persists via PlayerPrefs.
    /// </summary>
    public class AudioSlider : MonoBehaviour
    {
        public enum Channel { Master, Music, Sfx, Ambience }

        [SerializeField] private Slider slider;
        [SerializeField] private Channel channel = Channel.Master;

        private void Start()
        {
            if (!slider)
                slider = GetComponent<Slider>();

            slider.SetValueWithoutNotify(CurrentValue());
            slider.onValueChanged.AddListener(SetValue);
        }

        private float CurrentValue() => channel switch
        {
            Channel.Music    => GameSettings.MusicVolume,
            Channel.Sfx      => GameSettings.SfxVolume,
            Channel.Ambience => GameSettings.AmbienceVolume,
            _                => GameSettings.MasterVolume,
        };

        private void SetValue(float value)
        {
            switch (channel)
            {
                case Channel.Music:    GameSettings.SetMusicVolume(value);    break;
                case Channel.Sfx:      GameSettings.SetSfxVolume(value);      break;
                case Channel.Ambience: GameSettings.SetAmbienceVolume(value); break;
                default:               GameSettings.SetMasterVolume(value);   break;
            }
        }
    }
}
