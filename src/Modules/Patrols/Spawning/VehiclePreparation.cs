using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Patrols
{
    /// <summary>Resolves Patrols authored definitions before handing mechanical preparation to Vehicles.</summary>
    internal static class VehiclePreparation
    {
        private static bool _pending;
        /// <summary>Copies mechanical values and resolves exact authored prefabs before a sliced preparation starts.</summary>
        internal static IPreparedVehicle Require(CarTemplate t,string bumper,bool trailer)
        {
            var api=ModApi.Get<IVehicleAssembly>(); if(api==null)throw new System.InvalidOperationException("Vehicles assembly provider is unavailable");
            var s=new VehicleStructure { Name=t.Name,Body=t.Body,Wheel=t.Wheel,RearWheel=t.RearWheel,Engine=t.Engine,Radiator=t.Radiator,SteeringWheel=t.SteeringWheel,Exhaust=t.Exhaust,Seat=t.Seat,PassengerSeat=t.PassengerSeat,Parts=CopyParts(t.Parts) };
            s.Prefabs["|"+t.Body]=Prefabs.Exact(t.Body);
            if(t.Parts!=null)foreach(var part in t.Parts)s.Prefabs["|"+part.prefab]=Prefabs.Exact(part.prefab);
            string[] names={t.Wheel,t.RearWheel,t.Engine,t.Radiator,t.SteeringWheel,t.Exhaust,t.Seat,t.PassengerSeat,"Headlight"};
            string[] kinds={"wheel","wheel","engine","radiator","steeringwheel","exhaust","seat","seat","headlight"};
            for(int i=0;i<names.Length;i++)s.Prefabs[kinds[i]+"|"+names[i]]=Prefabs.Find(names[i],kinds[i]);
            s.Prefabs["bumper|"+bumper]=Prefabs.FindAny(bumper);
            return api.Require(s,bumper,trailer);
        }
        /// <summary>Detaches every authored part and pose array before sliced mechanical preparation observes it.</summary>
        private static TemplatePart[] CopyParts(TemplatePart[] parts)
        {
            if(parts==null)return null;var copy=new TemplatePart[parts.Length];for(int i=0;i<parts.Length;i++){var p=parts[i];copy[i]=new TemplatePart {parent=p.parent,hinge=p.hinge,prefab=p.prefab,hingePos=Copy(p.hingePos),hingeRot=Copy(p.hingeRot),pos=Copy(p.pos),rot=Copy(p.rot)};}return copy;
        }
        /// <summary>Detaches authored pose coordinates from mutable editor arrays.</summary>
        private static float[] Copy(float[] values) {return values!=null?(float[])values.Clone():null;}
        /// <summary>Consumes the exact preprocessed dormant instance through its owning provider.</summary>
        internal static GameObject Spawn(IPreparedVehicle entry,Vector3 position,Quaternion rotation) { return ModApi.Get<IVehicleAssembly>().Consume(entry,position,rotation); }
        /// <summary>Warms every available authored structural variant, including trailer variants.</summary>
        internal static void WarmPark() { _pending=true; PublishWarm(); }
        /// <summary>Waits for the owning Vehicles provider before submitting the authored park.</summary>
        internal static void PublishWarm() { if(!_pending||ModApi.Get<IVehicleAssembly>()==null)return;_pending=false;foreach(var t in CarTemplate.Park)Warm(t,false); }
        /// <summary>Queues bumpers independently and recursively queues authored trailers.</summary>
        internal static void Warm(CarTemplate t,bool trailer=false) { if(t==null||!t.Available)return; if(trailer||t.Bumpers==null||t.Bumpers.Length==0)Require(t,"",trailer);else foreach(var bumper in t.Bumpers)Require(t,bumper??"",false);if(t.Trailer!=null)Warm(t.Trailer,true); }
        /// <summary>Resets prepared world structures when the submitting Patrols world ends.</summary>
        internal static void Reset() { var api=ModApi.Get<IVehicleAssembly>();if(api!=null)api.Reset();_pending=true; }
        internal static bool Warming { get { var api=ModApi.Get<IVehicleAssembly>();PublishWarm();return _pending||api!=null&&api.Warming; } }
        /// <summary>Reports only Patrols restoration; Vehicles reports its own preparation progress.</summary>
        internal static LoadingProgress.Work? LoadWork() { return PatrolPersistence.Restoring ? new LoadingProgress.Work(PatrolPersistence.LoadFraction,"restoring saved patrols, crew and AI") : (LoadingProgress.Work?)null; }
    }
}
