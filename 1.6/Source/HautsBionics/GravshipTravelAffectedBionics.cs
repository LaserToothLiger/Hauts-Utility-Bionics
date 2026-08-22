using HautsFramework;
using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using Verse.Sound;

namespace HautsBionics
{
    //could've also been a DME, but it not being one slightly improves the performance of effects that iterate thru a hediff def's extensions. See the Harmony patch HVBLandingEndedPrefix for what these two classes do
    public class Hediff_ImplantGravNausea : Hediff_Implant
    {

    }
    public class GravitonPart : DefModExtension
    {
        public GravitonPart()
        {
        }
        public HediffDef hediff;
        public float severityMultiplier = 1f;
        public bool addToSameBodyPart = true;
    }
    /*at max severity (which happens as a gravship passenger when it lands, due to GravitonPart), lowers all the pawn's ability cooldowns by lowerAllCooldownsBy.
     * Its Anomaly voidshard equivalent provides a stronger cooldown offset as anomalous activity level rises, which you can see with the other three fields*/
    public class HediffCompProperties_AbilityRefresher : HediffCompProperties
    {
        public HediffCompProperties_AbilityRefresher()
        {
            this.compClass = typeof(HediffComp_AbilityRefresher);
        }
        public int lowerAllCooldownsBy;
        public bool multiplyByAnomalyActivityLevel;
        public float levelForAmbientHorror = 2;
        public Dictionary<int, float> levelAtEachLevel;
    }
    public class HediffComp_AbilityRefresher : HediffComp
    {
        public HediffCompProperties_AbilityRefresher Props
        {
            get
            {
                return (HediffCompProperties_AbilityRefresher)this.props;
            }
        }
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (this.parent.Severity == this.parent.def.maxSeverity)
            {
                int lowerCDsBy = this.Props.lowerAllCooldownsBy;
                if (ModsConfig.AnomalyActive && this.Props.multiplyByAnomalyActivityLevel)
                {
                    if (Find.Storyteller.difficulty.AnomalyPlaystyleDef == DefDatabase<AnomalyPlaystyleDef>.GetNamedSilentFail("AmbientHorror"))
                    {
                        lowerCDsBy = (int)(lowerCDsBy * this.Props.levelForAmbientHorror);
                    }
                    else if (this.Props.levelAtEachLevel != null)
                    {
                        lowerCDsBy = (int)(lowerCDsBy * Math.Max(1, this.Props.levelAtEachLevel.TryGetValue(Find.Anomaly.Level, 1)));
                    }
                }
                if (this.Pawn.abilities != null)
                {
                    List<AbilityGroupDef> agd = new List<AbilityGroupDef>();
                    foreach (RimWorld.Ability a in this.Pawn.abilities.AllAbilitiesForReading)
                    {
                        if (a.OnCooldown)
                        {
                            if (a.def.groupDef != null)
                            {
                                if (!agd.Contains(a.def.groupDef))
                                {
                                    agd.Add(a.def.groupDef);
                                    AbilityCooldownModifierUtility.SetNewCooldown(a, a.CooldownTicksRemaining - lowerCDsBy);
                                }
                            }
                            else
                            {
                                AbilityCooldownModifierUtility.SetNewCooldown(a, a.CooldownTicksRemaining - lowerCDsBy);
                            }
                        }
                    }
                }
                VEF.Abilities.CompAbilities comp = this.Pawn.GetComp<VEF.Abilities.CompAbilities>();
                if (comp != null && !comp.LearnedAbilities.NullOrEmpty())
                {
                    foreach (VEF.Abilities.Ability ab in comp.LearnedAbilities)
                    {
                        if (ab.cooldown > Find.TickManager.TicksGame)
                        {
                            ab.cooldown -= lowerCDsBy;
                        }
                    }
                }
                this.parent.Severity = this.parent.def.minSeverity;
            }
        }
    }
    /*periodically scans around self to either unleash an explosion with that radius (not hurting self) or just harm all hostile pawns and turrets in the radius. Spends severityCost to do so, and plays the sound and fleck.
     * periodicity: how many ticks in between each scan
     * radius: scan radius, AND explosion radius, AND hostile-hurting radius
     * damageType, damageAmount: ewisott
     * damagePerLevel: used for the voidshard equivalent, adds [anomalous activity level * this value] to damage
     * indiscriminateExplosion: governs whether it's the explosion or the hostile-targeted damage*/
    public class HediffCompProperties_GraviticRepulsion : HediffCompProperties
    {
        public HediffCompProperties_GraviticRepulsion()
        {
            this.compClass = typeof(HediffComp_GraviticRepulsion);
        }
        public int periodicity;
        public float radius;
        public float severityCost;
        public DamageDef damageType;
        public int damageAmount;
        public SoundDef sound;
        public FleckDef fleck;
        public float fleckSize;
        public bool indiscriminateExplosion;
    }
    public class HediffComp_GraviticRepulsion : HediffComp
    {
        public HediffCompProperties_GraviticRepulsion Props
        {
            get
            {
                return (HediffCompProperties_GraviticRepulsion)this.props;
            }
        }
        public override string CompLabelInBracketsExtra
        {
            get
            {
                return "x" + (int)this.parent.Severity;
            }
        }
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (this.Pawn.IsHashIntervalTick(this.Props.periodicity,delta) && this.Pawn.Spawned)
            {
                bool doFx = false;
                int damage = this.Props.damageAmount;
                foreach (Thing thing in GenRadial.RadialDistinctThingsAround(this.Pawn.Position, this.Pawn.Map, this.Props.radius, true).InRandomOrder())
                {
                    if (((thing is Pawn p && !p.Downed)|| thing is Building_Turret) && this.Pawn.HostileTo(thing))
                    {
                        doFx = true;
                        if (this.Props.indiscriminateExplosion)
                        {
                            GenExplosion.DoExplosion(this.Pawn.Position,this.Pawn.Map,this.Props.radius,this.Props.damageType,this.Pawn,this.Props.damageAmount,ignoredThings:new List<Thing> { this.Pawn});
                            break;
                        }
                        thing.TakeDamage(new DamageInfo(this.Props.damageType,this.Props.damageAmount,instigator:this.Pawn));
                    }
                }
                if (doFx)
                {
                    this.parent.Severity += this.Props.severityCost;
                    this.Props.sound?.PlayOneShot(new TargetInfo(this.Pawn.Position, this.Pawn.Map, false));
                    if (this.Props.fleck != null)
                    {
                        FleckMaker.Static(this.Pawn.TrueCenter(), this.Pawn.Map, this.Props.fleck,this.Props.fleckSize);
                    }
                }
            }
        }
    }
}
