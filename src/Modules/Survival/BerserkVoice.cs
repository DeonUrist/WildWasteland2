using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Survival
{
    /// <summary>One non-looping female cry or the two male cries in order, using the selected player appearance.</summary>
    internal sealed class BerserkVoice
    {
        private static readonly string[] Files = { "female_battlecry.mp3", "male_battlecry1.wav", "male_battlecry2.wav" };
        private readonly IModule _owner;
        private readonly AudioClip[] _clips = new AudioClip[3];
        private readonly Core.Audio.Load[] _loads = new Core.Audio.Load[3];
        private AudioSource _source;
        private int _cue = -1;
        private float _position;
        private bool _playing, _paused;
        internal BerserkVoice(IModule owner) { _owner = owner; }
        internal void Begin()
        {
            if (_cue == -1)
            {
                int choice = ModApi.Get<ICharacterHudData>()?.Choice ?? 1;
                _cue = choice == 0 || choice == 8 || choice == 9 ? 0 : 1;
            }
            if (_cue == 3) return;
            Load(_cue);
            if (_cue == 1) Load(2);
        }
        private void Load(int index)
        {
            if (_loads[index] != null) return;
            _loads[index] = Core.Audio.LoadAsync(Path.Combine(Assets.ModuleDir("Survival"), "Sounds", Files[index]), index == 0 ? AudioType.MPEG : AudioType.WAV, (clip, error) =>
            {
                _clips[index] = clip;
                if (clip != null) clip.hideFlags = HideFlags.HideAndDontSave;
                else Log.Warn(_owner, "Berserk battle cry could not load " + Files[index] + ": " + error);
            }, decompress: true);
        }
        internal void Tick()
        {
            if (_cue < 0 || _cue == 3 || _paused) return;
            if (_playing)
            {
                if (_source != null && _source.isPlaying) return;
                _playing = false; _position = 0; _cue = _cue == 1 ? 2 : 3;
                if (_cue == 3) return;
            }
            Load(_cue);
            var clip = _clips[_cue];
            if (clip == null)
            {
                if (_loads[_cue].Done) { _cue = _cue == 1 ? 2 : 3; _position = 0; }
                return;
            }
            if (_source == null)
            {
                var go = new GameObject("Survival.BerserkVoice"); go.transform.SetParent(Runner.Host.transform, false);
                _source = go.AddComponent<AudioSource>(); _source.playOnAwake = false; _source.loop = false; _source.spatialBlend = 0; _source.volume = 1;
            }
            _source.clip = clip; _source.time = Mathf.Clamp(_position, 0, Mathf.Max(0, clip.length - .01f)); _source.Play(); _playing = true;
            Log.Always(_owner, "Berserk battle cry: " + Files[_cue] + "; once, no loop.");
        }
        internal void Pause(bool paused)
        {
            if (_source != null && _playing && paused != _paused) { if (paused) _source.Pause(); else _source.UnPause(); }
            _paused = paused;
        }
        internal string Record()
        {
            if (_source != null && _playing) _position = _source.time;
            return _cue.ToString(CultureInfo.InvariantCulture) + "\n" + _position.ToString("R", CultureInfo.InvariantCulture);
        }
        internal void Restore(string cue, string position)
        {
            _cue = int.Parse(cue, CultureInfo.InvariantCulture); _position = float.Parse(position, CultureInfo.InvariantCulture);
            if (_cue < -1 || _cue > 3 || float.IsNaN(_position) || float.IsInfinity(_position) || _position < 0) throw new FormatException("Invalid Berserk voice state");
        }
        internal void Stop()
        {
            if (_source != null) { _source.Stop(); _source.clip = null; }
            _cue = -1; _position = 0; _playing = _paused = false;
        }
        internal void Clear()
        {
            Stop();
            for (int i = 0; i < _loads.Length; i++)
            {
                if (_loads[i] != null) _loads[i].Cancel(); _loads[i] = null;
                if (_clips[i] != null) UnityEngine.Object.Destroy(_clips[i]); _clips[i] = null;
            }
            if (_source != null) UnityEngine.Object.Destroy(_source.gameObject); _source = null;
        }
    }
}
