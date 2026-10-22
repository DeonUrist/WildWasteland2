using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Vehicles;
namespace WildWasteland.Modules.Vehicles
{
    /// <summary>Native light and brake switches shared by player and traffic control clients.</summary>
    internal static class NativeVehicleSwitches
    {
        /// <summary>Caches a native car handbrake FSM and retry time without repeated hierarchy scans.</summary>
        private sealed class Brake { internal GameObject Owner;internal PlayMakerFSM Fsm;internal float Retry; }
        private static readonly System.Collections.Generic.Dictionary<int,Brake> Brakes=new System.Collections.Generic.Dictionary<int,Brake>();
        private static readonly System.Collections.Generic.List<int> Dead=new System.Collections.Generic.List<int>();
        /// <summary>Reuses the native handbrake reference; missing or replaced levers are retried after one second.</summary>
        private static PlayMakerFSM FindBrake(GameObject car)
        {
            if(car==null)return null;
            int id=car.GetInstanceID();Brake brake;
            if(Brakes.TryGetValue(id,out brake)&&brake.Owner==car && (brake.Fsm!=null||Time.unscaledTime<brake.Retry))return brake.Fsm;
            if(Brakes.Count>64){Dead.Clear();foreach(var entry in Brakes)if(entry.Value.Owner==null)Dead.Add(entry.Key);foreach(int key in Dead)Brakes.Remove(key);}
            brake=new Brake{Owner=car,Retry=Time.unscaledTime+1f};Brakes[id]=brake;
            foreach(var f in car.GetComponentsInChildren<PlayMakerFSM>(true))if(f!=null&&f.gameObject.name=="handbrake"&&f.FsmName=="Handbrake"){brake.Fsm=f;break;}
            return brake.Fsm;
        }
        /// <summary>Releases scene references at the Vehicles lifecycle boundary.</summary>
        internal static void Clear() { Brakes.Clear();Dead.Clear(); }
        /// <summary>Switches a car's headlights through its light switch (the player's click) or straight through every Headlight FSM.</summary>
        internal static void Headlights(GameObject car, bool on, PlayMakerFSM[] snapshot = null)
        {
            if (car == null) return;
            int n = 0;
            foreach (var f in snapshot ?? car.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (f == null || f.Fsm == null || !f.Fsm.Initialized) continue;
                if (f.FsmName == (on ? "LightOff" : "LightOn") && f.enabled && f.gameObject.name == "switch_lights" && f.ActiveStateName == "idle")
                    f.SendEvent("useDoor");
                else if (f.FsmName == "Headlight") { f.SendEvent(on ? "HeadlightsON" : "HeadlightsOFF"); n++; }
            }
            if (Log.L2) Log.Deep(VehiclesModule.Instance, "Lights " + (on ? "on" : "off") + ": " + car.name + " (" + n + " light FSM(s))");
        }

        /// <summary>Moves the car's handbrake lever (its handbrake/Handbrake FSM).</summary>
        internal static void Handbrake(GameObject car, bool on) { Handbrake(car, on, FindBrake(car)); }
        // f = the car's handbrake/Handbrake FSM, cached by the caller (null = none)
        /// <summary>Moves the given handbrake lever FSM through its click states (or NWH input.Handbrake without a lever).</summary>
        internal static void Handbrake(GameObject car, bool on, PlayMakerFSM f)
        {
            if (f == null) { Log.Warn(VehiclesModule.Instance, "No handbrake/Handbrake FSM; setting NWH input.Handbrake only"); Nwh.SetHandbrake(car, on ? 1f : 0f); return; }
            string cur = f.Fsm.Initialized ? f.ActiveStateName : "";
            bool isOn = cur == "HandbrakeOn" || cur == "over" || cur == "Sound 2" || cur == "";
            if (on == isOn) { if (Log.L2) Log.Deep(VehiclesModule.Instance, "Handbrake already " + (on ? "on" : "off") + " (" + cur + ")"); return; }
            f.Fsm.SetState(on ? "Sound 2" : "Sound");
            if (Log.L2) Log.Deep(VehiclesModule.Instance, "Handbrake " + (on ? "on" : "released") + " (was " + cur + ")");
        }

    }
}
