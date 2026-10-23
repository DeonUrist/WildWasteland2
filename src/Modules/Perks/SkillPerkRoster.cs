using WildWasteland.Core;

namespace WildWasteland.Modules.Perks
{
    internal static class SkillPerkRoster
    {
        internal struct Entry
        {
            internal PlayerSkillId Skill;
            internal int Level;
            internal string Id, Name, Hint;
            internal Entry(PlayerSkillId skill, int level, string id, string name, string hint)
            { Skill = skill; Level = level; Id = id; Name = name; Hint = hint; }
        }
        internal static readonly Entry[] All = {
            new Entry(PlayerSkillId.Unarmed,25,"Pushback","Pushback","Kicks push enemies backward."),
            new Entry(PlayerSkillId.Unarmed,50,"Stagger","Stagger","Punches and kicks have a 10% chance to stun humanoids, rats and quadrupeds."),
            new Entry(PlayerSkillId.Unarmed,75,"OnePunch","One Punch","Full-health targets take double unarmed damage. Stun chance becomes 20% and includes large mutants and bosses."),
            new Entry(PlayerSkillId.Unarmed,100,"ParalysisStrike","Paralysis Strike","Unarmed stun is replaced by a 25% chance to ragdoll eligible targets for 2-3 seconds."),
            new Entry(PlayerSkillId.Melee,25,"MeleeProficiency","Melee Proficiency","Cutting hits bleed for three seconds: three ticks each worth one third of the hit. Hits refresh bleeding. Blunt hits have a 20% chance to stun humans and mutants."),
            new Entry(PlayerSkillId.Melee,50,"Parry","Parry","While holding a melee weapon, incoming melee attacks have (Melee skill / 2)% chance to deal no damage."),
            new Entry(PlayerSkillId.Melee,75,"Executioner","Executioner","Melee hits execute enemies already at or below 25% maximum health."),
            new Entry(PlayerSkillId.Melee,100,"Undertaker","Undertaker","Melee kills frighten nearby witnesses. Chainsaw witnesses crouch at their destination for twice the normal fear duration."),
            new Entry(PlayerSkillId.Pistols,25,"Gunslinger","Gunslinger","Pistols reload 50% faster."),
            new Entry(PlayerSkillId.Pistols,50,"Deadeye","Deadeye","Pistols have a 25% chance of double damage, or 50% on headshots."),
            new Entry(PlayerSkillId.Pistols,75,"QuickReflexes","Quick Reflexes","Pistols fire 1.5 times as fast."),
            new Entry(PlayerSkillId.Pistols,100,"HighNoon","High Noon","Pistol headshots ragdoll small enemies, humanoids and quadrupeds; large mutants: 50%; juggernauts: 10%."),
            new Entry(PlayerSkillId.Smgs,25,"RunAndGun","Run & Gun","SMG spread is reduced a further 25% while moving."),
            new Entry(PlayerSkillId.Smgs,50,"CloseQuarters","Close Quarters","SMGs deal up to 1.5 times damage at point-blank range, falling smoothly to normal at effective range."),
            new Entry(PlayerSkillId.Smgs,75,"BulletHose","Bullet Hose","Each successful SMG hit has a 15% chance to return one round to the current magazine, capped at capacity."),
            new Entry(PlayerSkillId.Smgs,100,"AdrenalineRush","Adrenaline Rush","SMG kills increase firing and reload speed by 50% for five seconds. Further kills refresh the duration."),
            new Entry(PlayerSkillId.Rifles,25,"Marksman","Marksman","Rifles and crossbows have an additional 25% spread reduction through iron sights."),
            new Entry(PlayerSkillId.Rifles,50,"LongShot","Long Shot","Rifle, sniper and crossbow effective range increases by 25%."),
            new Entry(PlayerSkillId.Rifles,75,"Sniper","Assassin","The first rifle or crossbow shot against an unaware enemy deals triple damage."),
            new Entry(PlayerSkillId.Rifles,100,"UrbanWarrior","Urban Warrior","Rifle and crossbow kills reveal enemies within 200 metres with yellow directional arrows for five seconds."),
            new Entry(PlayerSkillId.Shotguns,25,"BigBlast","Big Blast","Shotguns push enemies. Stun chance is blast damage / pre-hit health, capped at 100%, once per enemy per blast."),
            new Entry(PlayerSkillId.Shotguns,50,"FastPump","Fast Pump","Shotguns pump and reload 50% faster."),
            new Entry(PlayerSkillId.Shotguns,75,"CrowdControl","Crowd Control","When a blast hits two or more enemies, each receives the sum of all pellets that hit those enemies. Damage is applied once per enemy."),
            new Entry(PlayerSkillId.Shotguns,100,"Boomstick","This is my boomstick","Shotgun stun becomes ragdoll plus pushback. Chance is twice blast damage / pre-hit health, capped at 100%."),
            new Entry(PlayerSkillId.Athletics,25,"Parkour","Parkour","Ladder and ledge climbing are 1.5 times as fast."),
            new Entry(PlayerSkillId.Athletics,50,"Dodge","Dodge","Incoming damage is reduced by 25% while moving on foot."),
            new Entry(PlayerSkillId.Athletics,75,"Paintrain","Paintrain","Sprinting into humanoids ragdolls them. Continuous contact cannot trigger repeated knockdowns."),
            new Entry(PlayerSkillId.Athletics,100,"Unstoppable","Unstoppable","Sprint in any direction using normal sprint speed and stamina rules."),
            new Entry(PlayerSkillId.Barter,25,"FriendlyFace","Friendly Face","Coyotes forgive player attacks sooner, while retaining immediate retaliation."),
            new Entry(PlayerSkillId.Barter,50,"MasterTrader","Salesman","Merchant wanted items include an eligible high-value item from your trunk when one is available."),
            new Entry(PlayerSkillId.Barter,75,"Charismatic","Charismatic","Companion hiring prices are reduced by 50%, both in dialogue and when paid."),
            new Entry(PlayerSkillId.Barter,100,"MerchantReroll","Master Trader","Reroll stock and wanted items once each per merchant per player. Both uses are saved independently."),
            new Entry(PlayerSkillId.Survival,25,"CleanCut","Clean Cut","Butchering a creature has a 25% chance to yield one extra meat."),
            new Entry(PlayerSkillId.Survival,50,"OnlyBestParts","Only Best Parts","Food you last handled and cooked gains 25% less insanity, after normal cooking calculations."),
            new Entry(PlayerSkillId.Survival,75,"Fasting","Fasting","Hunger and thirst drain 1.5 times more slowly."),
            new Entry(PlayerSkillId.Survival,100,"StrongMind","Strong Mind","Persevere adds 50 hunger, thirst and fatigue instead of setting them to 100/100/80. Stat caps still apply.")
        };
    }
}
