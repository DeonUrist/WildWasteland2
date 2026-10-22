using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
namespace WildWasteland.Modules.Patrols
{
    // One shared cache for the player car, rather than hierarchy walks for each patrol's sensing tick.
    // WW: the cache is no longer a component added to the player's (saved) car (AGENTS.md 5.10) but a static table keyed by the car's
    // instance id (refreshed every 5 s as before, swept on scene load). The component type stays, empty, so cars saved by Apocapatrol
    // with it still load (Core.SaveTypes maps the old type name here).
    internal sealed class PatrolVisibility : MonoBehaviour
    {
        private sealed class Entry { internal GameObject Car; internal Transform Engine; internal float Horsepower, Refresh; internal Light[] Lights = new Light[0]; }
        private static readonly Dictionary<int, Entry> _cache = new Dictionary<int, Entry>();
        internal static void Reset() { _cache.Clear(); }
        private static Entry Of(GameObject car)
        {
            if (car == null) return null;
            Entry e; int id = car.GetInstanceID();
            if (!_cache.TryGetValue(id, out e))
            {
                if (_cache.Count > 32) { var dead = _cache.Where(p => p.Value.Car == null).Select(p => p.Key).ToList(); foreach (var k in dead) _cache.Remove(k); }
                e = new Entry { Car = car }; _cache[id] = e;
            }
            return e;
        }
        private static void Refresh(Entry e)
        {
            if (Time.unscaledTime < e.Refresh) return;
            e.Refresh = Time.unscaledTime + 5f; e.Horsepower = 0f; e.Engine = null;
            e.Lights = e.Car.GetComponentsInChildren<Light>(true).Where(l => l.type == LightType.Spot || l.transform.GetComponentsInParent<PlayMakerFSM>(true).Any(f => f.FsmName == "Headlight")).ToArray();
            foreach (var t in e.Car.GetComponentsInChildren<Transform>(true))
            {
                int end=t.name.IndexOf("HP",StringComparison.OrdinalIgnoreCase); if(end<=0)continue;
                int start=end-1;while(start>=0&&(char.IsDigit(t.name[start])||t.name[start]=='.'))start--;
                if(float.TryParse(t.name.Substring(start+1,end-start-1),NumberStyles.Float,CultureInfo.InvariantCulture,out var hp)&&hp>0f)
                { e.Horsepower=hp;e.Engine=t;break; }
            }
        }
        internal static bool LightsOn(GameObject car)
        {
            var cam=Core.GameRefs.MainCamera;var torch=cam!=null?cam.transform.Find("Flashlight"):null;if(torch!=null&&torch.gameObject.activeInHierarchy)return true;
            var cache=Of(car);if(cache==null)return false;Refresh(cache);
            foreach(var l in cache.Lights)if(l!=null&&l.enabled&&l.gameObject.activeInHierarchy&&l.intensity>0f)return true;
            return false;
        }
        internal static float EngineDistance(GameObject car)
        {if(car==null||!Nwh.EngineRunning(car))return 0f;var cache=Of(car);Refresh(cache);return cache.Engine!=null&&cache.Engine.IsChildOf(car.transform)?TrafficRules.EngineRange(cache.Horsepower):0f;}
    }
}
