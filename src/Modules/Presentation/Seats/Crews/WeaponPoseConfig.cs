using WildWasteland.Modules.Presentation;
using BepInEx.Configuration;
using WildWasteland.Core;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>Held-weapon offset (hand-local position and rotation) of a ranged human's seat pose profile.</summary>
    internal class WeaponPoseConfig
    {
        internal PoseFloat PositionX, PositionY, PositionZ;
        internal PoseFloat RotationX, RotationY, RotationZ;

        /// <summary>Binds the six weapon entries of a profile section; <paramref name="d"/> holds the human's defaults (indices 18..23).</summary>
        internal static WeaponPoseConfig Create(ModuleConfig config, string section, string oldSection, float[] d)
        {
            var positionRange = new AcceptableValueRange<float>(-2f, 2f);
            var rotationRange = new AcceptableValueRange<float>(-180f, 180f);
            return new WeaponPoseConfig
            {
                PositionX = PoseFloat.Create(config, section, oldSection, "WeaponPositionX", d[18], "Weapon local position along the hand's X axis (m)", positionRange),
                PositionY = PoseFloat.Create(config, section, oldSection, "WeaponPositionY", d[19], "Weapon local position along the hand's Y axis (m)", positionRange),
                PositionZ = PoseFloat.Create(config, section, oldSection, "WeaponPositionZ", d[20], "Weapon local position along the hand's Z axis (m)", positionRange),
                RotationX = PoseFloat.Create(config, section, oldSection, "WeaponRotationX", d[21], "Weapon local rotation around its X axis (degrees)", rotationRange),
                RotationY = PoseFloat.Create(config, section, oldSection, "WeaponRotationY", d[22], "Weapon local rotation around its Y axis (degrees)", rotationRange),
                RotationZ = PoseFloat.Create(config, section, oldSection, "WeaponRotationZ", d[23], "Weapon local rotation around its Z axis (degrees)", rotationRange)
            };
        }
    }
}
