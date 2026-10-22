using System;
using System.Collections.Generic;
using WildWasteland.Core;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Per-human seated arm pose (both arms, elbows, hands) plus the held-weapon offset for ranged humans.</summary>
    internal class ArmPoseProfile
    {
        internal BonePoseConfig LeftArm, LeftElbow, LeftHand, RightArm, RightElbow, RightHand;
        internal WeaponPoseConfig Weapon;

        private static readonly Dictionary<string, float[]> Defaults = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "Boltjaw", new[] { 80f,35f,-15.6f, 25f,0f,0f, 0f,0f,0f, -20f,50f,-20f, 125f,0f,25f, 0f,25f,-25f, 0f,0f,0f,-15f,20f,0f } },
            { "Flexa", new[] { 80f,35f,-15.652f, 25f,0f,0f, 0f,0f,0f, 5f,-10f,0f, 120f,0f,0f, 0f,25f,-45f, 0f,0f,0f,-15f,20f,0f } },
            { "Lugnut", new[] { 85f,34f,0f, 0f,0f,0f, 5f,15f,-10f, 80f,60f,20f, 20f,-140f,0f, -40f,50f,0f, 0f,-0.1f,0f,-15f,0f,0f } },
            { "Scrud", new[] { 78f,20f,30f, 25f,0f,0f, 0f,0f,0f, 75f,-20f,-30f, 25f,0f,0f, 0f,0f,0f, 0f,0f,0f,-25f,35f,0f } },
            { "Sprokka", new[] { 105f,10f,0f, 0f,0f,0f, 0f,0f,0f, 15f,-27f,-30f, 25f,0f,0f, 0f,0f,0f, 0f,0f,0.03f,0f,15f,0f } },
            { "Scraffa", new[] { 65f,10f,0f, 25f,0f,0f, 0f,0f,0f, 65f,-10f,0f, 25f,0f,0f, 0f,0f,0f } },
            { "Spanna", new[] { 65f,10f,0f, 25f,0f,0f, 0f,0f,0f, 65f,-10f,0f, 25f,0f,0f, 0f,0f,0f } }
        };

        /// <summary>Binds the profile of <paramref name="human"/> in <c>[Presentation / Seat pose.&lt;Human&gt;]</c> (defaults from <c>[Patrols / Pose.&lt;Human&gt;]</c>).</summary>
        internal static ArmPoseProfile Create(ModuleConfig config, string human, bool ranged)
        {
            string section = SeatPoseSettings.PoseSection + "." + human, old = SeatPoseSettings.OldPose + "." + human;
            float[] d = Defaults[human];
            return new ArmPoseProfile
            {
                LeftArm = BonePoseConfig.Create(config, section, old, "LeftArm", d[0], d[1], d[2]),
                LeftElbow = BonePoseConfig.Create(config, section, old, "LeftElbow", d[3], d[4], d[5]),
                LeftHand = BonePoseConfig.Create(config, section, old, "LeftHand", d[6], d[7], d[8]),
                RightArm = BonePoseConfig.Create(config, section, old, "RightArm", d[9], d[10], d[11]),
                RightElbow = BonePoseConfig.Create(config, section, old, "RightElbow", d[12], d[13], d[14]),
                RightHand = BonePoseConfig.Create(config, section, old, "RightHand", d[15], d[16], d[17]),
                Weapon = ranged ? WeaponPoseConfig.Create(config, section, old, d) : null
            };
        }
    }
}
