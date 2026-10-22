# RC1 default settings audit

Captured from Denis's installed configuration on 2026-10-15 for Wild Wasteland **0.1.4 RC1**.

691 active entries reviewed: 3 F6/Apocasetter entries, 141 internal gameplay entries, and 547 internal tool/tuning entries. Entries without binding metadata are legacy orphans (161 total) and are excluded. Existing configuration files are preserved; first-run standalone migration remains unchanged.

Ten differing current choices are now the source-code defaults. At Denis's explicit correction, `[Guns] ToggleIronsights` stays **false** by default even though the installed value is true. Unchanged entries keep their existing defaults. No settings are added or removed.

| File | Section | Setting | Previous default | RC1 default |
|---|---|---|---|---|
| com.denis.apocalypter.wildwasteland.cfg | Keys | In-Game Settings Button | F7 | F3 |
| WildWasteland/settings.cfg | CleanHUD / HUD | ShowNeeds | false | true |
| WildWasteland/settings.cfg | CleanHUD / HUD | ShowHealthNumbers | false | true |
| WildWasteland/settings.cfg | HUD | Crosshair | 3 | 1 |
| WildWasteland/settings.cfg | PlayerBody | ToggleObserving | false | true |
| WildWasteland/settings.cfg | PlayerBody / Camera culling | CameraCulling | true | false |
| WildWasteland/tools.cfg | Patrols / Debug | AllowDebugSpawns | false | true |
| WildWasteland/tools.cfg | Patrols / Traffic tuning | DebugMap | false | true |
| WildWasteland/tools.cfg | PlayerBody / Camera | ThirdPersonDistance | 5.44 | 3.26 |
| WildWasteland/tools.cfg | PlayerBody / Camera | ThirdPersonCarDistance | 15 | 5.38 |

## com.denis.apocalypter.wildwasteland.cfg

| Section | Setting | Type | RC1 default |
|---|---|---|---|
| General | Apocasetter | Boolean | true |
| General | Enable Debug | Boolean | false |
| Keys | In-Game Settings Button | Key | F3 |

## WildWasteland/settings.cfg

| Section | Setting | Type | RC1 default |
|---|---|---|---|
| CleanHUD | Enabled | Boolean | true |
| CleanHUD / HUD | HUD | Boolean | true |
| CleanHUD / HUD | ShowNeeds | Boolean | true |
| CleanHUD / HUD | ShowHealthNumbers | Boolean | true |
| CleanHUD / HUD | ShowInsanity | Boolean | true |
| CleanHUD / HUD | Scale | Single | 1 |
| CleanHUD / Menu | Enabled | Boolean | true |
| CleanHUD / Menu | EnableCheats | Boolean | false |
| Debug | VerboseLog | Boolean | false |
| Guns | Enabled | Boolean | true |
| Guns | Projectiles | Boolean | true |
| Guns | PerGunMagazines | Boolean | true |
| Guns | Unloading | Boolean | true |
| Guns | BackpackOverflow | Boolean | true |
| Guns | ArmorOverflow | Boolean | true |
| Guns | BossHealth | Boolean | true |
| Guns | MonsterProjectilesHitWalls | Boolean | true |
| Guns | MeleeOnWheels | Boolean | true |
| Guns | NpcAim | Boolean | true |
| Guns | IronsightWheelZoom | Boolean | true |
| Guns | ToggleIronsights | Boolean | false |
| Guns | SmoothWeaponSwitch | Boolean | true |
| Guns / Ammo | HoldSeconds | Single | 0.35 |
| Guns / Bosses | AdjustHumanBossHP | Single | 50 |
| Guns / Projectiles | BulletSpeed | Single | 250 |
| Guns / Projectiles | BoltSpeed | Single | 60 |
| Guns / Projectiles | HeadshotMultiplier | Single | 1.5 |
| Guns / Projectiles | Tracers | Boolean | true |
| Guns / Projectiles | PlayerGunTracers | Boolean | true |
| Guns / Projectiles | VehicleDamage | Boolean | true |
| Guns / Projectiles | WheelPopOff | Boolean | true |
| Guns / Ranges | PistolRange | Single | 60 |
| Guns / Ranges | SmgRange | Single | 70 |
| Guns / Ranges | RifleRange | Single | 120 |
| Guns / Ranges | SniperRange | Single | 250 |
| Guns / Ranges | ShotgunRange | Single | 35 |
| Guns / Ranges | CrossbowRange | Single | 90 |
| HUD | Enabled | Boolean | true |
| HUD | Crosshair | Int32 | 1 |
| HUD | SelectionDot | Boolean | true |
| HUD | HitFeedback | Boolean | true |
| HUD | HitMarker | Boolean | true |
| HUD | FloatingDamage | Int32 | 2 |
| HUD | HeadshotSound | Boolean | true |
| HUD | HeadshotVolume | Int32 | 100 |
| Keys | Observe (third person) | Key | LeftAlt |
| Keys | Climb (alternative key) | Key | None |
| Keys | Cassette start/stop | Key | Z |
| Keys | Cassette volume down | Key | Minus |
| Keys | Cassette volume up | Key | Equals |
| Keys | Shout | Key | Q |
| Keys | Shout modifier | Key | LeftAlt |
| Keys | Guns: unload fallback key | Key | R |
| Keys | Patrol editor | Key | None |
| Keys | Template recorder | Key | None |
| Keys | Bus passenger pose editor | Key | None |
| Keys | Merchant item editor | Key | None |
| NPC | Enabled | Boolean | true |
| NPC | EnableNpcDetection | Boolean | true |
| NPC | EnableNpcPathfinding | Boolean | true |
| NPC | NpcAim | Boolean | true |
| NPC | EnableIdleBehavior | Boolean | true |
| NPC | CoyotesIdle | Boolean | true |
| NPC | FriendsPassThrough | Boolean | true |
| NPC | Storms | Boolean | true |
| NPC | Burrow | Boolean | true |
| NPC | Ambush | Boolean | true |
| NPC | Pounce | Boolean | true |
| NPC | SpiderNests | Boolean | true |
| NPC | DogBite | Boolean | true |
| NPC | DogAnimations | Boolean | true |
| NPC | DogsLieDown | Boolean | true |
| NPC | DogJumpAttack | Boolean | true |
| NPC | DogCompanionCompat | Boolean | true |
| NPC / Detection | SightCone | Single | 100 |
| NPC / Detection | SightRange | Single | 100 |
| NPC / Detection | DarkSightRange | Single | 5 |
| NPC / Pathfinding | ScaleWithActors | Boolean | false |
| Patrols | Enabled | Boolean | true |
| Patrols | Automatic spawns | Boolean | true |
| Patrols | World traffic | Boolean | true |
| Patrols | Faction battles | Boolean | true |
| Patrols | Merchants | Boolean | true |
| Patrols | Human turrets | Boolean | true |
| Patrols | Ranged combat | Boolean | true |
| Patrols | Ram damage | Boolean | true |
| Patrols | Ram damage in car | Boolean | false |
| Patrols | Raider music | Boolean | true |
| Patrols | Custom paintjobs | Boolean | true |
| Patrols | Always explode cars | Boolean | false |
| Patrols | Cleanup | Boolean | true |
| Patrols | Raiders overheat | Boolean | false |
| Patrols | Fury Road | Boolean | true |
| Patrols | Simple convoy bandits AI | Boolean | true |
| Patrols | Tactical driving | Boolean | true |
| Patrols | War Rig spawns | Boolean | true |
| Patrols | War Rig ground recovery | Boolean | false |
| Patrols / Balance | MinPartHealth | Single | 80 |
| Patrols / Balance | MaxPartHealth | Single | 100 |
| Patrols / Balance | ExplodedCarPartsPercent | Single | 20 |
| Patrols / Combat | NPC ram damage multiplier | Single | 4 |
| Patrols / Combat | ShootDistance | Single | 40 |
| Patrols / Combat | WitnessMeChance | Single | 10 |
| Patrols / Merchants | Max merchants | Int32 | 3 |
| Patrols / Merchants | Merchant spawn chance | Single | 100 |
| Patrols / Merchants | Merchant check interval (minutes) | Single | 5 |
| Patrols / Spawning | PatrolSizePercent | Single | 100 |
| Patrols / Spawning | Max patrols | Int32 | 3 |
| Patrols / Spawning | Max convoys | Int32 | 1 |
| Patrols / Spawning | Car spawn limit | Boolean | true |
| Patrols / Spawning | Max cars in event | Int32 | 5 |
| Patrols / Spawning | Max cars in double event | Int32 | 6 |
| Patrols / World traffic | PatrolSpeedKmh | Single | 55 |
| Patrols / World traffic | ConvoySpeedKmh | Single | 38 |
| Patrols / World traffic | Camp traffic range | Single | 2500 |
| PlayerBody | Enabled | Boolean | true |
| PlayerBody | Character | Gender | Male |
| PlayerBody | ThirdPerson | Boolean | true |
| PlayerBody | ToggleObserving | Boolean | true |
| PlayerBody | DynamicCrosshair | Boolean | true |
| PlayerBody | BodyFirstPerson | Boolean | true |
| PlayerBody | FullBodyOnlyOnFoot | Boolean | false |
| PlayerBody | FirstPersonArms | Boolean | true |
| PlayerBody | FirstPersonArmsOnlyOnFoot | Boolean | false |
| PlayerBody | FirstPersonArmSkin | Boolean | true |
| PlayerBody | ReplaceDriver | Boolean | true |
| PlayerBody | EquipmentOnBody | Boolean | true |
| PlayerBody | FemalePainSounds | Boolean | true |
| PlayerBody | SmoothWeaponSway | Boolean | true |
| PlayerBody | SmoothAimDownSights | Boolean | true |
| PlayerBody | PickAssist | Boolean | true |
| PlayerBody / Camera culling | CameraCulling | Boolean | false |
| PlayerBody / Camera culling | CameraCullingInVehicles | Boolean | false |
| PlayerBody / Climbing | Climbing | Boolean | true |
| PlayerBody / Climbing | JumpTriggersClimbing | Boolean | true |
| PlayerBody / Vehicles | VehicleStatusHint | Boolean | true |
| PlayerBody / Vehicles | VehicleHotkeyHint | Boolean | true |
| PlayerBody / Vehicles | IgnitionKey | Boolean | true |
| PlayerBody / Vehicles | LanceIgnoresOwnCar | Boolean | true |
| POI | Enabled | Boolean | true |
| POI / World | Spawn chance (%) | Int32 | 2 |

## WildWasteland/tools.cfg

| Section | Setting | Type | RC1 default |
|---|---|---|---|
| Core | Frame budget (ms) | Single | 2 |
| Guns / Ammo | AnimationTimeout | Single | 4 |
| Guns / Ballistics | PlayerGuns | Boolean | true |
| Guns / Ballistics | FullDamageUntil | Single | 50 |
| Guns / Ballistics | NpcHitRadius | Single | 0.05 |
| Guns / Ballistics | NpcShotgunDamage | Single | 1.7 |
| Guns / Ballistics | ShotgunPellets | Int32 | 3 |
| Guns / Ballistics | ShotgunPelletSpread | Single | 1 |
| Guns / Ballistics | PlayerAccuracy | Single | 100 |
| Guns / Ballistics | NpcAccuracy | Single | 50 |
| Guns / Ballistics | PlayerBodyRadius | Single | 0.22 |
| Guns / Ballistics | PlayerHeadRadius | Single | 0.14 |
| Guns / Ballistics | VehicleDamagePer1 | Single | 20 |
| Guns / Ballistics | WheelDamageMultiplier | Single | 5 |
| Guns / Melee | StabWeapons | String | knife,shiv,machete |
| Guns / Melee | StabSound | String | Sounds/knifestab.wav |
| Guns / NPC aim | NpcAimAtBody | Boolean | true |
| Guns / NPC aim | LeadTargets | Boolean | true |
| Guns / NPC aim | LeadAccuracy | Single | 0.75 |
| Guns / NPC aim | LeadError | Single | 30 |
| Guns / NPC aim | MaxLeadTime | Single | 1.5 |
| Guns / Tracers | TracerColor | Color | FFC759FF |
| Guns / Tracers | Glow | Single | 1.5 |
| Guns / Tracers | TracerLength | Single | 4 |
| Guns / Tracers | TracerWidth | Single | 0.1 |
| Guns / Tracers | BoltColor | Color | D9B880FF |
| Guns / Tracers | BoltLength | Single | 0.8 |
| Guns / Tracers | BoltWidth | Single | 0.04 |
| Guns / Tracers | MaxTracers | Int32 | 300 |
| Guns / Tracers | ImpactEffects | Boolean | true |
| Guns / Tracers | MetalSparks | Boolean | true |
| Guns / Tracers | MetalSparksScale | Single | 0.1 |
| HUD / Crosshair | DotScale | Single | 0.3 |
| HUD / Crosshair | SearchSeconds | Single | 1 |
| HUD / Headshot sound | Gain | Single | 1.3 |
| HUD / Headshot sound | MinInterval | Single | 0.075 |
| HUD / Hit feedback | DamageFontSize | Int32 | 14 |
| HUD / Hit feedback | HitMarkerSize | Int32 | 22 |
| HUD / Hit feedback | NumberSeconds | Single | 1.2 |
| HUD / Hit feedback | ListSeconds | Single | 4 |
| HUD / Hit feedback | ListLines | Int32 | 8 |
| HUD / Hit feedback | MarkerSeconds | Single | 0.18 |
| HUD / Hit feedback | MergeSeconds | Single | 0.05 |
| NPC / Ambush | Prefabs | String | Scorpion_Small,Scorpion_Big,Spider_Small,Spider_Big |
| NPC / Ambush | MinDistance | Single | 3 |
| NPC / Ambush | MaxDistance | Single | 5 |
| NPC / Behaviour | DogIdlePoseMinSeconds | Single | 10 |
| NPC / Behaviour | DogIdlePoseMaxSeconds | Single | 25 |
| NPC / Behaviour | DogLieFrontLift | Single | 0 |
| NPC / Brain | ReactionTime | Single | 100 |
| NPC / Brain | TurnRate | Single | 210 |
| NPC / Brain | AimPose | Boolean | true |
| NPC / Brain | CrouchChance | Single | 50 |
| NPC / Brain | FeelerLength | Single | 3.5 |
| NPC / Brain | MeleeFeelerLength | Single | 2.5 |
| NPC / Brain | FeelerAngle | Single | 60 |
| NPC / Brain | FeelerCount | Int32 | 7 |
| NPC / Brain | DropCheck | Boolean | true |
| NPC / Brain | AdvanceChance | Single | 10 |
| NPC / Brain | AdvanceMin | Single | 2 |
| NPC / Brain | AdvanceMax | Single | 4 |
| NPC / Brain | StuckBackupSeconds | Single | 0.8 |
| NPC / Brain | StuckMemorySeconds | Single | 5 |
| NPC / Brain | StuckGiveUpCount | Int32 | 3 |
| NPC / Brain | ApocaplayerClips | Boolean | true |
| NPC / Brain | StrafeAngle | Single | 50 |
| NPC / Brain | CoverBelow | Single | 50 |
| NPC / Brain | CoverRange | Single | 25 |
| NPC / Brain | CoverMin | Single | 20 |
| NPC / Brain | CoverMax | Single | 40 |
| NPC / Brain | MaxDistance | Single | 150 |
| NPC / Burrow | Prefabs | String | Scorpion_Small |
| NPC / Burrow | MinSeconds | Single | 10 |
| NPC / Burrow | MaxSeconds | Single | 30 |
| NPC / Burrow | AmbushMin | Single | 3 |
| NPC / Burrow | AmbushMax | Single | 5 |
| NPC / Burrow | SeenFrom | Single | 5 |
| NPC / Burrow | BurrowSeconds | Single | 0.9 |
| NPC / Burrow | RiseSeconds | Single | 0.7 |
| NPC / Burrow | Depth | Single | 0.75 |
| NPC / Burrow | SandKeywords | String | sand |
| NPC / Burrow | UseTextureLayers | Boolean | false |
| NPC / Burrow | TerrainIsSand | Boolean | true |
| NPC / Burrow | FxScale | Single | 0.3 |
| NPC / Burrow | FxVolume | Single | 0.35 |
| NPC / Burrow | FxPitch | Single | 1.5 |
| NPC / Burrow | FxRange | Single | 45 |
| NPC / Burrow | Log | Boolean | false |
| NPC / Debug | VerboseLog | Boolean | false |
| NPC / Debug | NavDump | Boolean | false |
| NPC / Debug | ShowNavigation | Boolean | false |
| NPC / Detection | SightConeInsects | Single | 210 |
| NPC / Detection | SightConeBeasts | Single | 190 |
| NPC / Detection | SightConeBrutes | Single | 210 |
| NPC / Detection | SightConeBosses | Single | 300 |
| NPC / Detection | SightConeBurrowers | Single | 360 |
| NPC / Detection | ConeInsects | String | Spider,Scorpion,Wasp,Arachnid |
| NPC / Detection | ConeBeasts | String | Rat,Hound,Skinwal,Nightwalker |
| NPC / Detection | ConeBrutes | String | Professor,Teacher,Juggernaut |
| NPC / Detection | ConeBurrowers | String | Burrower |
| NPC / Dogs | Prefabs | String | Wild Hound,Yard Hound,Grimhound,Nightwalker,Alpha_Nightwalker |
| NPC / Dogs | Log | Boolean | false |
| NPC / Hunt | Prefabs | String | Scorpion_Big |
| NPC / Hunt | Range | Single | 15 |
| NPC / Hunt | MaxDistance | Single | 60 |
| NPC / Hunt | Behind | Single | 2.5 |
| NPC / Hunt | Speed | Single | 5 |
| NPC / Hunt | Cooldown | Single | 6 |
| NPC / Hunt | DiveSeconds | Single | 1.6 |
| NPC / Hunt | RiseSeconds | Single | 1 |
| NPC / Hunt | FallbackMin | Single | 3 |
| NPC / Hunt | FallbackMax | Single | 5 |
| NPC / Hunt | TravelSpeedFactor | Single | 2 |
| NPC / Hunt | BurrowSlowdown | Single | 2 |
| NPC / Hunt | FxScale | Single | 0.9 |
| NPC / Hunt | FxVolume | Single | 1 |
| NPC / Hunt | FxPitch | Single | 1 |
| NPC / Idle | ReturnDelay | Single | 0 |
| NPC / Idle | ReturnTries | Int32 | 10 |
| NPC / Idle | RetrySeconds | Single | 5 |
| NPC / Idle | WalkRadius | Single | 200 |
| NPC / Movement | FullSpeedAngle | Single | 30 |
| NPC / Movement | SlowestAngle | Single | 120 |
| NPC / Movement | SlowestSpeed | Single | 0.1 |
| NPC / Movement | BlockedRatio | Single | 0.33 |
| NPC / Movement | BlockedSeconds | Single | 0.5 |
| NPC / Movement | BlockedMemory | Single | 1.5 |
| NPC / Nav | BakeRange | Single | 200 |
| NPC / Nav | CellSize | Single | 0.5 |
| NPC / Nav | Margin | Single | 4 |
| NPC / Nav | MaxStep | Single | 0.25 |
| NPC / Nav | BakeBudgetMs | Single | 1 |
| NPC / Nav | FieldSeconds | Single | 1 |
| NPC / Nests | Prefabs | String | Spider_Small,Spider_Big |
| NPC / Nests | Sitters | String | Spider_Big |
| NPC / Nests | BuildRange | Single | 520 |
| NPC / Nests | FreeRange | Single | 750 |
| NPC / Nests | PlaceRadius | Single | 80 |
| NPC / Nests | MaxRadius | Single | 35 |
| NPC / Nests | MinHangingWebs | Int32 | 3 |
| NPC / Nests | MaxHangingWebs | Int32 | 7 |
| NPC / Nests | MinGroundWebs | Int32 | 4 |
| NPC / Nests | MaxGroundWebs | Int32 | 7 |
| NPC / Nests | SpotsTried | Int32 | 70 |
| NPC / Nests | FrameBudgetMs | Single | 1 |
| NPC / Nests | AlarmSpeed | Single | 1.5 |
| NPC / Nests | AlarmSeconds | Single | 20 |
| NPC / Nests | AlarmCooldown | Single | 5 |
| NPC / Nests | SitterReach | Single | 1 |
| NPC / Nests | Log | Boolean | false |
| NPC / NpcAim | AimTimeScale | Single | 50 |
| NPC / NpcAim | FacingTolerance | Single | 8 |
| NPC / NpcAim | EngagePercent | Single | 85 |
| NPC / NpcAim | Magazines | Boolean | true |
| NPC / NpcAim | MagazineTable | String | 22_pipe_pistol=1, 22_pipe_revolver=5, 22_pipe_smg=30, akm_drum=70, akm_trash=30, akms=30, borz_smg=25, crossbow=1, folk_17=17, m16a1=30, redmark_m11=4, redmark_m11_scoped=4, rochester_m24=2, rochester_m24_chopped=2, slamberg_500=5, slamberg_500_chopped=5, slamfire_shotgun=1 |
| NPC / NpcAim | SingleShotReload | Single | 1 |
| NPC / NpcAim | ReloadVolume | Single | 1 |
| NPC / NpcAim | ReloadSoundRange | Single | 35 |
| NPC / NpcAim | RoundSeconds | Single | 0.6 |
| NPC / NpcAim | ReloadSeconds | Single | 2.5 |
| NPC / Pounce | Prefabs | String | Spider_Small |
| NPC / Pounce | ZigzagFromMin | Single | 8 |
| NPC / Pounce | ZigzagFromMax | Single | 11 |
| NPC / Pounce | LeapFromMin | Single | 5 |
| NPC / Pounce | LeapFromMax | Single | 6 |
| NPC / Pounce | ZigzagAngle | Single | 35 |
| NPC / Pounce | ZigzagSeconds | Single | 0.35 |
| NPC / Pounce | ZigzagSpeed | Single | 1.3 |
| NPC / Pounce | LeapSpeed | Single | 10 |
| NPC / Pounce | Pitch | Single | 70 |
| NPC / Pounce | RunOffMin | Single | 1.5 |
| NPC / Pounce | RunOffMax | Single | 2.5 |
| NPC / Pounce | CooldownMin | Single | 4 |
| NPC / Pounce | CooldownMax | Single | 7 |
| NPC / Pounce | Log | Boolean | false |
| NPC / Senses | NightHours | String | 20=100, 21=75, 22=35, 23=0, 4=35, 5=75, 6=100 |
| NPC / Senses | FlankDistance | Single | 15 |
| NPC / Senses | FlankWidth | Single | 10 |
| NPC / Senses | NoticeSeconds | Single | 0.2 |
| NPC / Senses | NoticeFar | Single | 2 |
| NPC / Senses | LoseSeconds | Single | 5 |
| NPC / Senses | Guesses | Int32 | 3 |
| NPC / Senses | GuessRadius | Single | 8 |
| NPC / Senses | Trackers | String | Hound,Grimhound,Pup,Nightwalker,Spider,Arachnid,Scorpion |
| NPC / Senses | TauntMin | Single | 12 |
| NPC / Senses | TauntMax | Single | 25 |
| NPC / Senses | GunshotResponders | Int32 | 4 |
| NPC / Senses | GunshotNearRange | Single | 25 |
| NPC / Senses | SearchSeconds | Single | 30 |
| NPC / Senses | GhostTimeout | Single | 60 |
| NPC / Senses | ArriveDistance | Single | 1.5 |
| NPC / Senses | LookInterval | Single | 0.15 |
| NPC / Senses | ShotRangePistol | Single | 80 |
| NPC / Senses | ShotRangeSmg | Single | 120 |
| NPC / Senses | ShotRangeRifle | Single | 150 |
| NPC / Senses | ShotRangeSniper | Single | 150 |
| NPC / Senses | ShotRangeShotgun | Single | 150 |
| NPC / Senses | ShotRangeCrossbow | Single | 15 |
| NPC / Senses | NpcShotRanges | String | Flexa=150, Gungirl=150, Lugnut=150, Scrud=150, Boltjaw=120, Sprokka=80, Shoota=80, Pistoleer=80, Gunnar=150, Lugger=150 |
| NPC / Senses | AllClearRange | Single | 30 |
| NPC / Senses | TauntRange | Single | 15 |
| NPC / Senses | ShoutBlocksButtons | String | Kick,ShiftDown |
| NPC / Senses | PlayerShoutRange | Single | 25 |
| NPC / Senses | ShoutCooldown | Single | 1.5 |
| NPC / Senses | StormSight | Single | 80 |
| NPC / Senses | StormHearing | Single | 50 |
| NPC / Senses | StormRadius | Single | 2500 |
| NPC / Senses | ShoutVolume | Single | 1 |
| NPC / Senses | HumanFactions | String | Scrapyard,Coyotes |
| NPC / Senses | EngineMinRange | Single | 50 |
| NPC / Senses | EngineMaxRange | Single | 150 |
| NPC / Senses | EngineMinHp | Single | 40 |
| NPC / Senses | EngineMaxHp | Single | 300 |
| NPC / Senses | EngineIdleFactor | Single | 50 |
| NPC / Senses | BailOutAware | Boolean | true |
| NPC / Senses | BailOutAwareRange | Single | 150 |
| NPC / Senses | ExplosionRange | Single | 150 |
| NPC / Senses | BlastRange | Single | 150 |
| NPC / Senses | BlastPrefabs | String | Explosion_Grenade,Explosion_Can,Explosion_BlastRat,Explosion_BlastZombie |
| NPC / Senses | ThrowRange | Single | 10 |
| Patrols / AI | Throttle | Single | 1 |
| Patrols / AI | DriveByOffset | Single | 5 |
| Patrols / AI | LeadTime | Single | 1 |
| Patrols / AI | CommitSeconds | Single | 0.5 |
| Patrols / AI | SteerRate | Single | 1.5 |
| Patrols / AI | SteerAngle | Single | 30 |
| Patrols / AI | MaxSteerAtSpeed | Single | 0.35 |
| Patrols / AI | TurnSafeSpeed | Single | 18 |
| Patrols / AI | RamDistance | Single | 15 |
| Patrols / AI | PassWidth | Single | 12 |
| Patrols / AI | RunOutMeters | Single | 30 |
| Patrols / AI | RunOutMaxSeconds | Single | 4 |
| Patrols / AI | ReverseSeconds | Single | 2 |
| Patrols / AI | ReverseThrottle | Single | 0.6 |
| Patrols / AI | StuckSeconds | Single | 2 |
| Patrols / AI | RecoverWindow | Single | 30 |
| Patrols / AI | MaxRecovers | Single | 2 |
| Patrols / AI | WaitSeconds | Single | 4 |
| Patrols / AI | FeelerRange | Single | 10 |
| Patrols / AI | FeelerSpeedFactor | Single | 0.6 |
| Patrols / AI | FrontOffset | Single | 2 |
| Patrols / AI | MaxSlopeDeg | Single | 35 |
| Patrols / AI | AvoidGain | Single | 1.2 |
| Patrols / AI | IgnoreMassBelow | Single | 40 |
| Patrols / AI | GiveUpDistance | Single | 700 |
| Patrols / AI | InvertSteering | Boolean | false |
| Patrols / Audio | AudioVoices | Int32 | 64 |
| Patrols / Cleanup | RemoveAfterMinutes | Single | 10 |
| Patrols / Cleanup | MaxCars | Int32 | 30 |
| Patrols / Cleanup | MinDistance | Single | 800 |
| Patrols / Combat tuning | FireArcHalfAngle | Single | 100 |
| Patrols / Combat tuning | MaxAimPitch | Single | 35 |
| Patrols / Combat tuning | AimTurnSpeed | Single | 180 |
| Patrols / Combat tuning | FireBurstSeconds | Single | 3 |
| Patrols / Combat tuning | FireIntervalMin | Single | 8 |
| Patrols / Combat tuning | FireIntervalMax | Single | 20 |
| Patrols / Combat tuning | RamDamageMultiplier | Single | 1 |
| Patrols / Combat tuning | RamDamageByBody | String | Junker=50, Rust*=70, Scrapwagon=70, PigPen=50 |
| Patrols / Combat tuning | RamFullSpeedKmh | Single | 30 |
| Patrols / Combat tuning | RamPushStrength | Single | 1 |
| Patrols / Combat tuning | RamInCarFactor | Single | 1 |
| Patrols / Combat tuning | RiderRange | Single | 35 |
| Patrols / Combat tuning | RiderMaxThrowAngle | Single | 20 |
| Patrols / Combat tuning | RiderJumpRange | Single | 15 |
| Patrols / Combat tuning | RiderIntervalMin | Single | 5 |
| Patrols / Combat tuning | RiderIntervalMax | Single | 9 |
| Patrols / Combat tuning | RiderLeapReachPercent | Single | 80 |
| Patrols / Combat tuning | RiderBailRange | Single | 6 |
| Patrols / Combat tuning | SuspensionLiftCenterOfMass | Boolean | true |
| Patrols / Combat tuning | RiderLeapSpeed | Single | 11 |
| Patrols / Combat tuning | RiderBlastDamage | Single | 60 |
| Patrols / Combat tuning | RiderBlastRadius | Single | 7 |
| Patrols / Combat tuning | RiderLanceSpeed | Single | 30 |
| Patrols / Debug | TurretSpotEditor | Boolean | false |
| Patrols / Debug | Bus passenger pose editor | Boolean | false |
| Patrols / Debug | Merchant item editor | Boolean | false |
| Patrols / Debug | AllowDebugSpawns | Boolean | true |
| Patrols / Debug | AiOverlay | Boolean | false |
| Patrols / Debug | Hide missing-FSM warnings | Boolean | true |
| Patrols / Debug | ExitSpeedKmh | Single | 30 |
| Patrols / Driving | StuckPedalChance | Single | 5 |
| Patrols / Driving | BailChance | Single | 25 |
| Patrols / Driving | StuckBailChance | Single | 100 |
| Patrols / Loot | MinPartsFill | Single | 15 |
| Patrols / Loot | MaxPartsFill | Single | 60 |
| Patrols / Pose | OffsetX | Single | 0 |
| Patrols / Pose | OffsetY | Single | -0.3 |
| Patrols / Pose | OffsetZ | Single | 0 |
| Patrols / Pose | Enabled | Boolean | true |
| Patrols / Pose | ThighAngle | Single | 85 |
| Patrols / Pose | KneeAngle | Single | 30 |
| Patrols / Pose | ArmAngle | Single | 65 |
| Patrols / Pose | LeftLegCloser | Single | 10 |
| Patrols / Pose | RightLegCloser | Single | 10 |
| Patrols / Pose | LeftArmCloser | Single | 10 |
| Patrols / Pose | RightArmCloser | Single | 10 |
| Patrols / Pose | BikeArmAngle | Single | 55 |
| Patrols / Pose | BikeArmSpread | Single | 22 |
| Patrols / Pose | ElbowAngle | Single | 25 |
| Patrols / Pose.Boltjaw | LeftArmVertical | Single | 80 |
| Patrols / Pose.Boltjaw | LeftArmHorizontal | Single | 35 |
| Patrols / Pose.Boltjaw | LeftArmRotation | Single | -15.6 |
| Patrols / Pose.Boltjaw | LeftElbowVertical | Single | 25 |
| Patrols / Pose.Boltjaw | LeftElbowHorizontal | Single | 0 |
| Patrols / Pose.Boltjaw | LeftElbowRotation | Single | 0 |
| Patrols / Pose.Boltjaw | LeftHandVertical | Single | 0 |
| Patrols / Pose.Boltjaw | LeftHandHorizontal | Single | 0 |
| Patrols / Pose.Boltjaw | LeftHandRotation | Single | 0 |
| Patrols / Pose.Boltjaw | RightArmVertical | Single | -20 |
| Patrols / Pose.Boltjaw | RightArmHorizontal | Single | 50 |
| Patrols / Pose.Boltjaw | RightArmRotation | Single | -20 |
| Patrols / Pose.Boltjaw | RightElbowVertical | Single | 125 |
| Patrols / Pose.Boltjaw | RightElbowHorizontal | Single | 0 |
| Patrols / Pose.Boltjaw | RightElbowRotation | Single | 25 |
| Patrols / Pose.Boltjaw | RightHandVertical | Single | 0 |
| Patrols / Pose.Boltjaw | RightHandHorizontal | Single | 25 |
| Patrols / Pose.Boltjaw | RightHandRotation | Single | -25 |
| Patrols / Pose.Boltjaw | WeaponPositionX | Single | 0 |
| Patrols / Pose.Boltjaw | WeaponPositionY | Single | 0 |
| Patrols / Pose.Boltjaw | WeaponPositionZ | Single | 0 |
| Patrols / Pose.Boltjaw | WeaponRotationX | Single | -15 |
| Patrols / Pose.Boltjaw | WeaponRotationY | Single | 20 |
| Patrols / Pose.Boltjaw | WeaponRotationZ | Single | 0 |
| Patrols / Pose.Flexa | LeftArmVertical | Single | 80 |
| Patrols / Pose.Flexa | LeftArmHorizontal | Single | 35 |
| Patrols / Pose.Flexa | LeftArmRotation | Single | -15.652 |
| Patrols / Pose.Flexa | LeftElbowVertical | Single | 25 |
| Patrols / Pose.Flexa | LeftElbowHorizontal | Single | 0 |
| Patrols / Pose.Flexa | LeftElbowRotation | Single | 0 |
| Patrols / Pose.Flexa | LeftHandVertical | Single | 0 |
| Patrols / Pose.Flexa | LeftHandHorizontal | Single | 0 |
| Patrols / Pose.Flexa | LeftHandRotation | Single | 0 |
| Patrols / Pose.Flexa | RightArmVertical | Single | 5 |
| Patrols / Pose.Flexa | RightArmHorizontal | Single | -10 |
| Patrols / Pose.Flexa | RightArmRotation | Single | 0 |
| Patrols / Pose.Flexa | RightElbowVertical | Single | 120 |
| Patrols / Pose.Flexa | RightElbowHorizontal | Single | 0 |
| Patrols / Pose.Flexa | RightElbowRotation | Single | 0 |
| Patrols / Pose.Flexa | RightHandVertical | Single | 0 |
| Patrols / Pose.Flexa | RightHandHorizontal | Single | 25 |
| Patrols / Pose.Flexa | RightHandRotation | Single | -45 |
| Patrols / Pose.Flexa | WeaponPositionX | Single | 0 |
| Patrols / Pose.Flexa | WeaponPositionY | Single | 0 |
| Patrols / Pose.Flexa | WeaponPositionZ | Single | 0 |
| Patrols / Pose.Flexa | WeaponRotationX | Single | -15 |
| Patrols / Pose.Flexa | WeaponRotationY | Single | 20 |
| Patrols / Pose.Flexa | WeaponRotationZ | Single | 0 |
| Patrols / Pose.Lugnut | LeftArmVertical | Single | 85 |
| Patrols / Pose.Lugnut | LeftArmHorizontal | Single | 34 |
| Patrols / Pose.Lugnut | LeftArmRotation | Single | 0 |
| Patrols / Pose.Lugnut | LeftElbowVertical | Single | 0 |
| Patrols / Pose.Lugnut | LeftElbowHorizontal | Single | 0 |
| Patrols / Pose.Lugnut | LeftElbowRotation | Single | 0 |
| Patrols / Pose.Lugnut | LeftHandVertical | Single | 5 |
| Patrols / Pose.Lugnut | LeftHandHorizontal | Single | 15 |
| Patrols / Pose.Lugnut | LeftHandRotation | Single | -10 |
| Patrols / Pose.Lugnut | RightArmVertical | Single | 80 |
| Patrols / Pose.Lugnut | RightArmHorizontal | Single | 60 |
| Patrols / Pose.Lugnut | RightArmRotation | Single | 20 |
| Patrols / Pose.Lugnut | RightElbowVertical | Single | 20 |
| Patrols / Pose.Lugnut | RightElbowHorizontal | Single | -140 |
| Patrols / Pose.Lugnut | RightElbowRotation | Single | 0 |
| Patrols / Pose.Lugnut | RightHandVertical | Single | -40 |
| Patrols / Pose.Lugnut | RightHandHorizontal | Single | 50 |
| Patrols / Pose.Lugnut | RightHandRotation | Single | 0 |
| Patrols / Pose.Lugnut | WeaponPositionX | Single | 0 |
| Patrols / Pose.Lugnut | WeaponPositionY | Single | -0.1 |
| Patrols / Pose.Lugnut | WeaponPositionZ | Single | 0 |
| Patrols / Pose.Lugnut | WeaponRotationX | Single | -15 |
| Patrols / Pose.Lugnut | WeaponRotationY | Single | 0 |
| Patrols / Pose.Lugnut | WeaponRotationZ | Single | 0 |
| Patrols / Pose.Scraffa | LeftArmVertical | Single | 65 |
| Patrols / Pose.Scraffa | LeftArmHorizontal | Single | 10 |
| Patrols / Pose.Scraffa | LeftArmRotation | Single | 0 |
| Patrols / Pose.Scraffa | LeftElbowVertical | Single | 25 |
| Patrols / Pose.Scraffa | LeftElbowHorizontal | Single | 0 |
| Patrols / Pose.Scraffa | LeftElbowRotation | Single | 0 |
| Patrols / Pose.Scraffa | LeftHandVertical | Single | 0 |
| Patrols / Pose.Scraffa | LeftHandHorizontal | Single | 0 |
| Patrols / Pose.Scraffa | LeftHandRotation | Single | 0 |
| Patrols / Pose.Scraffa | RightArmVertical | Single | 65 |
| Patrols / Pose.Scraffa | RightArmHorizontal | Single | -10 |
| Patrols / Pose.Scraffa | RightArmRotation | Single | 0 |
| Patrols / Pose.Scraffa | RightElbowVertical | Single | 25 |
| Patrols / Pose.Scraffa | RightElbowHorizontal | Single | 0 |
| Patrols / Pose.Scraffa | RightElbowRotation | Single | 0 |
| Patrols / Pose.Scraffa | RightHandVertical | Single | 0 |
| Patrols / Pose.Scraffa | RightHandHorizontal | Single | 0 |
| Patrols / Pose.Scraffa | RightHandRotation | Single | 0 |
| Patrols / Pose.Scrud | LeftArmVertical | Single | 78 |
| Patrols / Pose.Scrud | LeftArmHorizontal | Single | 20 |
| Patrols / Pose.Scrud | LeftArmRotation | Single | 30 |
| Patrols / Pose.Scrud | LeftElbowVertical | Single | 25 |
| Patrols / Pose.Scrud | LeftElbowHorizontal | Single | 0 |
| Patrols / Pose.Scrud | LeftElbowRotation | Single | 0 |
| Patrols / Pose.Scrud | LeftHandVertical | Single | 0 |
| Patrols / Pose.Scrud | LeftHandHorizontal | Single | 0 |
| Patrols / Pose.Scrud | LeftHandRotation | Single | 0 |
| Patrols / Pose.Scrud | RightArmVertical | Single | 75 |
| Patrols / Pose.Scrud | RightArmHorizontal | Single | -20 |
| Patrols / Pose.Scrud | RightArmRotation | Single | -30 |
| Patrols / Pose.Scrud | RightElbowVertical | Single | 25 |
| Patrols / Pose.Scrud | RightElbowHorizontal | Single | 0 |
| Patrols / Pose.Scrud | RightElbowRotation | Single | 0 |
| Patrols / Pose.Scrud | RightHandVertical | Single | 0 |
| Patrols / Pose.Scrud | RightHandHorizontal | Single | 0 |
| Patrols / Pose.Scrud | RightHandRotation | Single | 0 |
| Patrols / Pose.Scrud | WeaponPositionX | Single | 0 |
| Patrols / Pose.Scrud | WeaponPositionY | Single | 0 |
| Patrols / Pose.Scrud | WeaponPositionZ | Single | 0 |
| Patrols / Pose.Scrud | WeaponRotationX | Single | -25 |
| Patrols / Pose.Scrud | WeaponRotationY | Single | 35 |
| Patrols / Pose.Scrud | WeaponRotationZ | Single | 0 |
| Patrols / Pose.Spanna | LeftArmVertical | Single | 65 |
| Patrols / Pose.Spanna | LeftArmHorizontal | Single | 10 |
| Patrols / Pose.Spanna | LeftArmRotation | Single | 0 |
| Patrols / Pose.Spanna | LeftElbowVertical | Single | 25 |
| Patrols / Pose.Spanna | LeftElbowHorizontal | Single | 0 |
| Patrols / Pose.Spanna | LeftElbowRotation | Single | 0 |
| Patrols / Pose.Spanna | LeftHandVertical | Single | 0 |
| Patrols / Pose.Spanna | LeftHandHorizontal | Single | 0 |
| Patrols / Pose.Spanna | LeftHandRotation | Single | 0 |
| Patrols / Pose.Spanna | RightArmVertical | Single | 65 |
| Patrols / Pose.Spanna | RightArmHorizontal | Single | -10 |
| Patrols / Pose.Spanna | RightArmRotation | Single | 0 |
| Patrols / Pose.Spanna | RightElbowVertical | Single | 25 |
| Patrols / Pose.Spanna | RightElbowHorizontal | Single | 0 |
| Patrols / Pose.Spanna | RightElbowRotation | Single | 0 |
| Patrols / Pose.Spanna | RightHandVertical | Single | 0 |
| Patrols / Pose.Spanna | RightHandHorizontal | Single | 0 |
| Patrols / Pose.Spanna | RightHandRotation | Single | 0 |
| Patrols / Pose.Sprokka | LeftArmVertical | Single | 105 |
| Patrols / Pose.Sprokka | LeftArmHorizontal | Single | 10 |
| Patrols / Pose.Sprokka | LeftArmRotation | Single | 0 |
| Patrols / Pose.Sprokka | LeftElbowVertical | Single | 0 |
| Patrols / Pose.Sprokka | LeftElbowHorizontal | Single | 0 |
| Patrols / Pose.Sprokka | LeftElbowRotation | Single | 0 |
| Patrols / Pose.Sprokka | LeftHandVertical | Single | 0 |
| Patrols / Pose.Sprokka | LeftHandHorizontal | Single | 0 |
| Patrols / Pose.Sprokka | LeftHandRotation | Single | 0 |
| Patrols / Pose.Sprokka | RightArmVertical | Single | 15 |
| Patrols / Pose.Sprokka | RightArmHorizontal | Single | -27 |
| Patrols / Pose.Sprokka | RightArmRotation | Single | -30 |
| Patrols / Pose.Sprokka | RightElbowVertical | Single | 25 |
| Patrols / Pose.Sprokka | RightElbowHorizontal | Single | 0 |
| Patrols / Pose.Sprokka | RightElbowRotation | Single | 0 |
| Patrols / Pose.Sprokka | RightHandVertical | Single | 0 |
| Patrols / Pose.Sprokka | RightHandHorizontal | Single | 0 |
| Patrols / Pose.Sprokka | RightHandRotation | Single | 0 |
| Patrols / Pose.Sprokka | WeaponPositionX | Single | 0 |
| Patrols / Pose.Sprokka | WeaponPositionY | Single | 0 |
| Patrols / Pose.Sprokka | WeaponPositionZ | Single | 0.03 |
| Patrols / Pose.Sprokka | WeaponRotationX | Single | 0 |
| Patrols / Pose.Sprokka | WeaponRotationY | Single | 15 |
| Patrols / Pose.Sprokka | WeaponRotationZ | Single | 0 |
| Patrols / Spawner | SpawnDistance | Single | 350 |
| Patrols / Traffic tuning | DebugMap | Boolean | true |
| Patrols / Traffic tuning | Real patrol names | Boolean | true |
| Patrols / Traffic tuning | hide route type | Boolean | false |
| Patrols / Traffic tuning | FollowRoads | Boolean | true |
| Patrols / Traffic tuning | MaxRoadConnectorMetres | Single | 350 |
| Patrols / Traffic tuning | MaxGroups | Int32 | 24 |
| Patrols / Traffic tuning | MaxRealCars | Int32 | 24 |
| Patrols / Traffic tuning | LoadRange | Single | 650 |
| Patrols / Traffic tuning | UnloadRange | Single | 1000 |
| Patrols / Traffic tuning | DestinationGraceSeconds | Single | 180 |
| Patrols / Traffic tuning | ConvoyGuardRange | Single | 300 |
| Patrols / Traffic tuning | PatrolMemorySeconds | Single | 20 |
| Patrols / Turret pose | TurretThigh | Single | 80 |
| Patrols / Turret pose | TurretKnee | Single | 130 |
| Patrols / Turret pose | TurretHipHeight | Single | 0.4 |
| Patrols / Turret pose | RiderLanceOffset | String | 0, 0, 0 |
| Patrols / Turret pose | RiderLanceRotation | String | 0, 0, 0 |
| PlayerBody / Animation | JumpClipStart | Single | 0.2 |
| PlayerBody / Animation | StrikeWindup | Single | 0.08 |
| PlayerBody / Animation | HipsDrift | Single | 0.25 |
| PlayerBody / Animation | BundleWalkSpeed | Single | 1.4 |
| PlayerBody / Animation | BundleRunSpeed | Single | 3.8 |
| PlayerBody / Animation | BundleCrouchSpeed | Single | 1 |
| PlayerBody / Animation | BundleSprintSpeed | Single | 5.5 |
| PlayerBody / Animation | RunFrom | Single | 3.2 |
| PlayerBody / Animation | SprintFrom | Single | 4.3 |
| PlayerBody / Animation | UpperPhase | Single | 0 |
| PlayerBody / Animation | TurnClipAngle | Single | 70 |
| PlayerBody / Animation | TurnStartRate | Single | 8 |
| PlayerBody / Animation | PumpClipSpeed | Single | 2 |
| PlayerBody / Animation | DirBlendSpeed | Single | 360 |
| PlayerBody / Animation | FootIK | Boolean | true |
| PlayerBody / Animation | RunLegsIn | Single | 10 |
| PlayerBody / Animation | WalkToStop | Boolean | true |
| PlayerBody / Animation | RunLegsTurn | Single | 75 |
| PlayerBody / Animation | CrouchDrop | Single | 0.42 |
| PlayerBody / Animation | AimPitch | Single | 0.9 |
| PlayerBody / Animation (fallback clips) | IdleClip | String | merchant_idle |
| PlayerBody / Animation (fallback clips) | RunClip | String | enemy_1_run |
| PlayerBody / Animation (fallback clips) | RifleClip | String | enemy_machinegun |
| PlayerBody / Animation (fallback clips) | PistolClip | String | sprokka_shoot |
| PlayerBody / Animation (fallback clips) | MeleeClip | String | enemy_1_attack |
| PlayerBody / Animation (fallback clips) | RunClipSpeed | Single | 4.5 |
| PlayerBody / Animation (fallback clips) | WalkStride | Single | 1.25 |
| PlayerBody / Animation (fallback clips) | ThighSwing | Single | 26 |
| PlayerBody / Animation (fallback clips) | KneeBend | Single | 38 |
| PlayerBody / Animation (fallback clips) | ArmSwing | Single | 16 |
| PlayerBody / Animation (fallback clips) | HipBob | Single | 0.025 |
| PlayerBody / Animation (fallback clips) | RightHanded | Boolean | true |
| PlayerBody / Camera | ThirdPersonDistance | Single | 3.26 |
| PlayerBody / Camera | ThirdPersonCarDistance | Single | 5.38 |
| PlayerBody / Camera | ThirdHeight | Single | 0.25 |
| PlayerBody / Camera | ThirdShoulder | Single | 0.45 |
| PlayerBody / Camera | ThirdCarHeight | Single | 1.1 |
| PlayerBody / Camera | OrbitSpeed | Single | 3 |
| PlayerBody / Camera | EnableMMB | Boolean | false |
| PlayerBody / Camera | AimDownSightsTime | Single | 0.2 |
| PlayerBody / Camera | PickAssistRadius | Single | 0.06 |
| PlayerBody / Camera culling | OcclusionOpacity | Single | 0.2 |
| PlayerBody / Camera culling | OcclusionRadius | Single | 0.55 |
| PlayerBody / Car seat | FirstPersonDrivingX | Single | 0 |
| PlayerBody / Car seat | FirstPersonDrivingY | Single | 0 |
| PlayerBody / Car seat | FirstPersonDrivingZ | Single | 0 |
| PlayerBody / Car seat | CarCameraForward | Single | 0.12 |
| PlayerBody / Car seat | SeatDrop | Single | 0.05 |
| PlayerBody / Climbing | MinimumHeight | Single | 0.45 |
| PlayerBody / Climbing | MaximumHeight | Single | 2.5 |
| PlayerBody / Climbing | Reach | Single | 0.85 |
| PlayerBody / Climbing | WaistHeightLimit | Single | 1.25 |
| PlayerBody / Climbing | AnimationSpeed | Single | 1.5 |
| PlayerBody / Editors | Pose key: Select weapon / arm | String | Keypad0 |
| PlayerBody / Editors | Pose key: Move / rotate | String | Keypad5 |
| PlayerBody / Editors | Pose key: Up / pitch up | String | Keypad8 |
| PlayerBody / Editors | Pose key: Down / pitch down | String | Keypad2 |
| PlayerBody / Editors | Pose key: Right / yaw right | String | Keypad6 |
| PlayerBody / Editors | Pose key: Left / yaw left | String | Keypad4 |
| PlayerBody / Editors | Pose key: Forward / roll right | String | Keypad7 |
| PlayerBody / Editors | Pose key: Back / roll left | String | Keypad1 |
| PlayerBody / Editors | WeaponAdjustment | Boolean | false |
| PlayerBody / First person | FirstPersonBodyX | Single | 0 |
| PlayerBody / First person | FirstPersonBodyY | Single | 0 |
| PlayerBody / First person | FirstPersonBodyZ | Single | 0 |
| PlayerBody / First person | FirstPersonBodyBack | Single | 0.08 |
| PlayerBody / First person | BodyBackDown | Single | 0.08 |
| PlayerBody / First person | FirstPersonWeaponBack | Single | 0.08 |
| PlayerBody / First person | FirstPersonChestLean | Single | 20 |
| PlayerBody / ModAPI | VerboseModApi | Boolean | false |
| PlayerBody / Model | Model | String | Models/Player_female.glb |
| PlayerBody / Model | Texture | String | Models/Player_female.png |
| PlayerBody / Model | ArmsTexture | String | Models/Player2_female_arms.png |
| PlayerBody / Model | AnimationBundle | String | Models/apocaplayer_anims.bundle |
