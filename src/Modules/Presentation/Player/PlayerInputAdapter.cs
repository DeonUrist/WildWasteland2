using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Presentation;
namespace WildWasteland.Modules.Presentation
{
    /// <summary>Translates native player input and weapon FSM timing into the common humanoid driver.</summary>
    internal sealed class PlayerInputAdapter
    {
        private readonly ModAPI.Character _driver;
        private string _weapon = "", _attackFor = "", _attackState = "", _throwFor = "", _throwState = "", _kick = "", _jump = "", _grenade = "";
        private float _weaponSince, _fsmReloadSince, _attackTime, _jumpAt = -10f, _reloadStart;
        private bool _reloadFresh, _cocking, _wasReloading, _seated;
        private readonly HashSet<string> _armsShowReload = new HashSet<string>();
        private readonly Dictionary<string,float> _meleeLengths = new Dictionary<string,float>();
        private Animator _meleeArms;
        private int _meleeHash;
        private float _meleeAt;
        private string _meleeFor;
        private bool _measureMelee;
        /// <summary>Changes on a new strike/throw so player-specific alignment can measure that action once.</summary>
        internal int ActionSequence { get; private set; }
        /// <summary>Only quick grenades use the player's native left-hand release-line correction.</summary>
        internal bool ThrowAim { get; private set; }
        /// <summary>Binds one input source to the same driver used by NPCs.</summary>
        internal PlayerInputAdapter(ModAPI.Character driver) { _driver=driver; }
        /// <summary>Resynchronizes transient state after a traversal or crawl ownership change.</summary>
        internal void Resume()
        {
            _driver.Plan.ResetActions(); _driver.Plan.Snap();
            _attackFor=_throwFor="";_measureMelee=false;_cocking=_wasReloading=false;
        }
        /// <summary>Samples native controls once, then evaluates the common character once at the body placement point.</summary>
        internal void Step(float dt, bool firstPerson, bool crouched, bool seated, bool turned, float pitch, float prone)
        {
            if (_seated!=seated) { _driver.Plan.SeatChanged(); _seated=seated; }
            _driver.RefreshTuning();
            var plan=_driver.Plan;plan.SetNow(Time.time);
            string weapon=Game.DrawnWeapon;
            if (weapon!=_weapon)
            {
                _weapon=weapon;_weaponSince=Time.time;_reloadFresh=false;_fsmReloadSince=0f;
                _wasReloading=false;_measureMelee=false;plan.CancelWeaponActions();
                _driver.SetWeapon(null,weapon);GunPose.Flush();
            }
            var kind=Props.KindOf(weapon);bool gun=kind==Props.Kind.Rifle || kind==Props.Kind.Pistol;
            var weaponState = ModApi.Get<IWeaponState>();
            if (weaponState != null && (ModApi.Get<IWeaponCatalog>()?.KindOf(weaponState.WeaponKey) ?? WeaponKind.Other) == WeaponKind.Shotgun && SkillPerks.Has("FastPump")) plan.PumpSpeed *= 1.5f;
            bool live=!PlayerState.Paused,click=live && Input.GetMouseButtonDown(0);
            bool aiming=live && gun && Game.AimDownSights,fire=live && Input.GetMouseButton(0);
            plan.FollowNativeReload=true;plan.NativeReload=gun && ReloadNow(weapon);plan.NativeReloadFraction=-1f;
            if(plan.NativeReload && !_wasReloading) _reloadStart=Time.time;
            _wasReloading=plan.NativeReload;
            if(plan.NativeReload && Game.ReloadsPerRound(weapon))
            {
                string clip=kind==Props.Kind.Pistol?"PistolReload":"RifleReload";
                int phase;float t;bool known;Game.ArmsReloading(weapon,out known);
                if(Game.ArmsReloadPhase(weapon,out phase,out t))plan.NativeReloadFraction=LocoPlan.RoundTime(clip,phase,t);
                else if(known)plan.NativeReloadFraction=LocoPlan.RoundTime(clip,1,0f);
                else
                {
                    float a,b;var c=Anims.Get(clip);
                    if(c!=null && LocoPlan.RoundSegment(clip,out a,out b)) { float len=Mathf.Max(.1f,c.length*(b-a));plan.NativeReloadFraction=a+Mathf.Repeat(Time.time-_reloadStart,len)/len*(b-a); }
                }
            }
            if (plan.NativeReload && !Game.ReloadsPerRound(weapon)) { int phase; float fraction; if (Game.ArmsReloadPhase(weapon,out phase,out fraction)) plan.NativeReloadFraction = fraction; }
            string attack;float attackTime;
            bool cock=kind==Props.Kind.Rifle && Game.Attack(weapon,out attack,out attackTime) && LocoPlan.IsCocking(attack);
            if(cock && !_cocking) _driver.Pump();_cocking=cock;
            if(live)
            {
                if(kind==Props.Kind.Melee || weapon=="hands") WatchAttack(weapon,click);else _attackFor="";
                if(kind==Props.Kind.Throw) WatchThrow(weapon,click);else _throwFor="";
                string grenade=Game.GrenadeState;
                if(grenade=="fire" && _grenade!="fire") StartThrow(false,true);_grenade=grenade;
                string kick=Game.KickState;
                if(!seated && !firstPerson && kick=="on" && _kick!="on") _driver.Kick();_kick=kick;
                string jump=PlayerState.JumpState;
                bool launched=jump=="Falling" && _jump=="Idle" && PlayerState.Velocity.y>2f && Time.time>_jumpAt+.5f;
                if(!seated && (jump=="Idle" && Input.GetButtonDown("Jump") || launched)) { _driver.Jump();_jumpAt=Time.time; }
                _jump=jump;
            }
            MeasureMelee();
            plan.Seated=seated;plan.Preview=seated?null:GunPose.Preview;
            bool pistol=kind==Props.Kind.Pistol;
            plan.SeatedHandsClip=seated && gun ? (pistol ? (plan.NativeReload?"PistolReload":fire?"PistolFire":"PistolIdle")
                : (plan.NativeReload?"RifleReload":fire?"RifleFire":"RifleIdle")) : null;
            if(plan.SeatedHandsClip!=null && Anims.Get(plan.SeatedHandsClip)==null)plan.SeatedHandsClip=pistol?"PistolIdle":"RifleIdle";
            _driver.ManualVelocity=true;_driver.Velocity=seated || plan.Preview!=null?Vector3.zero:PlayerState.Velocity;
            _driver.Crouched=seated?turned:crouched;_driver.Aiming=aiming;_driver.Firing=fire && gun;
            _driver.Airborne=!seated && (PlayerState.JumpState=="Jump" || PlayerState.JumpState=="Falling");
            _driver.TurnInPlace=!seated && plan.Preview==null && CharacterSettings.TurnClipAngle.Value>0f;
            _driver.WalkToStop=!seated && plan.Preview==null && CharacterSettings.WalkToStop.Value;
            _driver.AimPitch=firstPerson || seated?0f:pitch*(1f-prone);_driver.HeadPitch=seated?0f:pitch*(1f-prone);
            if(firstPerson)plan.SuppressKick();
            _driver.StepManual(dt);
        }
        /// <summary>Rejects stale reload FSM state and follows known first-person reload animation.</summary>
        private bool ReloadNow(string weapon)
        {
            if(Time.time-_weaponSince<.3f)return false;
            bool known;bool arms=Game.ArmsReloading(weapon,out known),fsm=Game.IsReloading(weapon);
            if(fsm && arms)_armsShowReload.Add(weapon);
            if(known && _armsShowReload.Contains(weapon))return arms || fsm && Game.ReloadsPerRound(weapon);
            if(!fsm){_reloadFresh=true;_fsmReloadSince=0f;return false;}
            if(!_reloadFresh)return false;
            if(_fsmReloadSince<=0f)_fsmReloadSince=Time.time;
            return Time.time-_fsmReloadSince<8f;
        }
        /// <summary>Translates each native attack transition into one shared strike command.</summary>
        private void WatchAttack(string weapon,bool click)
        {
            string state;float timer;bool has=Game.Attack(weapon,out state,out timer);
            if(weapon!=_attackFor){_attackFor=weapon;_attackState=state;_attackTime=timer;return;}
            bool strike=has?state!="" && state!="on" && (_attackState=="on" || timer>_attackTime+.05f):click;
            _attackState=state;_attackTime=timer;
            if(!strike)return;
            float cycle=Mathf.Clamp((has?timer:.4f)+Time.deltaTime,.2f,1.5f);
            _driver.Strike(cycle);ActionSequence++;
            if(_driver.Plan.MeleePlaying)
            {
                float known;if(_meleeLengths.TryGetValue(weapon,out known))_driver.Plan.RetimeMelee(known);
                _meleeAt=Time.time;_meleeFor=weapon;_measureMelee=false;_meleeArms=null;
                var w=PlayerState.WeaponsParent!=null?PlayerState.WeaponsParent.Find(weapon):null;
                if(w!=null)_meleeArms=w.GetComponentInChildren<Animator>();
                if(_meleeArms!=null && _meleeArms.isActiveAndEnabled){_meleeHash=_meleeArms.GetCurrentAnimatorStateInfo(0).fullPathHash;_measureMelee=true;}
            }
        }
        /// <summary>Fits the common melee clock to the observed native arm swing without selecting a second animation.</summary>
        private void MeasureMelee()
        {
            if(!_measureMelee)return;
            if(_meleeArms==null || Time.time-_meleeAt>=.3f){_measureMelee=false;return;}
            var state=_meleeArms.IsInTransition(0)?_meleeArms.GetNextAnimatorStateInfo(0):_meleeArms.GetCurrentAnimatorStateInfo(0);
            if(state.fullPathHash==_meleeHash || state.length<=.05f)return;
            float seconds=Mathf.Clamp(state.length/Mathf.Max(.1f,Mathf.Abs(_meleeArms.speed*state.speedMultiplier)),.2f,2f);
            _meleeLengths[_meleeFor]=seconds;_driver.Plan.RetimeMelee(seconds);_measureMelee=false;
        }
        /// <summary>Observes the native throwing-weapon release transition.</summary>
        private void WatchThrow(string weapon,bool click)
        {
            string state;float timer;
            if(!Game.Attack(weapon,out state,out timer)){if(click)StartThrow(true,false);_throwFor="";return;}
            if(weapon!=_throwFor){_throwFor=weapon;_throwState=state;return;}
            if(_throwState=="on" && state!="on" && state!="" && state!="InMenu")StartThrow(true,true);
            _throwState=state;
        }
        /// <summary>Supplies native release timing to the common throw clock.</summary>
        private void StartThrow(bool right,bool timed)
        {
            var plan=_driver.Plan;float before=plan.ThrowAt;plan.Throw(right,timed);
            if(plan.ThrowAt!=before){ThrowAim=!right;ActionSequence++;}
        }
    }
}
