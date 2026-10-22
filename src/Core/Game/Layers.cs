namespace WildWasteland.Core
{
    /// <summary>
    /// The game's physics layer numbers and the masks the modules repeat as literals (about 300 "1 &lt;&lt; n" sites). Names come
    /// from the code comments that use them (Senses.cs:108, Webs.cs:26, Orbs.cs:41-42, ThirdPerson.cs:81/381, Culler.cs:16,
    /// Ram.cs:54, WeatherVisuals.cs:205, Senses.cs:356). Layers 12 and 13 are used in masks without a documented meaning.
    /// Constants only: switching callers is Wave A/B work and must keep each mask bit-identical.
    /// </summary>
    public static class Layers
    {
        /// <summary>Walls, rock, props.</summary>
        public const int Default = 0;
        /// <summary>Unity "Ignore Raycast" (WW ragdolls: not pickable, never a camera obstruction).</summary>
        public const int IgnoreRaycast = 2;
        /// <summary>UI; the game's God Mode (F11) moves the player rig here.</summary>
        public const int UI = 5;
        /// <summary>The player.</summary>
        public const int Player = 6;
        /// <summary>Cars and attached vehicle parts (vehPart_Attach).</summary>
        public const int Car = 8;
        /// <summary>Loose items and detached vehicle parts.</summary>
        public const int Item = 9;
        /// <summary>NPCs / creatures (also the cars' CarAttack trigger target layer).</summary>
        public const int Actor = 10;
        /// <summary>Doors.</summary>
        public const int Door = 11;
        /// <summary>Layer 12 (used in masks; meaning undocumented).</summary>
        public const int Layer12 = 12;
        /// <summary>Layer 13 (in the player guns' raycast mask; meaning undocumented).</summary>
        public const int Layer13 = 13;
        /// <summary>Terrain and ground meshes.</summary>
        public const int Ground = 14;
        /// <summary>See-through geometry (fences, grates): blocks movement, not sight.</summary>
        public const int SeeThrough = 16;
        /// <summary>Map / compass icons (exempt from the game's far-item culling).</summary>
        public const int MapIcons = 22;

        /// <summary>The game's bullet obstruction set (RaySensor ObstructedByLayers 19201): Default, Car, Item, Door, Ground.</summary>
        public const int BulletBlockers = (1 << Default) | (1 << Car) | (1 << Item) | (1 << Door) | (1 << Ground);
        /// <summary>BulletBlockers without Car (cars do not hide the player or NPCs; NPC Senses SightBlockers).</summary>
        public const int SightBlockers = BulletBlockers & ~(1 << Car);
        /// <summary>What can be stood on / anchored to: Default, Car, Door, Ground, SeeThrough (Brain GroundMask, Webs Anchors).</summary>
        public const int Standable = (1 << Default) | (1 << Car) | (1 << Door) | (1 << Ground) | (1 << SeeThrough);
        /// <summary>Standable + Item (Pounce Obst, Burrow GroundMask).</summary>
        public const int Obstacles = Standable | (1 << Item);
        /// <summary>The player guns' Raycast mask: Default, Car, Item, Actor, Door, 13, Ground (ThirdPerson GunMask).</summary>
        public const int GunRaycast = (1 << Default) | (1 << Car) | (1 << Item) | (1 << Actor) | (1 << Door) | (1 << Layer13) | (1 << Ground);
        /// <summary>Living bodies: Player, Actor (Orbs OrbBodies).</summary>
        public const int Bodies = (1 << Player) | (1 << Actor);
        /// <summary>Third-person camera obstruction mask: everything but Player, IgnoreRaycast, UI, Item, MapIcons.</summary>
        public const int CameraObstruction = ~((1 << Player) | (1 << IgnoreRaycast) | (1 << UI) | (1 << Item) | (1 << MapIcons));

        /// <summary>Bit mask of one layer.</summary>
        public static int Mask(int layer) { return 1 << layer; }

        /// <summary>True when layer is in mask.</summary>
        public static bool In(int layer, int mask) { return (mask & (1 << layer)) != 0; }
    }
}
