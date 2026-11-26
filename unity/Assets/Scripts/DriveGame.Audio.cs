using System.Collections.Generic;
using LToLocal.Rules;
using UnityEngine;

namespace LToLocal.Game
{
    /// <summary>
    /// Sounds and the coach's voice (content/audio, licences in content/audio/LICENCES.md): indicator ticks in time
    /// with the lamps, a chime for faults and good moves, the coach reading the fault, and a quiet town ambience.
    /// Both can be switched off in Settings. The engine blends three free CC0 loops (idle, low, mid) by speed and
    /// raises their pitch as the car speeds up.
    /// </summary>
    public sealed partial class DriveGame
    {
        public const string SoundKey = "settings_sound", VoiceKey = "settings_voice";
        public static bool SoundOn { get => PlayerPrefs.GetInt(SoundKey, 1) == 1; set { PlayerPrefs.SetInt(SoundKey, value ? 1 : 0); PlayerPrefs.Save(); } }
        public static bool VoiceOn { get => PlayerPrefs.GetInt(VoiceKey, 1) == 1; set { PlayerPrefs.SetInt(VoiceKey, value ? 1 : 0); PlayerPrefs.Save(); } }

        private AudioSource _sfx, _voice, _ambience;
        private readonly AudioSource[] _engine = new AudioSource[3];
        private AudioClip _tickOn, _tickOff, _chimeFault, _chimeGood;
        private readonly Dictionary<string, AudioClip> _voiceClips = new Dictionary<string, AudioClip>();
        private bool _lastBlink;
        private float _nextVoiceAt;

        private void SetUpAudio()
        {
            _sfx = gameObject.AddComponent<AudioSource>();
            _voice = gameObject.AddComponent<AudioSource>();
            _ambience = gameObject.AddComponent<AudioSource>();
            foreach (var s in new[] { _sfx, _voice, _ambience }) { s.playOnAwake = false; s.spatialBlend = 0f; }
            _tickOn = Resources.Load<AudioClip>("Content/audio/sfx/indicator_on");
            _tickOff = Resources.Load<AudioClip>("Content/audio/sfx/indicator_off");
            _chimeFault = Resources.Load<AudioClip>("Content/audio/sfx/chime_fault");
            _chimeGood = Resources.Load<AudioClip>("Content/audio/sfx/chime_success");
            string[] engineClips = { "engine_idle", "engine_low", "engine_mid" };
            for (int i = 0; i < 3; i++)
            {
                var clip = Resources.Load<AudioClip>($"Content/audio/sfx/{engineClips[i]}");
                if (clip == null) continue;
                _engine[i] = gameObject.AddComponent<AudioSource>();
                _engine[i].clip = clip;
                _engine[i].loop = true;
                _engine[i].volume = 0f;
                _engine[i].playOnAwake = false;
                _engine[i].spatialBlend = 0f;
                if (SoundOn) _engine[i].Play();
            }
            var amb = Resources.Load<AudioClip>("Content/audio/sfx/ambience_town");
            Debug.Log($"[LToLocal] Audio: tick={(_tickOn != null)} chime={(_chimeFault != null)} ambience={(amb != null)} listeners={FindObjectsByType<AudioListener>().Length} rate={AudioSettings.outputSampleRate} sound={SoundOn} voice={VoiceOn} volume={AudioListener.volume} paused={AudioListener.pause}");
            if (amb != null)
            {
                _ambience.clip = amb;
                _ambience.loop = true;
                _ambience.volume = 0.35f;
                if (SoundOn) _ambience.Play();
            }
        }

        /// <summary>Indicator relay: a tick as the lamp comes on, a softer tock as it goes off.</summary>
        private void IndicatorAudio(bool blink)
        {
            if (_indicator == 0 || !SoundOn || _mode != Mode.Driving) { _lastBlink = blink; return; }
            if (blink != _lastBlink)
            {
                var clip = blink ? _tickOn : _tickOff;
                if (clip != null) _sfx.PlayOneShot(clip, 0.6f);
            }
            _lastBlink = blink;
        }

        private void RuleAudio(RuleEvent e)
        {
            bool good = e.Outcome == RuleOutcome.GoodPractice;
            if (SoundOn)
            {
                var chime = good ? _chimeGood : _chimeFault;
                if (chime != null) _sfx.PlayOneShot(chime, good ? 0.5f : 0.7f);
            }
            if (!VoiceOn || good || Time.time < _nextVoiceAt || _voice.isPlaying) return;
            string key = $"voice.{e.RuleId}.fault";
            if (!_voiceClips.TryGetValue(key, out var clip))
            {
                clip = Resources.Load<AudioClip>($"Content/audio/voice/{key}");
                _voiceClips[key] = clip;
            }
            if (clip == null) return;
            _voice.clip = clip;
            _voice.Play();
            _nextVoiceAt = Time.time + clip.length + 4f; // don't nag
        }

        /// <summary>Crossfades idle → low → mid with speed and throttle, nudging pitch up within each band.</summary>
        private void EngineAudio(float speedMph, float throttle)
        {
            bool on = SoundOn && _mode == Mode.Driving;
            float revs = Mathf.Clamp01(speedMph / 70f) * 0.8f + throttle * 0.2f;  // 0 idle … 1 flat out
            float[] centre = { 0f, 0.4f, 0.85f };
            for (int i = 0; i < 3; i++)
            {
                var src = _engine[i];
                if (src == null) continue;
                float w = Mathf.Clamp01(1f - Mathf.Abs(revs - centre[i]) / 0.45f);
                src.volume = on ? w * 0.35f : 0f;
                src.pitch = 0.85f + Mathf.Clamp(revs - centre[i], -0.3f, 0.45f) * 0.9f + 0.15f;
            }
        }

        private void ApplySoundSetting()
        {
            foreach (var e in _engine)
                if (e != null) { if (SoundOn && !e.isPlaying) e.Play(); if (!SoundOn) e.Stop(); }
            if (_ambience == null) return;
            if (SoundOn && !_ambience.isPlaying && _ambience.clip != null) _ambience.Play();
            if (!SoundOn && _ambience.isPlaying) _ambience.Stop();
        }
    }
}
