using WildWasteland.Modules.Presentation;
using BepInEx.Configuration;
using WildWasteland.Core;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>Three angles (vertical swing, horizontal swing, axial twist) of one bone of a per-human seat pose profile.</summary>
    internal class BonePoseConfig
    {
        internal PoseFloat Vertical, Horizontal, Rotation;

        /// <summary>Binds the three angle entries of <paramref name="bone"/> in <c>[Presentation / section]</c> (defaults from <paramref name="oldSection"/>).</summary>
        internal static BonePoseConfig Create(ModuleConfig config, string section, string oldSection, string bone, float vertical, float horizontal, float twist)
        {
            var range = new AcceptableValueRange<float>(-180f, 180f);
            return new BonePoseConfig
            {
                Vertical = PoseFloat.Create(config, section, oldSection, bone + "Vertical", vertical, bone + " vertical angle: positive swings forward/up (degrees)", range),
                Horizontal = PoseFloat.Create(config, section, oldSection, bone + "Horizontal", horizontal, bone + " horizontal angle: positive turns toward the occupant's right (degrees)", range),
                Rotation = PoseFloat.Create(config, section, oldSection, bone + "Rotation", twist, bone + " axial rotation around the limb/joint (degrees)", range)
            };
        }
    }
}
