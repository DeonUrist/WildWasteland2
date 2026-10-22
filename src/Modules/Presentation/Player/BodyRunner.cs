using System;
using UnityEngine;
using WildWasteland.Modules.Presentation;
using WildWasteland.Core;

namespace WildWasteland.Modules.PlayerBody
{
    // The old Apocaplayer Runner MonoBehaviour, as plain static job methods registered by PlayerBodyModule.Init (Update -> Tick,
    // LateUpdate -> LateTick, OnGUI -> Gui, ModApiRunner.LateUpdate -> ModApiTick, Start's end-of-frame
    // coroutine -> EndOfFrameLoop). The old "Enabled off" branch is Shutdown -> TearDown (the jobs are removed by Core).
    internal static partial class BodyRunner
    {
        private static Body _body;
        private static Vector3 _jumpHeadPlayerPosition;
        private static bool _jumpHeadReady;
        /// <summary>Head position of the body during the player's jump, shifted with the player's movement since the last pose (scene space).</summary>
        internal static bool TryPlayerJumpHead(GameObject player, out Vector3 position)
        {
            position = Vector3.zero;
            if (!_jumpHeadReady || player != PlayerState.Player || PlayerState.Dead || _body == null || !_body.Alive || !_body.TryJumpHead(out position)) return false;
            position += player.transform.position - _jumpHeadPlayerPosition;
            return true;
        }
        /// <summary>The player's body, or null while it is not built.</summary>
        internal static Body PlayerBody { get { return _body; } }
        private static float _nextBuild;
        private static string _lastMode = "";
        private static Vector3 _lastVelocity;
        private static Transform _lastCar;
        internal static bool RagdollVisible { get { return _body != null && _body.IsRagdoll; } }
        /// <summary>Camera focus point while dead (the ragdoll).</summary>
        internal static Vector3 DeathFocus { get { return _body.DeathFocus; } }
        /// <summary>Head and feet points of the live body (occlusion cutaway).</summary>
        internal static bool TryOcclusionPoints(out Vector3 head, out Vector3 feet)
        {
            if (_body != null && _body.Alive) return _body.TryOcclusionPoints(out head, out feet);
            head = feet = Vector3.zero; return false;
        }

        /// <summary>Scene load: every player-side cache and the body dropped (crawl, wheel arms, climbing, ladders, NPC traversals, car seat, third person, arms, cave, TAB picture, props).</summary>
        public static void OnSceneLoaded()
        {
            Crawling.Reset();
            WheelArms.Clear();
            ClimbingController.ResetInstance();
            LadderController.ResetInstance();
            NpcTraversal.Clear();
            DestroyBody();
            CarSeat.Detach();
            ThirdPerson.Off();
            Game.Reset();
            Arms.OnSceneLoaded();
            Cave.Reset();
            Props.OnSceneLoaded();
        }

        /// <summary>Init: start from a clean state (also after a runtime off -&gt; on)</summary>
        internal static void ResetAll()
        {
            OnSceneLoaded();
            _nextBuild = 0f; _lastMode = "";
        }

        /// <summary>Shutdown: everything the old "Enabled = false" branch restored, plus the camera / cutaway components</summary>
        internal static void TearDown()
        {
            WheelArms.Clear();
            DestroyBody();
            CarSeat.Detach();
            ThirdPerson.Off();
            Arms.Restore();
            OcclusionCutaway.Stop();
            ThirdPerson.EndOfFrame();
            Core.Log.Main(PresentationModule.Instance, "Disabled: the game's own player is back");
        }

        /// <summary>Male / Female: rebuild the body from the other model; her arms texture and TAB picture only for her</summary>
        public static void CharacterChanged()
        {
            ClimbingController.StopInstance();
            LadderController.StopInstance();
            try
            {
                DestroyBody();
                Model.Reset();
                Arms.Restore();   // the next character's arms (or the game's for Male)
                if (Core.Log.L1) Core.Log.Main(PresentationModule.Instance, "Character: " + CharacterSettings.Character.Value);
            }
            catch (Exception e) { Core.Log.Error(PresentationModule.Instance, "Character switch: " + e); }
        }

        /// <summary>Destroys the body and the cached jump/death references.</summary>
        private static void DestroyBody()
        {
            _jumpHeadReady = false;
            if (_body != null) _body.Destroy();
            _body = null;
            _lastVelocity = Vector3.zero;
            _lastCar = null;
        }

        /// <summary>Job "Presentation.BodyUpdate" (Update): wheel arms, arm skin, third-person tick, weapon pose editor hint/flush; body dropped in menus.</summary>
        internal static void Tick()
        {
            try
            {
                WheelArms.Tick();
                if (!Game.Ready) { OcclusionCutaway.Stop(); if (_body != null) DestroyBody(); return; }   // (2.2.6) the cutaway taken down in menus / loading too
                if (!PlayerState.Dead) { _lastVelocity = PlayerState.Velocity; _lastCar = PlayerState.InCar ? PlayerState.CarRoot : null; }
                if (CharacterSettings.FemaleArms.Value && CharacterSettings.Female) Arms.Tick(); else Arms.Restore();
                ThirdPerson.Tick();
                GunPose.ClearHint();                       // set again in LateUpdate while a weapon is being adjusted
                if (!ThirdPerson.On || PlayerState.InCar) GunPose.Flush();
            }
            catch (Exception e) { Core.Log.Error(PresentationModule.Instance, "Update: " + e); }
        }

        /// <summary>Job "Presentation.BodyLate" (LateUpdate): builds the body (every 2 s while missing) and routes it to the view: dead, car seat (first/third person), on foot (first/third person), arms and equipment.</summary>
        internal static void LateTick()
        {
            try
            {
                if (!Game.Ready) return;
                ThirdPerson.LateCrosshair();
                if (!PlayerState.Dead && _body != null && _body.IsRagdoll) DestroyBody();
                if (_body == null || !_body.Alive)
                {
                    _body = null;
                    if (Time.unscaledTime < _nextBuild) return;
                    _nextBuild = Time.unscaledTime + 2f;
                    _body = Body.Create();
                    if (_body == null) return;
                }

                if (!Crawling.Active) _body.EndCrawl();
                if (PlayerState.Dead)
                {
                    _body.LateDead(_lastVelocity, _lastCar);
                    ThirdPerson.CarShift = false;
                    return;
                }
                bool inCar = PlayerState.InCar;
                bool fpCam = Game.FirstPersonCameraOn;
                ThirdPerson.CarShift = false;
                string mode;
                bool fpArms = false;
                if (inCar)
                {
                    bool replace = CharacterSettings.ReplaceDriver.Value;
                    bool seated = replace && CarSeat.Attach(PlayerState.Player.transform);   // (2.2.7) the game's driver: no seat search every frame
                    if (!seated || !replace)
                    {
                        // no seat to copy (or the player wants the game's driver): no body in the car
                        if (!replace) CarSeat.Detach();
                        _body.SetVisible(false, false);
                        mode = "car (game driver)";
                    }
                    else
                    {
                        bool firstPerson = fpCam && (!ThirdPerson.On || ThirdPerson.Peek);   // binoculars: first person
                        _body.LateCar(PlayerState.Player.transform, firstPerson);
                        if (firstPerson) _body.ShiftFirstPerson(PlayerState.Player.transform.rotation, CarSeatSettings.CarViewBase + new Vector3(CarSeatSettings.FpCarCamX.Value, CarSeatSettings.FpCarCamY.Value, CarSeatSettings.FpCarCamZ.Value));
                        _body.SetMesh(firstPerson, true, Game.DrawnWeapon != "");
                        _body.SetVisible(!firstPerson || CharacterSettings.BodyInCar, firstPerson && CharacterSettings.BodyInCar);
                        ThirdPerson.CarShift = firstPerson && CharacterSettings.BodyInCar && CarSeatSettings.CarCameraForward.Value != 0f;
                        // no body in first person: still her arms on the wheel while nothing is drawn (the body mesh has them when it is shown)
                        fpArms = firstPerson && fpCam && !CharacterSettings.BodyInCar && CharacterSettings.ArmsInCar && Game.DrawnWeapon == "" && !Game.Binoculars && (Game.ToolInHand || !Game.GameArmsShown);
                        mode = firstPerson ? "car, first person" : "car, third person";
                    }
                }
                else
                {
                    CarSeat.Detach();
                    bool third = ThirdPerson.On && !ThirdPerson.Peek;   // binoculars raised: drawn as in first person
                    _body.LateFoot(third ? Body.View.ThirdPerson : Body.View.FirstPerson, Time.deltaTime);
                    if (!third && PlayerState.PlayerCamera != null)
                        _body.ShiftFirstPerson(Quaternion.Euler(0f, PlayerState.PlayerCamera.eulerAngles.y, 0f), new Vector3(CharacterSettings.FpCamX.Value, CharacterSettings.FpCamY.Value, CharacterSettings.FpCamZ.Value));
                    _jumpHeadPlayerPosition = PlayerState.Player.transform.position;
                    _jumpHeadReady = true;
                    _body.SetMesh(!third, false);
                    bool show = third || CharacterSettings.BodyFirstPerson.Value;
                    _body.SetVisible(show && fpCam, !third && show && fpCam);
                    // first person, nothing in hand: the game draws no arms - show the body's own, animated like the rest of her
                    fpArms = !third && fpCam && (Crawling.Active || ModAPI.PlayerTraversalActive
                        || CharacterSettings.EmptyHandArms.Value && !Game.Binoculars && (Game.ToolInHand || !Game.GameArmsShown));
                    mode = third ? "on foot, third person" : "on foot, first person";
                }
                _body.SetFirstPersonArms(fpArms);
                _body.LateEquipment(CharacterSettings.EquipmentOnBody.Value);
                if (!ReferenceEquals(mode, _lastMode)) { _lastMode = mode; if (Core.Log.L2) Core.Log.Deep(PresentationModule.Instance, "View: " + mode); }
            }
            catch (Exception e) { Core.Log.Error(PresentationModule.Instance, "LateUpdate: " + e); _nextBuild = Time.unscaledTime + 5f; }
        }

        /// <summary>Steering-wheel hands while driving with the body in the seat.</summary>
        internal static void WheelTick()
        {
            if (_body != null && _body.Alive && Game.Ready && PlayerState.InCar && !PlayerState.Dead && CharacterSettings.ReplaceDriver.Value && CarSeat.Seat != null)
                _body.LateWheelArms();
        }

        /// <summary>(2.2.0) characters animated through ModAPI, after the Animators (was ModApiRunner.LateUpdate)</summary>
        internal static void ModApiTick()
        {
            ModAPI.LateTick();
        }


    }
}
