using System.Collections.Generic;
using UnityEngine;

// Presentation contracts (core-modapi.md 8): one character pipeline for player, human NPCs, female NPCs and animals; traversal,
// seat poses, weapon poses, vehicle skins, camera rig, pose authoring. Provider: Presentation. Consumers: NPCAI, Patrols, Quests,
// Gunplay, HUD, Developer, Compatibility. Interfaces only - no logic. FROZEN after W0 (changes via docs/refactor/requests/).
namespace WildWasteland.Core
{
    // ------------------------------------------------------------------ moved unchanged from Services.cs (PlayerBody animation API)

    /// <summary>Mirror of Apocaplayer.ModAPI v1 (static part). Provided by the Presentation module (today PlayerBody).
    /// Superseded by <see cref="ICharacterPresentation"/> for new code; kept until Wave C.</summary>
    public interface IAnimationApi
    {
        /// <summary>Contract version (1).</summary>
        int ApiVersion { get; }
        /// <summary>Provider version string.</summary>
        string Version { get; }
        /// <summary>Animation bundle loaded (clips available).</summary>
        bool Ready { get; }
        /// <summary>The player camera is in third person.</summary>
        bool PlayerThirdPerson { get; }
        /// <summary>The player's shout clip, or null.</summary>
        AudioClip PlayerShout { get; }
        /// <summary>Head position of the player during a jump (scene space).</summary>
        bool TryPlayerJumpHead(GameObject player, out Vector3 position);
        /// <summary>Muzzle of the player's held weapon (scene space).</summary>
        bool TryPlayerWeaponMuzzle(out Vector3 position);
        /// <summary>All clip names of the bundle.</summary>
        string[] ClipNames();
        /// <summary>True when <paramref name="name"/> is one of the player clips.</summary>
        bool IsPlayerClip(string name);
        /// <summary>Clip by name or null.</summary>
        AnimationClip GetClip(string name);
        /// <summary>Normalized weapon key of a weapon object name.</summary>
        string WeaponKey(string objectName);
        /// <summary>Weapon kind name of a key.</summary>
        string WeaponKind(string weaponKey);
        /// <summary>True when poses exist for the key.</summary>
        bool HasWeaponPoses(string weaponKey);
        /// <summary>Held pose of a weapon for a clip.</summary>
        bool WeaponPose(string weaponKey, string clip, out Vector3 localPosition, out Quaternion localRotation);
        /// <summary>Reload clip name of a weapon.</summary>
        string ReloadClip(string weaponKey);
        /// <summary>Reload clip length, seconds.</summary>
        float ReloadClipSeconds(string weaponKey);
        /// <summary>Time per round for one-round reloads, seconds.</summary>
        float RoundSeconds(string weaponKey);
        /// <summary>True for weapons reloaded one round at a time.</summary>
        bool ReloadsOneRoundAtATime(string weaponKey);
        /// <summary>True for weapons that cock after every shot.</summary>
        bool CocksAfterShot(string weaponKey);
        /// <summary>Native movement speed of a tier ("walk", "run"...).</summary>
        float NativeSpeed(string tier);
        /// <summary>Number of attached characters.</summary>
        int Count { get; }
        /// <summary>Drives an NPC/turret Animator with the player animation set. Null when another owner animates it.</summary>
        IAnimatedCharacter Attach(Animator animator);
        /// <summary>The character driving <paramref name="animator"/>, or null.</summary>
        IAnimatedCharacter Find(Animator animator);
        /// <summary>(WW) A scripted traversal animation on a humanoid NPC Animator: "Ladder" (looping; Phase in cycles, one cycle = two
        /// rungs) or "ClimbWaist" / "ClimbHigh" (Phase 0..1 of the clip). Suspends that Animator's ModAPI graph until Dispose. Null when the
        /// rig is not humanoid or the animation is missing.</summary>
        INpcTraversalAnimation BeginNpcTraversal(Animator animator, string kind);
        /// <summary>(WW) The ladder animation: height climbed per cycle (two rungs), metres.</summary>
        float LadderCycleRise { get; }
        /// <summary>(WW) Hips-to-rung distance in the ladder source, metres.</summary>
        float LadderToeDepth { get; }
    }

    /// <summary>(WW) One NPC's ladder / climb animation lease (see IAnimationApi.BeginNpcTraversal).</summary>
    public interface INpcTraversalAnimation
    {
        /// <summary>False once the lease ended or the animator died.</summary>
        bool Valid { get; }
        /// <summary>Animation phase (cycles for ladders, 0..1 for climbs).</summary>
        float Phase { get; set; }
        /// <summary>Natural length of one cycle / of the climb clip, seconds.</summary>
        float Length { get; }
        /// <summary>Ends the lease and resumes the character graph.</summary>
        void Dispose();
    }

    /// <summary>Mirror of Apocaplayer.ModAPI.Character: one animated NPC/turret character.</summary>
    public interface IAnimatedCharacter
    {
        /// <summary>Driven animator.</summary>
        Animator Animator { get; }
        /// <summary>False once disposed or destroyed.</summary>
        bool Valid { get; }
        /// <summary>Current weapon key.</summary>
        string WeaponKey { get; }
        /// <summary>Current weapon kind name.</summary>
        string WeaponKind { get; }
        /// <summary>Reload running.</summary>
        bool Reloading { get; }
        /// <summary>Pump/cock running.</summary>
        bool Pumping { get; }
        /// <summary>Clip on the hands layer.</summary>
        string HandsClip { get; }
        /// <summary>Clip on the action layer.</summary>
        string ActionClip { get; }
        /// <summary>Clip on the upper-body layer.</summary>
        string UpperClip { get; }
        /// <summary>Speed cap requested by the current action, m/s.</summary>
        float SpeedCap { get; }
        /// <summary>Velocity is set by the caller instead of measured.</summary>
        bool ManualVelocity { get; set; }
        /// <summary>Planar velocity used for locomotion blending.</summary>
        Vector3 Velocity { get; set; }
        /// <summary>Crouched stance.</summary>
        bool Crouched { get; set; }
        /// <summary>Aiming stance.</summary>
        bool Aiming { get; set; }
        /// <summary>Firing stance.</summary>
        bool Firing { get; set; }
        /// <summary>In the air.</summary>
        bool Airborne { get; set; }
        /// <summary>Aim pitch, degrees.</summary>
        float AimPitch { get; set; }
        /// <summary>Turn in place enabled.</summary>
        bool TurnInPlace { get; set; }
        /// <summary>Walk-to-stop transition enabled.</summary>
        bool WalkToStop { get; set; }
        /// <summary>Graph suspended (another owner animates).</summary>
        bool Suspended { get; set; }
        /// <summary>Sets the held weapon model.</summary>
        void SetWeapon(Transform model);
        /// <summary>Sets the held weapon model with an explicit key.</summary>
        void SetWeapon(Transform model, string weaponKey);
        /// <summary>Plays a shot.</summary>
        void Shoot();
        /// <summary>Plays a shot lasting <paramref name="seconds"/>.</summary>
        void Shoot(float seconds);
        /// <summary>Plays a reload.</summary>
        void Reload(float seconds);
        /// <summary>Plays a reload of <paramref name="rounds"/> rounds.</summary>
        void Reload(float seconds, int rounds);
        /// <summary>Cancels a running reload.</summary>
        void CancelReload();
        /// <summary>Plays a pump/cock.</summary>
        void Pump();
        /// <summary>Plays a jump.</summary>
        void Jump();
        /// <summary>Plays a kick.</summary>
        void Kick();
        /// <summary>Plays a throw.</summary>
        void Throw();
        /// <summary>Plays a throw with the chosen hand.</summary>
        void Throw(bool rightHand);
        /// <summary>Plays a melee strike.</summary>
        void Strike();
        /// <summary>Plays a melee strike lasting <paramref name="seconds"/>.</summary>
        void Strike(float seconds);
        /// <summary>Releases the animator.</summary>
        void Dispose();
    }

    /// <summary>Model/texture paths of a character identity (NPC Women -> PlayerBody). Superseded by <see cref="ICharacterAppearance"/>.</summary>
    public interface ICharacterAssets
    {
        /// <summary>Model and texture file paths of <paramref name="identity"/>.</summary>
        bool TryPaths(string identity, out string model, out string texture);
    }

    /// <summary>Dresses an NPC by identity (NPC Women -> Patrols). Superseded by <see cref="ICharacterAppearance"/>.</summary>
    public interface INpcAppearance
    {
        /// <summary>Applies the identity's look to <paramref name="root"/>.</summary>
        void Dress(GameObject root, string identity, bool randomize);
    }

    // ------------------------------------------------------------------ new (Wave B Presentation)

    /// <summary>Kind of rig a character uses.</summary>
    public enum CharacterKind { Player, Human, Female, Dog, Animal }

    /// <summary>One-shot actions a driver asks the character pipeline to play.</summary>
    public enum CharacterActionKind { Shoot, Reload, CancelReload, Pump, Jump, Kick, Throw, ThrowLeft, Strike, Punch, Climb, Die }

    /// <summary>Per-tick inputs of one character (the player adapter and NPC drivers fill the same struct).</summary>
    public struct CharacterInputs
    {
        /// <summary>Planar velocity (scene space), m/s.</summary>
        public Vector3 Velocity;
        /// <summary>Aim pitch, degrees (positive = up).</summary>
        public float AimPitch;
        /// <summary>Speed cap requested by the driver, m/s (0 = none).</summary>
        public float SpeedCap;
        /// <summary>Stance flags.</summary>
        public bool Crouched, Aiming, Firing, Airborne, Crawling, Seated;
        /// <summary>Locomotion options.</summary>
        public bool TurnInPlace, WalkToStop;
    }

    /// <summary>The single character driver (ModCharacter + CharPlan) for player and NPCs (README 6.1).</summary>
    public interface ICharacterPresentation
    {
        /// <summary>Clips/bundles loaded; attach calls fail before.</summary>
        bool Ready { get; }
        /// <summary>Starts driving <paramref name="animator"/> as <paramref name="id"/>; false when another owner animates it or the rig is unsupported.</summary>
        bool Attach(EntityId id, Animator animator, CharacterKind kind);
        /// <summary>Stops driving the character and restores its animator.</summary>
        void Detach(EntityId id);
        /// <summary>True while <paramref name="id"/> is driven by the pipeline.</summary>
        bool IsAttached(EntityId id);
        /// <summary>Kind the character was attached as.</summary>
        bool TryGetKind(EntityId id, out CharacterKind kind);
        /// <summary>Sets this tick's inputs (read on the next animation update).</summary>
        void SetInputs(EntityId id, ref CharacterInputs inputs);
        /// <summary>Last inputs of the character.</summary>
        bool TryGetInputs(EntityId id, out CharacterInputs inputs);
        /// <summary>Plays a one-shot action; <paramref name="seconds"/>/<paramref name="rounds"/> are used by timed actions (0 = clip length / 1).</summary>
        void Action(EntityId id, CharacterActionKind action, float seconds, int rounds);
        /// <summary>The held weapon changed (model may be null for unarmed); key from IWeaponCatalog/IAnimationApi.WeaponKey.</summary>
        void WeaponChanged(EntityId id, Transform model, string weaponKey);
        /// <summary>Suspends/resumes the character graph (cutscenes, seat poses owned elsewhere).</summary>
        void Suspend(EntityId id, bool suspended);
        /// <summary>Muzzle point and direction of the held weapon (scene space).</summary>
        bool TryGetMuzzle(EntityId id, out Vector3 position, out Vector3 direction);
        /// <summary>Held prop transform (weapon/tool model) of the character.</summary>
        bool TryGetHeldProp(EntityId id, out Transform prop);
        /// <summary>Character root transform of an attached id.</summary>
        bool TryGetRoot(EntityId id, out Transform root);
        /// <summary>Visual root offset (scene space) other modules apply for animal behaviours (burrow depth, pounce arc).</summary>
        void SetVisualOffset(EntityId id, Vector3 offset);
        /// <summary>Animation update tier (0 = every frame, higher = cheaper) chosen by distance LOD; read-only for diagnostics.</summary>
        int UpdateTier(EntityId id);
        /// <summary>Number of attached characters.</summary>
        int Count { get; }
    }

    /// <summary>One appearance service: Women of Wasteland, crew appearance, faction outfits (README 6.1, D9/D10).</summary>
    public interface ICharacterAppearance
    {
        /// <summary>Applies the identity's look to <paramref name="root"/> (randomize = roll a variant).</summary>
        void Dress(GameObject root, string identity, bool randomize);
        /// <summary>Model and texture file paths of <paramref name="identity"/>.</summary>
        bool TryPaths(string identity, out string model, out string texture);
        /// <summary>Rolls the variant of a freshly spawned NPC (e.g. "Gungirl"/"Shoota"); null = keep the native look.</summary>
        string RollVariant(string prefabName, int seed);
        /// <summary>Identity currently applied to an NPC (null = native).</summary>
        string IdentityOf(GameObject npc);
        /// <summary>True when the NPC uses a female rig/voice.</summary>
        bool IsFemale(GameObject npc);
        /// <summary>Voice/audio profile id of the identity (pain, death, shout, reload sounds).</summary>
        string VoiceOf(string identity);
    }

    /// <summary>Kinds of traversal handled by the shared engine.</summary>
    public enum TraversalKind { Ladder, ClimbWaist, ClimbHigh, Drop, Crawl }

    /// <summary>One character's traversal lease (player or NPC).</summary>
    public interface ITraversalLease
    {
        /// <summary>Character holding the lease.</summary>
        EntityId Character { get; }
        /// <summary>Traversal kind.</summary>
        TraversalKind Kind { get; }
        /// <summary>False once ended.</summary>
        bool Valid { get; }
        /// <summary>Phase (cycles for ladders, 0..1 for climbs/drops).</summary>
        float Phase { get; set; }
        /// <summary>Natural length of one cycle or the clip, seconds.</summary>
        float Length { get; }
        /// <summary>Ends the lease (publishes TraversalEnded).</summary>
        void Dispose();
    }

    /// <summary>Unified traversal engine (README P3): leases, ladder occupancy, player ladder state (replaces Services.PlayerLadder*/LadderClimbers).</summary>
    public interface ITraversal
    {
        /// <summary>Starts a traversal animation lease; null when the rig or clip is unsupported.</summary>
        ITraversalLease Begin(EntityId character, Animator animator, TraversalKind kind);
        /// <summary>Active lease of a character, or null.</summary>
        ITraversalLease LeaseOf(EntityId character);
        /// <summary>True while the player climbs a ladder.</summary>
        bool PlayerOnLadder { get; }
        /// <summary>Ladder trigger the player climbs, his climbing side (horizontal, from rung line to player), slide phase and top slide speed.</summary>
        bool TryPlayerLadder(out Collider ladder, out Vector3 side, out float slidePhase, out float slideSpeed);
        /// <summary>Reports (on = true) or clears an NPC's feet on a ladder (refreshed every physics step by NPCAI).</summary>
        void SetLadderOccupant(EntityId npc, Vector3 feetScene, bool on);
        /// <summary>Fills <paramref name="into"/> (cleared first) with the feet of every NPC on a ladder (scene space).</summary>
        void LadderOccupants(List<Vector3> into);
        /// <summary>Ladder animation: height climbed per cycle (two rungs), metres.</summary>
        float LadderCycleRise { get; }
        /// <summary>Ladder animation: hips-to-rung distance, metres.</summary>
        float LadderToeDepth { get; }
        /// <summary>True while the player crawls (low crawl).</summary>
        bool PlayerCrawling { get; }
    }

    /// <summary>Seat kinds served by the shared seat-pose system.</summary>
    public enum SeatKind { Driver, Passenger, Turret, Bike, BusPassenger, Rider, Follower }

    /// <summary>One seat-pose system for player car seat, crews, bus passengers, riders and followers (README 6.1).</summary>
    public interface ISeatPoses
    {
        /// <summary>Seats <paramref name="character"/> at <paramref name="seat"/> with a named pose (null = default for the kind).</summary>
        bool Seat(GameObject character, Transform seat, SeatKind kind, string pose);
        /// <summary>Releases the seat pose and restores the character.</summary>
        void Release(GameObject character);
        /// <summary>True while the character holds a seat pose.</summary>
        bool IsSeated(GameObject character);
        /// <summary>Seat and kind of a seated character.</summary>
        bool TryGetSeat(GameObject character, out Transform seat, out SeatKind kind);
        /// <summary>Enables/disables seated animation (aim/fire from the seat).</summary>
        void Animate(GameObject character, bool enabled);
        /// <summary>Hands/IK targets of the player's seat (steering wheel grip) are active.</summary>
        bool PlayerHandsOnWheel { get; }
    }

    /// <summary>Weapon pose weights (lowering during weapon switch, ADS transition) per character.</summary>
    public interface IWeaponPose
    {
        /// <summary>Lowering weight 0..1 of the character's weapon (1 = fully lowered).</summary>
        float LowerWeight(EntityId character);
        /// <summary>Aim-down-sights weight 0..1 of the character.</summary>
        float AdsWeight(EntityId character);
        /// <summary>Requests a lowering weight from <paramref name="owner"/> (the highest request wins; 0 clears).</summary>
        void RequestLower(IModule owner, EntityId character, float weight);
        /// <summary>Aim lift (degrees) applied to a weapon key.</summary>
        float AimLift(string weaponKey);
    }

    /// <summary>Faction vehicle paintjobs and cargo textures (README D10).</summary>
    public interface IVehicleSkins
    {
        /// <summary>Paints <paramref name="vehicle"/> for a faction; cargo container texture from <paramref name="lootKey"/> (may be null).</summary>
        bool Paint(GameObject vehicle, string faction, string lootKey);
        /// <summary>Restores the native look.</summary>
        void Clear(GameObject vehicle);
        /// <summary>True when a skin exists for the faction.</summary>
        bool HasSkin(string faction);
        /// <summary>Paint key applied to a vehicle (null = native).</summary>
        string PaintOf(GameObject vehicle);
    }

    /// <summary>Camera view modes.</summary>
    public enum CameraViewMode { FirstPerson, ThirdPerson, Peek, Orbit, Map, Binoculars, Scope }

    /// <summary>Camera rig queries (README P2). Projection/FOV writes go through Core CameraArbiter.</summary>
    public interface ICameraRig
    {
        /// <summary>Current view mode.</summary>
        CameraViewMode Mode { get; }
        /// <summary>True in any third-person mode.</summary>
        bool ThirdPerson { get; }
        /// <summary>Player view pose and field of view (scene space).</summary>
        bool TryGetPlayerView(out Vector3 position, out Quaternion rotation, out float fov);
        /// <summary>Aim ray from the eye and its first hit point (shared once per frame).</summary>
        bool TryGetAimRay(out Ray ray, out Vector3 hitPoint);
        /// <summary>Crosshair screen position (pixels).</summary>
        Vector2 CrosshairScreen { get; }
        /// <summary>True while the rig owns the mouse wheel (other wheel users stand down).</summary>
        bool OwnsMouseWheel { get; }
    }

    /// <summary>Developer editors: named pose sets (weapon poses, aim lift, steering-wheel grips) owned by Presentation.</summary>
    public interface IPoseAuthoring
    {
        /// <summary>Pose set names ("WeaponPoses", "AimLift", "WheelGrips"...).</summary>
        IList<string> Sets { get; }
        /// <summary>Keys within a set (weapon keys, vehicle keys...).</summary>
        void Keys(string set, List<string> into);
        /// <summary>Pose of one key/slot.</summary>
        bool TryGetPose(string set, string key, string slot, out Vector3 localPosition, out Quaternion localRotation);
        /// <summary>Sets a pose live (not saved yet).</summary>
        void SetPose(string set, string key, string slot, Vector3 localPosition, Quaternion localRotation);
        /// <summary>Writes the set to disk.</summary>
        bool Save(string set);
        /// <summary>Reloads the set from disk, dropping live edits.</summary>
        void Revert(string set);
    }

    /// <summary>Developer editors: seat poses (bus driver/passenger, crew, turret spots) owned by Presentation.</summary>
    public interface ISeatPoseAuthoring
    {
        /// <summary>Vehicle/seat keys with stored poses.</summary>
        void Keys(List<string> into);
        /// <summary>Stored pose of a seat (local to the seat).</summary>
        bool TryGetSeatPose(string key, SeatKind kind, out Vector3 localPosition, out Quaternion localRotation);
        /// <summary>Sets a seat pose live.</summary>
        void SetSeatPose(string key, SeatKind kind, Vector3 localPosition, Quaternion localRotation);
        /// <summary>Writes all seat poses to disk.</summary>
        bool Save();
    }
}
