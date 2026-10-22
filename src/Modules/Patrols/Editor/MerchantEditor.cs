using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WildWasteland.Modules.Patrols
{
    // The merchant object editor (absorbed debug MerchantSpotter 1.0.2 plugin, ex ApocapatrolMerchantSpotter.MerchantEditor): the numpad
    // moves / rotates the selected shop object (Box / Board / Names / Prices / Sale items) of the nearest merchant bus; 5 saves the layout
    // into MerchantLayouts.json (shared per template across shop types), the bus is held still while editing.
    // WW: a component on the module's runner object; Tick / Gui are scheduler jobs. On/off = [Patrols / Debug] Merchant item editor (F7
    // "Merchant item placement"), the toggle key = [Keys] Merchant item editor (None by default).
    internal sealed class MerchantEditor : MonoBehaviour
    {
        private GameObject _car,_marker,_forward;
        private int _slot;private string _kind="Box",_status="";private float _next,_step=.05f;
        private MerchantObject[] _objects=new MerchantObject[0];
        private static bool Press(Key key)=>PatrolsModule.Pressed(key);
        private static bool Held(Key key){var kb=Keyboard.current;if(kb==null||key==Key.None)return false;try{return kb[key].isPressed;}catch(Exception){return false;}}
        private MerchantObject Current=>_objects.Length==0?null:_objects[Mathf.Clamp(_slot,0,_objects.Length-1)];
        internal static bool Enabled=>PatrolsModule.MerchantEditorEnabled!=null&&PatrolsModule.MerchantEditorEnabled.Value;
        private static string KeyText=>PatrolsModule.MerchantEditorKey!=null&&PatrolsModule.MerchantEditorKey.Value!=Key.None?PatrolsModule.MerchantEditorKey.Value.ToString():"F7 page";

        // ---- F7 "Merchant item placement" page
        internal GameObject Car=>_car;
        internal string Kind=>_kind;
        internal string Status=>_status;
        internal float StepSize=>_step;
        internal string Selected{get{var c=Current;return c!=null&&_car!=null&&c.Transform!=null?c.Label+"  "+_car.transform.InverseTransformPoint(c.Transform.position).ToString("F3"):"";}}
        internal void NextKind(){if(_car==null)return;var kinds=MerchantApi.Objects(_car).Select(o=>o.Kind).Distinct().ToArray();if(kinds.Length==0)return;int i=Array.IndexOf(kinds,_kind);_kind=kinds[(i+1)%kinds.Length];_slot=0;_next=0;}
        internal void NextObject(){if(_objects.Length>0)_slot=(_slot+1)%_objects.Length;}
        internal void SaveLayout(){if(_car==null)return;try{_status=MerchantApi.Save(_car);Core.Log.Always("[Patrols] "+_status);}catch(Exception e){_status="Save failed: "+e.Message;Core.Log.Error("[Patrols] Merchant editor save",e);}}
        internal void Freeze(){if(_car!=null){MerchantApi.Freeze(_car);_status="Bus held still";}}

        // WW: was Update (Update job)
        internal void Tick()
        {
            if(!Enabled){Hide();return;}
            if(Time.unscaledTime>=_next)
            {
                _next=Time.unscaledTime+.5f;var cam=Core.GameRefs.MainCamera;
                var nearest=cam!=null?MerchantApi.Cars().Where(c=>c!=null&&(c.transform.position-cam.transform.position).sqrMagnitude<6400f).OrderBy(c=>(c.transform.position-cam.transform.position).sqrMagnitude).FirstOrDefault():null;
                if(nearest!=_car){_car=nearest;_slot=0;_kind="Box";}
                if(_car!=null){var all=MerchantApi.Objects(_car);if(!all.Any(o=>o.Kind==_kind&&o.Transform!=null)){var first=all.FirstOrDefault(o=>o.Transform!=null);if(first!=null){_kind=first.Kind;_slot=0;}}}
                _objects=MerchantApi.Objects(_car).Where(o=>o.Kind==_kind&&o.Transform!=null).ToArray();
            }
            if(_car==null||Current==null){Hide();return;}MerchantApi.Freeze(_car);
            if(Time.timeScale<=0f)return;   // WW: no numpad edits while F7 (or another editor) pauses the game
            if(Press(Key.NumpadDivide)){NextKind();return;}
            if(Press(Key.Numpad0))NextObject();
            if(Press(Key.NumpadPlus))_step=Mathf.Min(1f,_step*2f);if(Press(Key.NumpadMinus))_step=Mathf.Max(.005f,_step/2f);
            var node=Current.Transform;float step=_step*((Held(Key.LeftShift)||Held(Key.RightShift))?5f:1f);bool rotate=Held(Key.LeftCtrl)||Held(Key.RightCtrl);
            var delta=Vector3.zero;
            if(Press(Key.Numpad8))delta.z+=step;if(Press(Key.Numpad2))delta.z-=step;
            if(Press(Key.Numpad6))delta.x+=step;if(Press(Key.Numpad4))delta.x-=step;
            if(Press(Key.Numpad9))delta.y+=step;if(Press(Key.Numpad7))delta.y-=step;
            if(rotate)node.Rotate(new Vector3(delta.y,delta.x,delta.z)*(2f/_step),Space.Self);
            else node.position+=_car.transform.TransformDirection(delta);
            if(Press(Key.Numpad5))SaveLayout();
            Mark(ref _marker,node.position,new Color(1f,.8f,.1f),.13f);Mark(ref _forward,node.position+node.forward*.4f,Color.green,.08f);
        }
        private static void Mark(ref GameObject marker,Vector3 point,Color color,float size)
        {
            if(marker==null){marker=GameObject.CreatePrimitive(PrimitiveType.Sphere);marker.name="MerchantSpotter.Marker";marker.hideFlags=HideFlags.HideAndDontSave;Destroy(marker.GetComponent<Collider>());marker.GetComponent<Renderer>().material.color=color;}
            marker.SetActive(true);marker.transform.position=point;marker.transform.localScale=Vector3.one*size;
        }
        private void Hide(){if(_marker!=null)_marker.SetActive(false);if(_forward!=null)_forward.SetActive(false);}
        // WW: was OnGUI (Gui job)
        internal void Gui()
        {
            if(!Enabled||Event.current.type!=EventType.Repaint)return;
            GUI.depth=-1100;
            string selected=Current!=null&&_car!=null&&Current.Transform!=null?Current.Label+"  "+_car.transform.InverseTransformPoint(Current.Transform.position).ToString("F3"):_car!=null?"Merchant bus found; waiting for shop objects":"No merchant bus found within 80 m. Spawn it from Merchants, not as an ordinary vehicle.";
            GUI.Box(new Rect(15,15,850,110),"Merchant object editor: "+selected+"\n/ = object type   0 = next object   5 = save   "+KeyText+" = toggle\n8/2 forward/back   4/6 sideways   7/9 down/up   Shift = x5   +/- = step ("+_step.ToString("0.000")+" m)\nCtrl + 4/6 yaw   Ctrl + 7/9 pitch   Ctrl + 8/2 roll   "+_status);
        }
        private void OnDestroy(){foreach(var m in new[]{_marker,_forward})if(m!=null){Destroy(m.GetComponent<Renderer>().material);Destroy(m);}}
    }
}
