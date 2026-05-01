using HautsFramework;
using RimWorld;
using Verse;

namespace HautsBionics
{
    public class Verb_AbilityShootBionicGraser : Verb_AbilityShootDontMove
    {
        protected override bool TryCastShot()
        {
            if (this.Ability != null)
            {
                Pawn p = this.CasterPawn;
                if (p != null)
                {
                    foreach (Hediff h in p.health.hediffSet.hediffs)
                    {
                        if (!h.AllAbilitiesForReading.NullOrEmpty() && h.AllAbilitiesForReading.Contains(this.Ability))
                        {
                            HediffComp_RadiationBuildup hcrb = h.TryGetComp<HediffComp_RadiationBuildup>();
                            if (hcrb != null)
                            {
                                h.Severity += hcrb.Props.severityPerShot *(hcrb.Props.resistanceStat != null ? p.GetStatValue(hcrb.Props.resistanceStat) : 1f);
                            }
                            break;
                        }
                    }
                }
            }
            return base.TryCastShot();
        }
    }
    public class HediffCompProperties_RadiationBuildup : HediffCompProperties
    {
        public HediffCompProperties_RadiationBuildup()
        {
            this.compClass = typeof(HediffComp_RadiationBuildup);
        }
        public ExtraDamage damage;
        public HediffDef condition;
        public float conditionSevverity;
        public float severityPerShot;
        public StatDef resistanceStat;
        public float damageOrConditionMinSeverity;
        public SimpleCurve damageOrConditionMTBdaysCurve;
        public string riskString;
    }
    public class HediffComp_RadiationBuildup : HediffComp
    {
        public HediffCompProperties_RadiationBuildup Props
        {
            get
            {
                return (HediffCompProperties_RadiationBuildup)this.props;
            }
        }
        public float CurrentRisk
        {
            get
            {
                return this.parent.Severity >= this.Props.damageOrConditionMinSeverity ? this.Props.damageOrConditionMTBdaysCurve.Evaluate(this.parent.Severity) : 0f;
            }
        }
        public override string CompTipStringExtra {
            get {
                if (this.Props.riskString == null)
                {
                    return null;
                }
                float currentRisk = this.CurrentRisk;
                if (currentRisk <= 0f)
                {
                    return null;
                }
                return this.Props.riskString.Translate(this.CurrentRisk);
            }
        }
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (this.Pawn.IsHashIntervalTick(240,delta) && this.parent.Severity >= this.Props.damageOrConditionMinSeverity)
            {
                if (Rand.MTBEventOccurs(this.CurrentRisk,60000,240))
                {
                    if (Rand.Chance(0.5f))
                    {
                        DamageInfo dinfo2 = new DamageInfo(this.Props.damage.def, this.Props.damage.amount, 999f, -1f, null, this.parent.Part, null, DamageInfo.SourceCategory.ThingOrUnknown);
                        dinfo2.SetWeaponHediff(this.parent.def);
                        this.Pawn.TakeDamage(dinfo2);
                    } else {
                        Hediff h = this.Pawn.health.hediffSet.GetFirstHediffOfDef(this.Props.condition);
                        if (h != null)
                        {
                            h.Severity += this.Props.conditionSevverity;
                        } else {
                            h = HediffMaker.MakeHediff(this.Props.condition,this.Pawn);
                            h.Severity = this.Props.conditionSevverity;
                            this.Pawn.health.AddHediff(h);
                        }
                    }
                }
            }
        }
    }
}
