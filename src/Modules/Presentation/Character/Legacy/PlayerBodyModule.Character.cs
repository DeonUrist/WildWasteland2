using BepInEx.Configuration;
using WildWasteland.Core;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// PlayerBodyModule part (Presentation, B05): [Obsolete] forwarders of the character entries, which moved to
    /// <see cref="CharacterSettings"/> ([Presentation] sections) in Wave B. Kept for the parts of other packages that still read them
    /// through this class (camera, seats, HUD, compatibility); Wave C deletes them.
    /// </summary>
    public sealed partial class PlayerBodyModule
    {
        /// <summary>Forwarder of <see cref="CharacterSettings.Character"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.Character")]
        internal static ConfigEntry<CharacterChoice> Character { get { return CharacterSettings.Character; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.BodyFirstPerson"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.BodyFirstPerson")]
        internal static ConfigEntry<bool> BodyFirstPerson { get { return CharacterSettings.BodyFirstPerson; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.EmptyHandArms"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.EmptyHandArms")]
        internal static ConfigEntry<bool> EmptyHandArms { get { return CharacterSettings.EmptyHandArms; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.FemaleArms"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.FemaleArms")]
        internal static ConfigEntry<bool> FemaleArms { get { return CharacterSettings.FemaleArms; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.ReplaceDriver"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.ReplaceDriver")]
        internal static ConfigEntry<bool> ReplaceDriver { get { return CharacterSettings.ReplaceDriver; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.EquipmentOnBody"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.EquipmentOnBody")]
        internal static ConfigEntry<bool> EquipmentOnBody { get { return CharacterSettings.EquipmentOnBody; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.PainSounds"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.PainSounds")]
        internal static ConfigEntry<bool> PainSounds { get { return CharacterSettings.PainSounds; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.FirstPersonSwayOn"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.FirstPersonSwayOn")]
        internal static ConfigEntry<bool> FirstPersonSwayOn { get { return CharacterSettings.FirstPersonSwayOn; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.SmoothAimDownSights"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.SmoothAimDownSights")]
        internal static ConfigEntry<bool> SmoothAimDownSights { get { return CharacterSettings.SmoothAimDownSights; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.FullBodyOnlyOnFoot"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.FullBodyOnlyOnFoot")]
        internal static ConfigEntry<bool> FullBodyOnlyOnFoot { get { return CharacterSettings.FullBodyOnlyOnFoot; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.FirstPersonArmsOnlyOnFoot"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.FirstPersonArmsOnlyOnFoot")]
        internal static ConfigEntry<bool> FirstPersonArmsOnlyOnFoot { get { return CharacterSettings.FirstPersonArmsOnlyOnFoot; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.FootIK"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.FootIK")]
        internal static ConfigEntry<bool> FootIK { get { return CharacterSettings.FootIK; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.WalkToStop"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.WalkToStop")]
        internal static ConfigEntry<bool> WalkToStop { get { return CharacterSettings.WalkToStop; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.ModelFile"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.ModelFile")]
        internal static ConfigEntry<string> ModelFile { get { return CharacterSettings.ModelFile; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.TextureFile"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.TextureFile")]
        internal static ConfigEntry<string> TextureFile { get { return CharacterSettings.TextureFile; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.ArmsTextureFile"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.ArmsTextureFile")]
        internal static ConfigEntry<string> ArmsTextureFile { get { return CharacterSettings.ArmsTextureFile; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.AnimBundleFile"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.AnimBundleFile")]
        internal static ConfigEntry<string> AnimBundleFile { get { return CharacterSettings.AnimBundleFile; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.BodyBack"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.BodyBack")]
        internal static ConfigEntry<float> BodyBack { get { return CharacterSettings.BodyBack; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.BodyBackDown"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.BodyBackDown")]
        internal static ConfigEntry<float> BodyBackDown { get { return CharacterSettings.BodyBackDown; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.RunFrom"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.RunFrom")]
        internal static ConfigEntry<float> RunFrom { get { return CharacterSettings.RunFrom; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.AimPitchShare"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.AimPitchShare")]
        internal static ConfigEntry<float> AimPitchShare { get { return CharacterSettings.AimPitchShare; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.FirstPersonWeaponBack"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.FirstPersonWeaponBack")]
        internal static ConfigEntry<float> FirstPersonWeaponBack { get { return CharacterSettings.FirstPersonWeaponBack; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.FirstPersonChestLean"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.FirstPersonChestLean")]
        internal static ConfigEntry<float> FirstPersonChestLean { get { return CharacterSettings.FirstPersonChestLean; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.FpCamX"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.FpCamX")]
        internal static ConfigEntry<float> FpCamX { get { return CharacterSettings.FpCamX; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.FpCamY"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.FpCamY")]
        internal static ConfigEntry<float> FpCamY { get { return CharacterSettings.FpCamY; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.FpCamZ"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.FpCamZ")]
        internal static ConfigEntry<float> FpCamZ { get { return CharacterSettings.FpCamZ; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.JumpClipStart"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.JumpClipStart")]
        internal static ConfigEntry<float> JumpClipStart { get { return CharacterSettings.JumpClipStart; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.StrikeWindup"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.StrikeWindup")]
        internal static ConfigEntry<float> StrikeWindup { get { return CharacterSettings.StrikeWindup; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.UpperPhase"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.UpperPhase")]
        internal static ConfigEntry<float> UpperPhase { get { return CharacterSettings.UpperPhase; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.ClipSprintSpeed"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.ClipSprintSpeed")]
        internal static ConfigEntry<float> ClipSprintSpeed { get { return CharacterSettings.ClipSprintSpeed; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.SprintFrom"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.SprintFrom")]
        internal static ConfigEntry<float> SprintFrom { get { return CharacterSettings.SprintFrom; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.TurnClipAngle"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.TurnClipAngle")]
        internal static ConfigEntry<float> TurnClipAngle { get { return CharacterSettings.TurnClipAngle; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.TurnStartRate"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.TurnStartRate")]
        internal static ConfigEntry<float> TurnStartRate { get { return CharacterSettings.TurnStartRate; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.DirBlendSpeed"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.DirBlendSpeed")]
        internal static ConfigEntry<float> DirBlendSpeed { get { return CharacterSettings.DirBlendSpeed; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.PumpClipSpeed"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.PumpClipSpeed")]
        internal static ConfigEntry<float> PumpClipSpeed { get { return CharacterSettings.PumpClipSpeed; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.RunLegsIn"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.RunLegsIn")]
        internal static ConfigEntry<float> RunLegsIn { get { return CharacterSettings.RunLegsIn; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.RunLegsTurn"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.RunLegsTurn")]
        internal static ConfigEntry<float> RunLegsTurn { get { return CharacterSettings.RunLegsTurn; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.HipsDrift"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.HipsDrift")]
        internal static ConfigEntry<float> HipsDrift { get { return CharacterSettings.HipsDrift; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.ClipWalkSpeed"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.ClipWalkSpeed")]
        internal static ConfigEntry<float> ClipWalkSpeed { get { return CharacterSettings.ClipWalkSpeed; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.ClipRunSpeed"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.ClipRunSpeed")]
        internal static ConfigEntry<float> ClipRunSpeed { get { return CharacterSettings.ClipRunSpeed; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.ClipCrouchSpeed"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.ClipCrouchSpeed")]
        internal static ConfigEntry<float> ClipCrouchSpeed { get { return CharacterSettings.ClipCrouchSpeed; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.CrouchDrop"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.CrouchDrop")]
        internal static ConfigEntry<float> CrouchDrop { get { return CharacterSettings.CrouchDrop; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.Female"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.Female")]
        internal static bool Female { get { return CharacterSettings.Female; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.BodyInCar"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.BodyInCar")]
        internal static bool BodyInCar { get { return CharacterSettings.BodyInCar; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.ArmsInCar"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.ArmsInCar")]
        internal static bool ArmsInCar { get { return CharacterSettings.ArmsInCar; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.BodyModelFile"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.BodyModelFile")]
        internal static string BodyModelFile { get { return CharacterSettings.BodyModelFile; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.BodyTextureFile"/>.</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.BodyTextureFile")]
        internal static string BodyTextureFile { get { return CharacterSettings.BodyTextureFile; } }
        /// <summary>Forwarder of <see cref="CharacterSettings.Slider"/> (the seat part binds its first-person driving offsets with it).</summary>
        [System.Obsolete("refactor: use Presentation CharacterSettings.Slider")]
        static ConfigEntry<float> Slider(ModuleConfig cfg, string sub, string key) { return CharacterSettings.Slider(cfg, sub, key); }
    }
}
