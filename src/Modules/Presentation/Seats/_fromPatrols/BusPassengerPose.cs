using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WildWasteland.Modules.Patrols
{
    [Serializable] internal sealed class BusPassengerPreset
    {
        // Forward bend, sideways swing, axial twist. Knees bend opposite to thighs.
        public Vector3[] left={new Vector3(85f,-5f,0f),new Vector3(-90f,0f,0f),new Vector3(25f,-5f,0f),new Vector3(25f,0f,0f),Vector3.zero};
        public Vector3[] right={new Vector3(85f,5f,0f),new Vector3(-90f,0f,0f),new Vector3(25f,5f,0f),new Vector3(25f,0f,0f),Vector3.zero};
        public Vector3 seatOffset;
    }

    internal static class BusPassengerPoseStore
    {
        private static BusPassengerPreset _preset;
        internal static BusPassengerPreset Current { get { if(_preset==null)Load();return _preset; } }
        private static string FilePath=>Path.Combine(EditorStore.Root,"BusPassengerPose.json");
        private static void Load()
        {
            _preset=new BusPassengerPreset();
            try
            {
                if(!File.Exists(FilePath))return;
                var saved=JsonUtility.FromJson<BusPassengerPreset>(File.ReadAllText(FilePath));
                if(saved==null||saved.left==null||saved.right==null||saved.left.Length!=5||saved.right.Length!=5)throw new InvalidDataException("Expected five joints per side");
                for(int i=0;i<5;i++){saved.left[i]=Safe(saved.left[i],180f);saved.right[i]=Safe(saved.right[i],180f);}
                saved.seatOffset=Safe(saved.seatOffset,1f);_preset=saved;
            }
            catch(Exception e){Core.Log.Warn("[Patrols] " + "Bus passenger pose: "+e.Message);}
        }
        private static Vector3 Safe(Vector3 value,float limit)
        {
            if(float.IsNaN(value.x)||float.IsInfinity(value.x)||float.IsNaN(value.y)||float.IsInfinity(value.y)||float.IsNaN(value.z)||float.IsInfinity(value.z))throw new InvalidDataException("Invalid joint value");
            return new Vector3(Mathf.Clamp(value.x,-limit,limit),Mathf.Clamp(value.y,-limit,limit),Mathf.Clamp(value.z,-limit,limit));
        }
        internal static void Reset(){_preset=new BusPassengerPreset();}
        internal static string Save()
        {
            try
            {
                Directory.CreateDirectory(EditorStore.Root);string temp=FilePath+".tmp";
                File.WriteAllText(temp,JsonUtility.ToJson(Current,true));
                if(File.Exists(FilePath))File.Replace(temp,FilePath,FilePath+".bak");else File.Move(temp,FilePath);
                return "Saved BusPassengerPose.json (all bus passengers)";
            }
            catch(Exception e){Core.Log.Warn("[Patrols] " + "Bus passenger pose save: "+e.Message);return "Save failed: "+e.Message;}
        }
    }
}
