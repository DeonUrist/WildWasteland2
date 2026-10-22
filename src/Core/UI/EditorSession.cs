using UnityEngine;

namespace WildWasteland.Core.UI
{
    /// <summary>
    /// One modal editor at a time for the whole mod (F7 tool window, the Patrols ledger, the template recorder, ...).
    /// Pauses the game (timeScale 0), frees the cursor and blocks game input through Apocasetter's InputBlocker when present.
    /// Release happens one frame after Close so the click that closed the window does not reach the game.
    /// </summary>
    public sealed class EditorSession
    {
        static EditorSession _owner;
        int _releaseFrame = -1;
        float _time;
        CursorLockMode _lock;
        bool _visible, _blocked;
        bool _pause;
        /// <summary>Whether this editor owns the modal session.</summary>
        public bool Open { get; private set; }
        /// <summary>Whether any editor currently owns the modal session.</summary>
        public static bool Busy { get { return _owner != null; } }
        /// <summary>The active editor session, including its deferred release frame.</summary>
        public static EditorSession Owner { get { return _owner; } }
        /// <summary>Scene cancellation generation for editor caches.</summary>
        public static int Generation { get; private set; }
        /// <summary>Cancels the active editor immediately on a world or scene transition.</summary>
        public static void CancelAll() { Generation++; if (_owner != null) _owner.Close(true); }

        /// <summary>Acquires a pausing session while gameplay is running.</summary>
        public bool Acquire()
        { return Acquire(true); }
        /// <summary>Acquires a session with the requested pause policy while gameplay is running.</summary>
        public bool Acquire(bool pause)
        { return Acquire(pause, false); }
        /// <summary>Acquires the modal input lease, optionally from an already paused menu, preserving its original state.</summary>
        public bool Acquire(bool pause, bool allowPaused)
        {
            if (Busy || (!allowPaused && Time.timeScale <= 0) || ApocasetterBridge.InputBlocked) return false;
            _owner = this; Open = true; _time = Time.timeScale; _lock = Cursor.lockState; _visible = Cursor.visible;
            _pause = pause;
            _blocked = ApocasetterBridge.SetInputBlock(true);
            if (_pause) Time.timeScale = 0; Maintain(); return true;
        }
        /// <summary>Keeps the cursor released and reapplies the session's pause policy.</summary>
        public void Maintain()
        {
            if (!Open) return;
            if (_pause) Time.timeScale = 0; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        /// <summary>(WW) Pause / run the world while the session stays open (input stays blocked, the cursor free).</summary>
        public bool Paused { get { return Open && _pause; } }
        /// <summary>Changes the pause policy without surrendering the input lease.</summary>
        public void SetPause(bool pause)
        {
            if (!Open || pause == _pause) return;
            _pause = pause;
            Time.timeScale = pause ? 0f : (_time > 0f ? _time : 1f);
        }
        /// <summary>Closes now and normally releases input one frame later to consume the closing click.</summary>
        public void Close(bool immediate = false)
        {
            if (!Open && _releaseFrame < 0) return;
            Open = false;
            if (immediate) Release(); else _releaseFrame = Time.frameCount + 1;
        }
        /// <summary>Called every frame by Core (EarlyUpdate).</summary>
        public static void Tick() { if (_owner != null && _owner._releaseFrame >= 0 && Time.frameCount >= _owner._releaseFrame) _owner.Release(); }
        /// <summary>Restores the time, cursor and optional Apocasetter input state captured on acquisition.</summary>
        void Release()
        {
            if (_blocked) ApocasetterBridge.SetInputBlock(false);
            _blocked = false; _releaseFrame = -1; Time.timeScale = _time; Cursor.lockState = _lock; Cursor.visible = _visible; _owner = null;
        }
    }
}
