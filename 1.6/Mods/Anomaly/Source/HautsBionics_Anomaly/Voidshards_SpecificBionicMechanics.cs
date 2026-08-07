using HautsFramework;
using RimWorld;
using Verse;

namespace HautsBionics_Anomaly
{
    /*the VS version of an Archotech Truelife, it no longer has its unique former functionality; instead, it grants massive damage resistance and a tiny amount of regeneration if the pawn is a shambler.
     * This is accomplished by binarizing severity each variable tick - are you a shambler-->severity for the high stage, otherwise-->severity for the dud stage.
     * Essentially strictly worse than a Truelife (you'd rather have a living person than an unkillable shambler in 99% of cases, and maybe even in >=75% of combat scenarios). But hey, the option is there.*/
    public class Hediff_Hellborne : Hediff_Implant
    {
        public override void PostTickInterval(int delta)
        {
            base.PostTickInterval(delta);
            if (this.pawn.IsShambler)
            {
                this.Severity = 1f;
            }
            else
            {
                this.Severity = 0.01f;
            }
        }
    }
    /*the VS version of the Archotech Sentience Catalyzer, it no longer has the ability to instill sentience catalysts in target animals; instead, it has a melee-range ability to destroy an animal's brain and grant a temporary consciousness boost.
     * hediffToSelf: ewisott - in this case a cons boost
     * baseSeverityToAdd: the hediff should be created with a certain amount of severity (or if it already exists on the caster, its severity should increase by) this amount...
     * bonusSeverityIf fields: ...plus these values if the targeted animal had intermediate trainability|advanced trainability|a sentience catalyst hediff*/
    public class CompProperties_AbilityAbsorbSentience : CompProperties_AbilityAiScansForTargets
    {
        public HediffDef hediffToSelf;
        public float baseSeverityToAdd;
        public float bonusSeverityIfIntermediate;
        public float bonusSeverityIfAdvanced;
        public float bonusSeverityIfHadSentienceCatalyst;
        public float NPCtargetingRange;
    }
    public class CompAbilityEffect_AbsorbSentience : CompAbilityEffect_AiScansForTargets
    {
        public new CompProperties_AbilityAbsorbSentience Props
        {
            get
            {
                return (CompProperties_AbilityAbsorbSentience)this.props;
            }
        }
        public override float Range => this.Props.NPCtargetingRange;
        public override bool AdditionalQualifiers(Thing thing)
        {
            return base.AdditionalQualifiers(thing) && this.CanApplyOn(new LocalTargetInfo(thing),null);
        }
        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return base.CanApplyOn(target, dest) && target.Pawn != null && AbilityUtility.ValidateMustBeAnimal(target.Pawn, false, this.parent);
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            float severityToGain = this.Props.baseSeverityToAdd;
            Pawn occupant = target.Pawn;
            EffecterDefOf.ChimeraRage.Spawn(occupant.Position, occupant.Map, 1f).Cleanup();
            if (occupant != null)
            {
                TrainabilityDef td = TrainableUtility.GetTrainability(occupant);
                if (td != null)
                {
                    if (td == TrainabilityDefOf.Advanced)
                    {
                        severityToGain += this.Props.bonusSeverityIfAdvanced;
                    } else if (td == TrainabilityDefOf.Intermediate) {
                        severityToGain += this.Props.bonusSeverityIfIntermediate;
                    }
                    if (ModsConfig.OdysseyActive && occupant.health.hediffSet.HasHediff(HediffDefOf.SentienceCatalyst))
                    {
                        severityToGain += this.Props.bonusSeverityIfHadSentienceCatalyst;
                    }
                }
                DamageInfo damageInfo = new DamageInfo(DamageDefOf.ExecutionCut, 9999f, 999f, -1f, null, occupant.health.hediffSet.GetBrain(), null, DamageInfo.SourceCategory.ThingOrUnknown, null, true, true, QualityCategory.Normal, true, false);
                damageInfo.SetIgnoreInstantKillProtection(true);
                damageInfo.SetAllowDamagePropagation(false);
                occupant.forceNoDeathNotification = true;
                occupant.TakeDamage(damageInfo);
                occupant.forceNoDeathNotification = false;
            }
            if (occupant.Dead)
            {
                Hediff alreadyExisting = this.parent.pawn.health.hediffSet.GetFirstHediffOfDef(this.Props.hediffToSelf);
                if (alreadyExisting != null)
                {
                    alreadyExisting.Severity += severityToGain;
                } else {
                    BodyPartRecord bpr = this.parent.pawn.health.hediffSet.GetBrain();
                    Hediff hediff = HediffMaker.MakeHediff(this.Props.hediffToSelf, this.parent.pawn, bpr);
                    this.parent.pawn.health.AddHediff(hediff, bpr);
                    hediff.Severity = severityToGain;
                }
            }
        }
    }
    /*some VS have a unique property that grants a chance for any social interaction the pawn makes to be replaced with Inhuman Rambling.
     * Multiple sources of this additively stack their [chancePerSeverity*current severity] to determine the final chance.
     * Obviously, if the InhumanRambler comp has noChanceIfVoidDisrupted and anomaly level >=6 (disrupted), we discount its chance.
     * Needs an update to work with CAPAL from the Framework*/
    public class HediffCompProperties_InhumanRambler : HediffCompProperties
    {
        public HediffCompProperties_InhumanRambler()
        {
            this.compClass = typeof(HediffComp_InhumanRambler);
        }
        public float chancePerSeverity;
        public bool noChanceIfVoidDisrupted = true;
    }
    public class HediffComp_InhumanRambler : HediffComp
    {
        public HediffCompProperties_InhumanRambler Props
        {
            get
            {
                return (HediffCompProperties_InhumanRambler)this.props;
            }
        }
    }
    /*The VS version of a Psychic Harmonizer no longer scales its effect by the host's current mood, but rather instead by the current anomalous activity level. Needs an update to work with CAPAL from the Framework*/
    public class Thought_DarkHarmony : Thought_Memory
    {
        protected override float BaseMoodOffset
        {
            get
            {
                float num = Find.Anomaly.Level;
                if (num > 3f)
                {
                    if (num > 5f)
                    {
                        num = 2f;
                    }
                    else
                    {
                        num -= 1f;
                    }
                }
                return this.CurStage.baseMoodEffect * num;
            }
        }
    }
}
