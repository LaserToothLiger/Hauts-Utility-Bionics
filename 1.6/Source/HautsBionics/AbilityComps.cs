using HautsFramework;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using VEF;
using Verse;

namespace HautsBionics
{
    //NPCs use Bionic Foam Spit to extinguish fires, unless said fire shares a cell with an enemy. This obv isn't perfect, since they could target a fire next to a burning foe and inadvertently help them via splash, but it's good enough.
    public class CompAbilityEffect_Firefoamer : CompAbilityEffect_AiScansForTargets
    {
        public override bool AdditionalQualifiers(Thing thing)
        {
            if (thing is Fire && thing.Spawned)
            {
                List<Thing> things = thing.Position.GetThingList(thing.Map);
                foreach (Thing t in things)
                {
                    if (this.parent.pawn.HostileTo(t))
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }
    }
    //Bionic Banshee Wail is a Nova derivative, which means it inherits properties for NPCs to cast on self if a VictimCounter is exceeded, casting prompts AffectSelf, and it also inflicts AffectPawn on all pawns in radius
    public class CompProperties_AbilityBansheeWail : CompProperties_AbilityNova
    {
        public int stunTicks;
        public PawnCapacityDef stunScalarCapacity = null;
        public StatDef stunScalarStat = null;
        public float aiHearingDifferenceToUse = 0.5f;
    }
    public class CompAbilityEffect_BansheeWail : CompAbilityEffect_Nova
    {
        public new CompProperties_AbilityBansheeWail Props
        {
            get
            {
                return (CompProperties_AbilityBansheeWail)this.props;
            }
        }
        public float StunTime(Pawn pawn)
        {
            float stunTime = this.Props.stunTicks;
            if (this.Props.stunScalarCapacity != null)
            {
                stunTime *= pawn.health.capacities.GetLevel(this.Props.stunScalarCapacity);
            }
            if (this.Props.stunScalarStat != null)
            {
                stunTime *= pawn.GetStatValue(this.Props.stunScalarStat);
            }
            return stunTime;
        }
        public override void AffectSelf()
        {
            base.AffectSelf();
            this.parent.pawn.stances.stunner.StunFor((int)(StunTime(this.parent.pawn) / 2f), this.parent.pawn, false, true);
        }
        public override void AffectPawn(Pawn pawn)
        {
            base.AffectPawn(pawn);
            pawn.stances.stunner.StunFor((int)StunTime(pawn), this.parent.pawn, false, true);
        }
        public override bool VictimCounter()
        {
            this.hearingTotal = 0;
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(this.parent.pawn.Position, this.parent.pawn.Map, this.Radius, true))
            {
                if (thing is Pawn p && !p.stances.stunner.Stunned)
                {
                    if (p.HostileTo(this.parent.pawn) && !p.ThreatDisabled(this.parent.pawn) && this.parent.pawn.Map.attackTargetsCache.GetPotentialTargetsFor(this.parent.pawn).Contains(p))
                    {
                        this.hearingTotal += p.health.capacities.GetLevel(PawnCapacityDefOf.Hearing);
                    }
                    else if (this.Props.aiDislikesFriendlyFire && p.Faction != null)
                    {
                        this.hearingTotal -= p.health.capacities.GetLevel(PawnCapacityDefOf.Hearing);
                    }
                }
            }
            return this.parent.pawn.Spawned && this.hearingTotal > this.Props.aiHearingDifferenceToUse;
        }
        private float hearingTotal = 0;
    }
    //Bionic EMP Discharge is also a Nova. It doesn't need any unique fields (EMP explosion uses default EMP damage def properties), but we want it to only cast if the EMP wouldn't debilitate the caster and it WOULD debilitate a foe
    public class CompAbilityEffect_EMPDischarge : CompAbilityEffect_Nova
    {
        public override void AffectSelf()
        {
            base.AffectSelf();
            List<Thing> ignoredThings = new List<Thing> { this.parent.pawn };
            GenExplosion.DoExplosion(this.parent.pawn.PositionHeld, this.parent.pawn.Map, Math.Min(this.Radius, this.Props.maxRadius), DamageDefOf.EMP, null, -1, -1f, null, null, null, null, null, 0f, 1, null, null, 255, false, null, 0f, 1, 0f, false, null, ignoredThings, null, true, 1f, 0f, true, null, 1f);
        }
        public override bool VictimCounter()
        {
            if (HautsMiscUtility.ReactsToEMP(this.parent.pawn))
            {
                return false;
            }
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(this.parent.pawn.Position, this.parent.pawn.Map, this.Radius, true))
            {
                if (thing.HostileTo(this.parent.pawn))
                {
                    if (thing is Building_Turret)
                    {
                        return true;
                    } else if (thing is Pawn p && HautsMiscUtility.ReactsToEMP(p) && !p.ThreatDisabled(this.parent.pawn) && this.parent.pawn.Map.attackTargetsCache.GetPotentialTargetsFor(this.parent.pawn).Contains(p)) {
                        return true;
                    }
                }
            }
            return false;
        }
        public override float Radius
        {
            get
            {
                return base.Radius * (this.parent.pawn.BodySize + this.parent.pawn.GetStatValue(VEFDefOf.VEF_BodySize_Offset)) * this.parent.pawn.GetStatValue(VEFDefOf.VEF_BodySize_Multiplier);
            }
        }
        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            if (base.AICanTargetNow(target))
            {
                if (target.Thing is Building_Turret)
                {
                    return true;
                }
            }
            return ((target.Pawn != null && HautsMiscUtility.ReactsToEMP(target.Pawn)) || target.Thing is Building_Turret) && base.AICanTargetNow(target);
        }
    }
    /*Inflict Mechanite Disease is melee for most pawns. However, the mechanitor "mech remote repair distance" (granted by repair probes) increases its range too. (That's handled by XML fields inherited from its parent)
     * This is why you can't target a pawn at an arbitrary distance and have the caster go over to them like most melee abilities - it's behaving as a ranged one.
     * Its parent comp allows NPCs with this ability to periodically scan for someone in range to apply the ability to. In this case, it's looking for any in-range foe it could incapacitate with the expected pain caused by the inflicted disease.*/
    public class CompProperties_AbilityInflictMechanites : CompProperties_AbilityAiScansForTargets
    {
        public List<HediffDef> possibleHediffs;
    }
    public class CompAbilityEffect_InflictMechanites : CompAbilityEffect_AiScansForTargets
    {
        public new CompProperties_AbilityInflictMechanites Props
        {
            get
            {
                return (CompProperties_AbilityInflictMechanites)this.props;
            }
        }
        public override float Range
        {
            get
            {
                return Math.Max(base.Range, 2f);
            }
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Hediff hediff = HediffMaker.MakeHediff(this.Props.possibleHediffs.RandomElement(), target.Pawn, null);
            target.Pawn.health.AddHediff(hediff, null, null, null);
        }
        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            if (target.Pawn != null)
            {
                return this.SufficientlyPained(target.Pawn);
            }
            return false;
        }
        public override bool AdditionalQualifiers(Thing thing)
        {
            if (thing is Pawn p)
            {
                return this.SufficientlyPained(p);
            }
            return false;
        }
        public bool SufficientlyPained(Pawn pawn)
        {
            if (!pawn.ThreatDisabled(this.parent.pawn))
            {
                float avgPain = 0f;
                foreach (HediffDef h in this.Props.possibleHediffs)
                {
                    if (!pawn.health.hediffSet.HasHediff(h) && h.stages != null)
                    {
                        avgPain += h.stages[h.StageAtSeverity(h.initialSeverity)].painOffset;
                    }
                }
                avgPain /= this.Props.possibleHediffs.Count;
                return pawn.health.hediffSet.PainTotal + avgPain >= pawn.GetStatValue(StatDefOf.PainShockThreshold, true, -1);
            }
            return false;
        }
    }
    /*Cardiac Overdrive is a GiveHediff derivative that also adds another hediff, secondHediff, to secondHediffPart at an initial severity of secondHediffSeverity (or if it already exists, increases its severity by that amount).
     * secondHediff is being used as the "penalty" of using this ability too frequently, since it causes pain, reduces blood pumping, and can inflict heart attacks.*/
    public class CompProperties_AbilityCO : CompProperties_AbilityGiveHediff
    {
        public HediffDef secondHediff;
        public float secondHediffSeverity;
        public BodyPartDef secondHediffPart;
    }
    public class CompAbilityEffect_GiveHediffCO : CompAbilityEffect_GiveHediff
    {
        public new CompProperties_AbilityCO Props
        {
            get
            {
                return (CompProperties_AbilityCO)this.props;
            }
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Hediff hediff = this.parent.pawn.health.hediffSet.GetFirstHediffOfDef(this.Props.secondHediff);
            if (hediff != null)
            {
                hediff.Severity += this.Props.secondHediffSeverity;
            } else {
                hediff = HediffMaker.MakeHediff(this.Props.secondHediff, this.parent.pawn, this.Props.secondHediffPart != null ? this.parent.pawn.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Undefined).Where((BodyPartRecord bpr) => bpr.def == this.Props.secondHediffPart).RandomElement() : null);
                hediff.Severity = this.Props.secondHediffSeverity;
                this.parent.pawn.health.AddHediff(hediff, hediff.Part);
            }
        }
    }
    //the mod "Craftable and Improved Sentience Catalysts" turns sentience catalysts into leveling hediffs, so the archotech sentience catalyzer needs a custom comp to handle this if you are/n't running that mod
    public class CompProperties_AbilityCatalyzeSentience : CompProperties_AbilityEffect
    {
        public HediffDef hediffDef;
        public bool onlyBrain;
        public int levelOffset = 1;
    }
    public class CompAbilityEffect_CatalyzeSentience : CompAbilityEffect
    {
        public new CompProperties_AbilityCatalyzeSentience Props
        {
            get
            {
                return (CompProperties_AbilityCatalyzeSentience)this.props;
            }
        }
        public bool TargetHasMaxSentienceCatalysts(Pawn p)
        {
            Hediff h = p.health.hediffSet.GetFirstHediffOfDef(this.Props.hediffDef);
            if (p.health.hediffSet.HasHediff(this.Props.hediffDef))
            {
                if (!(h is Hediff_Level hl) || (hl.CurStageIndex + 1) >= this.Props.hediffDef.stages.Count)
                {
                    return true;
                }
            }
            return false;
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (target != null && target.Thing != null && target.Thing is Pawn p)
            {
                if (!p.RaceProps.Animal || this.TargetHasMaxSentienceCatalysts(p))
                {
                    return;
                }
                Hediff already = p.health.hediffSet.GetFirstHediffOfDef(this.Props.hediffDef);
                if (already != null)
                {
                    if (this.Props.levelOffset > 0)
                    {
                        if (already is Hediff_Level hl)
                        {
                            hl.ChangeLevel(this.Props.levelOffset);
                        } else {
                            already.Severity = this.Props.levelOffset;
                        }
                    }
                } else {
                    Hediff hediff = HediffMaker.MakeHediff(this.Props.hediffDef, p, this.Props.onlyBrain ? p.health.hediffSet.GetBrain() : null);
                    p.health.AddHediff(hediff, null, null, null);
                }
            }
        }
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (target != null && target.Thing != null && target.Thing is Pawn p)
            {
                if (this.TargetHasMaxSentienceCatalysts(p))
                {
                    if (throwMessages)
                    {
                        Messages.Message("CannotUseAbility".Translate(this.parent.def.label) + ": " + "HVB_MaxedOutCatalysts".Translate(), target.ToTargetInfo(this.parent.pawn.Map), MessageTypeDefOf.RejectInput, false);
                    }
                    return false;
                }
                return AbilityUtility.ValidateMustBeAnimal(p, throwMessages, this.parent);
            }
            return false;
        }
        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return false;
        }
    }
    //Big and Small - Sapient Animals ability comp: does the cogni-fi effect on the targeted pawn
    public class CompProperties_AbilityCogniFi : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityCogniFi()
        {
            this.compClass = typeof(CompAbilityEffect_CogniFi);
        }
    }
    //an ability comp that mimicks what the cogni-fi item does. Note that this ability can never apply on any pawn UNLESS Big and Small is running, as CanSapienateAnimal is false by default.
    public class CompAbilityEffect_CogniFi : CompAbilityEffect
    {
        public new CompProperties_AbilityCogniFi Props
        {
            get
            {
                return (CompProperties_AbilityCogniFi)this.props;
            }
        }
        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return base.CanApplyOn(target, dest) && target.Pawn != null && ModCompatibilityUtility.CanSapienateAnimal(target.Pawn);
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (target.Pawn != null)
            {
                ModCompatibilityUtility.SapienateAnimal(target.Pawn);
            }
        }
    }
}
