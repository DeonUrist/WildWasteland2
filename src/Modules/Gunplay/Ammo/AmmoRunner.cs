using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Gunplay;

namespace WildWasteland.Modules.Guns
{
    /// Was a MonoBehaviour on Gunplay.Runner; now the scheduler job "Guns.Ammo" (every frame: the hold/tap state machine needs frames).
    internal static class AmmoRunner
    {
        private static bool _held;
        private static float _pressStart;
        private static bool _consumed;
        internal static Unloader Unloader = new Unloader();

        internal static void ResetAll()
        {
            _held = false; _pressStart = 0f; _consumed = false;
            Unloader.CancelForLoad();
            AmmoSettings.TapFrame = -100;
            ReloadTapHook.Reset();
        }

        internal static void Tick()
        {
            try { PerGunAmmo.Tick(); } catch (Exception e) { PerGunAmmo.Fail(e); }
            BackpackOverflow.Tick();
            if (!AmmoSettings.Enabled.Value) { Unloader.Tick(); return; }

            bool down = AmmoSettings.ReloadHeld();
            float now = Time.unscaledTime;

            if (down && !_held)
            {
                _held = true; _pressStart = now; _consumed = false;
            }
            else if (down && _held && !_consumed && now - _pressStart >= AmmoSettings.HoldSeconds.Value)
            {
                _consumed = true;
                if (Time.timeScale > 0f && !Unloader.Busy) Unloader.TryStart();
            }
            else if (!down && _held)
            {
                _held = false;
                if (!_consumed && !Unloader.Busy) { AmmoSettings.TapFrame = Time.frameCount; AmmoSettings.V("Reload tap"); }
            }

            Unloader.Tick();
        }
    }
}
