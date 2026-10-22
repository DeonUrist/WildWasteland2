using System;
using System.IO;
using UnityEngine;

namespace WildWasteland.Modules.PlayerBody
{
    // A looping humanoid animation baked from a Mixamo FBX into embedded per-bone world poses (tools/ladder-assets/export.py, no Unity
    // editor needed). On first use the source skeleton is rebuilt for a moment, given a runtime humanoid Avatar and read back through
    // HumanPoseHandler as muscle poses; every humanoid rig (the player's body now, NPC bodies later) plays those through its own
    // HumanPoseHandler. The cycle is in place: the climbed height per cycle is Rise, the facing is normalised to the root's +Z.
    internal sealed class PoseClip
    {
        internal readonly string Name;
        internal float Rise { get; private set; }        // metres climbed per cycle in the source
        internal float Depth { get; private set; }       // hips to hands (ladder plane), metres, along the facing
        internal float ToeDepth { get; private set; }    // (v2) hips to the balls of the feet (the rungs), metres, along the facing
        internal float Seconds { get; private set; }     // natural cycle length
        internal bool Ready { get { return _muscles != null; } }
        private float[][] _muscles; private Vector3[] _bodyPos; private Quaternion[] _bodyRot;
        private float[][] _contact;   // (v3) per frame, how planted the left [0] / right [1] foot is, 0..1
        internal bool HasContact { get { return _contact != null; } }
        internal float Contact(float phase, int side)
        {
            if (_contact == null) return 0f;
            var c = _contact[side]; int n = c.Length;
            float x = (phase - Mathf.Floor(phase)) * n;
            int a = Mathf.Min(n - 1, (int)x), b = (a + 1) % n;
            return Mathf.Lerp(c[a], c[b], x - a);
        }

        private PoseClip(string name) { Name = name; }

        // one shared copy per animation (the player's ladders and NPC traversals); a failed load is not retried
        private static readonly System.Collections.Generic.Dictionary<string, PoseClip> _shared = new System.Collections.Generic.Dictionary<string, PoseClip>();
        internal static PoseClip Shared(string name)
        {
            PoseClip c;
            if (_shared.TryGetValue(name, out c)) return c;
            c = name == "ClimbingLadder" ? Load(LadderController.Resource, name) : null;
            _shared[name] = c;
            return c;
        }

        // Mixamo bone -> Unity human bone (HumanTrait.BoneName)
        private static readonly string[] Map = {
            "Hips","Hips", "Spine","Spine", "Spine1","Chest", "Spine2","UpperChest", "Neck","Neck", "Head","Head",
            "LeftShoulder","LeftShoulder", "LeftArm","LeftUpperArm", "LeftForeArm","LeftLowerArm", "LeftHand","LeftHand",
            "RightShoulder","RightShoulder", "RightArm","RightUpperArm", "RightForeArm","RightLowerArm", "RightHand","RightHand",
            "LeftUpLeg","LeftUpperLeg", "LeftLeg","LeftLowerLeg", "LeftFoot","LeftFoot", "LeftToeBase","LeftToes",
            "RightUpLeg","RightUpperLeg", "RightLeg","RightLowerLeg", "RightFoot","RightFoot", "RightToeBase","RightToes" };
        private static readonly string[] Fingers = { "Thumb","Thumb", "Index","Index", "Middle","Middle", "Ring","Ring", "Pinky","Little" };
        private static readonly string[] Phalanx = { "Proximal", "Intermediate", "Distal" };

        internal static PoseClip Load(string resource, string name)
        {
            var clip = new PoseClip(name);
            GameObject root = null; Avatar avatar = null;
            try
            {
                byte[] data;
                using (var s = WildWasteland.Core.DiskResources.Open(resource))   // (WW) runtime assets are files listed in asset-index.tsv
                {
                    if (s == null) throw new InvalidDataException(resource + " missing from asset-index.tsv / the plugin folder");
                    data = new byte[s.Length]; int got = 0; while (got < data.Length) { int r = s.Read(data, got, data.Length - got); if (r <= 0) break; got += r; }
                }
                using (var br = new BinaryReader(new MemoryStream(data)))
                {
                    if (new string(br.ReadChars(4)) != "WWPA") throw new InvalidDataException("not a pose animation");
                    int version = br.ReadInt32();
                    if (version < 1 || version > 3) throw new InvalidDataException("unsupported pose animation version " + version);
                    int bones = br.ReadInt32(), frames = br.ReadInt32(); float fps = br.ReadSingle();
                    clip.Rise = br.ReadSingle(); clip.Depth = br.ReadSingle(); clip.ToeDepth = br.ReadSingle();
                    if (version < 2 || clip.ToeDepth <= 0f) clip.ToeDepth = clip.Depth;
                    clip.Seconds = frames / Mathf.Max(1f, fps);
                    var names = new string[bones]; var parent = new int[bones];
                    for (int i = 0; i < bones; i++)
                    {
                        int len = br.ReadUInt16(); names[i] = System.Text.Encoding.UTF8.GetString(br.ReadBytes(len)); parent[i] = br.ReadInt32();
                        if (parent[i] >= i) throw new InvalidDataException("bones are not parent-first");
                    }
                    root = new GameObject("WW.PoseSource." + name) { hideFlags = HideFlags.HideAndDontSave };
                    var t = new Transform[bones];
                    for (int i = 0; i < bones; i++)
                    { t[i] = new GameObject(names[i]) { hideFlags = HideFlags.HideAndDontSave }.transform; t[i].SetParent(parent[i] < 0 ? root.transform : t[parent[i]], false); }
                    Pose(br, t);   // the rest pose (T-pose, soles at y 0)
                    var hd = new HumanDescription
                    {
                        upperArmTwist = .5f, lowerArmTwist = .5f, upperLegTwist = .5f, lowerLegTwist = .5f,
                        armStretch = .05f, legStretch = .05f, feetSpacing = 0f, hasTranslationDoF = false
                    };
                    var skel = new SkeletonBone[bones + 1];
                    skel[0] = new SkeletonBone { name = root.name, position = Vector3.zero, rotation = Quaternion.identity, scale = Vector3.one };
                    for (int i = 0; i < bones; i++) skel[i + 1] = new SkeletonBone { name = names[i], position = t[i].localPosition, rotation = t[i].localRotation, scale = Vector3.one };
                    hd.skeleton = skel;
                    var human = new System.Collections.Generic.List<HumanBone>();
                    for (int k = 0; k < Map.Length; k += 2) Add(human, names, "mixamorig:" + Map[k], Map[k + 1]);
                    for (int side = 0; side < 2; side++)
                        for (int f = 0; f < Fingers.Length; f += 2)
                            for (int p = 0; p < 3; p++)
                                Add(human, names, "mixamorig:" + (side == 0 ? "Left" : "Right") + "Hand" + Fingers[f] + (p + 1), (side == 0 ? "Left " : "Right ") + Fingers[f + 1] + " " + Phalanx[p]);
                    hd.human = human.ToArray();
                    avatar = AvatarBuilder.BuildHumanAvatar(root, hd);
                    if (avatar == null || !avatar.isValid || !avatar.isHuman) throw new InvalidDataException("the source skeleton gave no valid humanoid avatar");
                    var handler = new HumanPoseHandler(avatar, root.transform);
                    var hp = new HumanPose();
                    clip._muscles = new float[frames][]; clip._bodyPos = new Vector3[frames]; clip._bodyRot = new Quaternion[frames];
                    Vector3 fwd = Vector3.zero;
                    for (int f = 0; f < frames; f++)
                    {
                        Pose(br, t);
                        handler.GetHumanPose(ref hp);
                        clip._muscles[f] = (float[])hp.muscles.Clone(); clip._bodyPos[f] = hp.bodyPosition; clip._bodyRot[f] = hp.bodyRotation;
                        Vector3 v = hp.bodyRotation * Vector3.forward; v.y = 0f; fwd += v.normalized;
                    }
                    handler.Dispose();
                    if (version >= 3)
                    {
                        clip._contact = new float[2][];
                        for (int side = 0; side < 2; side++) { var b = br.ReadBytes(frames); clip._contact[side] = new float[frames]; for (int f = 0; f < frames; f++) clip._contact[side][f] = b[f] / 255f; }
                    }
                    // face the root's +Z (the ladder) whatever the source's own facing convention
                    var turn = Quaternion.Inverse(Quaternion.LookRotation(fwd.sqrMagnitude > 1e-4f ? fwd : Vector3.forward, Vector3.up));
                    for (int f = 0; f < frames; f++) { clip._bodyRot[f] = turn * clip._bodyRot[f]; clip._bodyPos[f] = turn * clip._bodyPos[f]; }
                }
                PlayerBodyModule.Info("Pose animation " + name + " ready: " + clip._muscles.Length + " frames, " + clip.Seconds.ToString("0.00") + " s, " + clip.Rise.ToString("0.00") + " m per cycle");
                return clip;
            }
            catch (Exception e) { PlayerBodyModule.Error("Pose animation " + name + ": " + e.Message); return null; }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (avatar != null) UnityEngine.Object.DestroyImmediate(avatar);
            }
        }

        private static void Add(System.Collections.Generic.List<HumanBone> list, string[] names, string bone, string human)
        {
            if (Array.IndexOf(names, bone) < 0) return;
            var hb = new HumanBone { boneName = bone, humanName = human };
            hb.limit.useDefaultValues = true;
            list.Add(hb);
        }

        // one stored pose: world position + rotation per bone (parents first), relative to the source root
        private static void Pose(BinaryReader br, Transform[] t)
        {
            for (int i = 0; i < t.Length; i++)
            {
                var p = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                var q = new Quaternion(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                t[i].SetPositionAndRotation(p, q);
            }
        }

        // the pose at a cycle position (0..1, wrapping), blended over `weight` onto `pose` (which holds the rig's current pose)
        internal void Blend(float phase, float weight, ref HumanPose pose)
        {
            int n = _muscles.Length;
            float x = (phase - Mathf.Floor(phase)) * n;
            int a = Mathf.Min(n - 1, (int)x), b = (a + 1) % n; float k = x - a;
            var ma = _muscles[a]; var mb = _muscles[b]; var m = pose.muscles;
            weight = Mathf.Clamp01(weight);
            for (int i = 0; i < m.Length && i < ma.Length; i++) m[i] = Mathf.Lerp(m[i], Mathf.Lerp(ma[i], mb[i], k), weight);
            pose.bodyPosition = Vector3.Lerp(pose.bodyPosition, Vector3.Lerp(_bodyPos[a], _bodyPos[b], k), weight);
            pose.bodyRotation = Quaternion.Slerp(pose.bodyRotation, Quaternion.Slerp(_bodyRot[a], _bodyRot[b], k), weight);
        }
    }
}
