namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// CT3 (plan §7, research B §2 #1 #6 #14 #15): the vehicle hinge FSMs poll their hinge's child count every frame
    /// (vehPart_Attach partAttached/partMissing GameObjectHasChildren or GetChildCountAdvanced+IntCompare, checkWheel, Shake off/idle,
    /// getEngine, getRadiator, GetChild, BumperCheck, WindshieldType, checkSuspension; HingeAdjustVisible polls the global
    /// AdjustToolBool) - Sources fsm/prefabs/Poloska.md L1584, L2856-L2954, L1906, L1929, L2085, L2517, L581, L3416, L2768, L3488.
    /// They now sleep through <see cref="PollSleep"/> and wake in the frame their child count (or the bool) would fire the event.
    /// Not done as an OnTransformChildrenChanged component on the hinges: hinges are children of saved cars (ES3 saveChildren=1
    /// writes every child's components that have an ES3 type, ES3Type_GameObject.cs:L70-L95) and AGENTS.md 5.10 forbids adding
    /// components to saved objects; the guard needs no component and also covers attach paths that bypass PlayMaker (mods, ES3).
    /// </summary>
    public sealed class HingePollFeature : PollSleepFeature
    {
        public override string PlanId { get { return "CT3"; } }
        public override string Key { get { return "Hinge polls"; } }
        public override string Title { get { return "Event-driven vehicle hinges"; } }
        public override int Order { get { return 3; } }
        public override string Help { get { return "Vehicle hinge FSMs that check every frame whether a part is attached sleep until the part count actually changes (exact)."; } }
        static readonly string[] _names = { "vehPart_Attach", "checkWheel", "Shake", "getEngine", "getRadiator", "GetChild", "BumperCheck", "WindshieldType", "checkSuspension", "HingeAdjustVisible" };
        protected override string[] FsmNames { get { return _names; } }
    }

    /// <summary>
    /// CT5 (research B §2 #5 #11-#13 #16-#20 #22 #29): "wait until" polls - item CheckTag (bumper_10.md:L19-L39), Condition and
    /// LiquidAmount clamps (wheel_1.md:L60, Poloska.md:L1213), car DistanceKinematic / TireWear / FuelConsumption off / Speed /
    /// CrashDamage waiting state (Poloska.md L151-L322, L1195), engine AirCooling off, ExhaustSmoke idle, NPC Health, sell-board
    /// ItemList trigger (Sell_Items_mechanic.md:L9). Implemented as the exact <see cref="PollSleep"/> instead of a frame throttle:
    /// nothing is delayed, so no state with a per-second accumulator can drift (states with FloatAdd perSecond, Wait etc. contain
    /// unsupported actions and simply never sleep).
    /// </summary>
    public sealed class WaitPollFeature : PollSleepFeature
    {
        public override string PlanId { get { return "CT5"; } }
        public override string Key { get { return "Wait-until polls"; } }
        public override string Title { get { return "Sleeping wait-until polls"; } }
        public override int Order { get { return 5; } }
        public override string Help { get { return "Item, vehicle and NPC FSMs that only wait for a value to change sleep until it does (exact)."; } }
        static readonly string[] _names = { "CheckTag", "Condition", "LiquidAmount", "DistanceKinematic", "TireWear", "FuelConsumption", "Speed", "CrashDamage", "AirCooling", "ExhaustSmoke", "Health", "ItemList" };
        protected override string[] FsmNames { get { return _names; } }
    }

    /// <summary>
    /// CT11 (research B §1.3 "Options canvases"): the 21 option toggles (Graphics/Gameplay/PlayerCar/Audio canvases, e.g.
    /// Graphics_Canvas.md:L37-L50, Gameplay_Canvas.md:L236-L251) rest in on/off with an everyFrame BoolTest on their own variable
    /// even while the menu is hidden. They sleep until the variable flips (a click goes through the UI BOOL VALUE CHANGED global
    /// transition = a state entry, which wakes them; a settings load writes the variable, which the guard sees the same frame).
    /// Exact, so it does not need to know whether the canvas is open.
    /// </summary>
    public sealed class OptionTogglePollFeature : PollSleepFeature
    {
        public override string PlanId { get { return "CT11"; } }
        public override string Key { get { return "Option toggles"; } }
        public override string Title { get { return "Sleeping option toggles"; } }
        public override int Order { get { return 11; } }
        public override string Help { get { return "Settings-menu toggles stop checking their value every frame while it does not change (exact)."; } }
        static readonly string[] _names = { "toggleFootstepSounds", "disableFatigue", "disableInsanity", "disableSandstorm", "disableThirstHunger", "mapCheat", "peacefulMode", "permaBase",
            "ambient occlusion", "bloom", "color grading", "screen space shadows", "sharpen", "show fps", "toggleADS", "AutoClutch", "automaticGearbox", "toggleHeadbob", "HShifter", "NormalizeInput", "SmoothInput" };
        protected override string[] FsmNames { get { return _names; } }
        protected override string RootPattern { get { return "Graphics_Canvas|Gameplay_Canvas|PlayerCar_Canvas|Audio_Canvas"; } }
    }
}
