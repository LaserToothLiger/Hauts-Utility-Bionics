using HautsFramework;
using RimWorld;
using System;
using Verse;

namespace HautsBionics
{
    /*a special variant of the Framework's DamageNegationShield. ShieldGenerator bionics produce a bionicShieldDef hediff which should have the BionicShield comp; if a pawn has multiple generators that share the same def,
     * only one instance of the shield is generated, and it uses the aggregate of the "stats" in those generators' ShieldGenerator fields.
     * These stat aggregates are recalculated whenever a shield generator of the shield's type is added to or removed from the pawn, invoking the shield's RedetermineAllStats method.
     * Shield generators won't make a shield if their severity is exceeded by their minSeverityToGenerate, but given what this is used for (graviton shielder) it doesn't prevent them from contributing to a shield's stats during RedetermineAllStats.*/
    public class HediffCompProperties_BionicShield : HediffCompProperties_DamageNegationShield
    {
        public HediffCompProperties_BionicShield()
        {
            this.compClass = typeof(HediffComp_BionicShield);
        }
    }
    public class HediffComp_BionicShield : HediffComp_DamageNegationShield
    {
        public new HediffCompProperties_BionicShield Props
        {
            get
            {
                return (HediffCompProperties_BionicShield)this.props;
            }
        }
        public override void RedetermineAllStats()
        {
            float netEnergyGainPerTick = this.Props.baseEnergyRechargeRate / 60f;
            int netResetDelayTicks = this.Props.baseStartingTicksToReset;
            float netMaxEnergy = this.Props.baseMaxEnergy;
            bool anyGenerator = false;
            foreach (Hediff h in this.Pawn.health.hediffSet.hediffs)
            {
                HediffComp_ShieldGenerator hcsg = h.TryGetComp<HediffComp_ShieldGenerator>();
                if (hcsg != null && hcsg.Props.bionicShieldDef != null && hcsg.Props.bionicShieldDef == this.parent.def)
                {
                    netEnergyGainPerTick += hcsg.Props.energyRegenOffset / 60f;
                    netResetDelayTicks = (int)(netResetDelayTicks * hcsg.Props.resetDelayFactor);
                    netMaxEnergy += hcsg.Props.maxEnergyOffset;
                    if (hcsg.Props.makesShield)
                    {
                        anyGenerator = true;
                    }
                }
            }
            netEnergyGainPerTick *= this.Props.rechargeRateScalar != null ? this.Pawn.GetStatValue(this.Props.rechargeRateScalar) : 1f;
            netMaxEnergy *= this.Props.maxEnergyScalar != null ? this.Pawn.GetStatValue(this.Props.maxEnergyScalar) : 1f;
            netMaxEnergy += this.Props.minSeverityToWork;
            this.EnergyGainPerTick = Math.Max(0f, netEnergyGainPerTick);
            this.ResetDelayTicks = Math.Max(1, netResetDelayTicks);
            this.MaxEnergy = Math.Max(0.001f, netMaxEnergy);
            if (!anyGenerator)
            {
                this.Pawn.health.RemoveHediff(this.parent);
            }
        }
    }
    public class HediffCompProperties_ShieldGenerator : HediffCompProperties
    {
        public HediffCompProperties_ShieldGenerator()
        {
            this.compClass = typeof(HediffComp_ShieldGenerator);
        }
        public float energyRegenOffset;
        public float resetDelayFactor = 1f;
        public float maxEnergyOffset;
        public HediffDef bionicShieldDef;
        public bool makesShield;
        public float minSeverityToGenerate = -1f;
    }
    public class HediffComp_ShieldGenerator : HediffComp
    {
        public HediffCompProperties_ShieldGenerator Props
        {
            get
            {
                return (HediffCompProperties_ShieldGenerator)this.props;
            }
        }
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            if (this.Props.bionicShieldDef != null)
            {
                if (this.Props.makesShield && !this.Pawn.health.hediffSet.HasHediff(this.Props.bionicShieldDef) && this.parent.Severity >= this.Props.minSeverityToGenerate)
                {
                    Hediff hediff = HediffMaker.MakeHediff(this.Props.bionicShieldDef, this.Pawn, null);
                    this.Pawn.health.AddHediff(hediff);
                }
                Hediff shield = this.Pawn.health.hediffSet.GetFirstHediffOfDef(this.Props.bionicShieldDef);
                if (shield != null)
                {
                    HediffComp_BionicShield hcbs = shield.TryGetComp<HediffComp_BionicShield>();
                    if (hcbs != null)
                    {
                        hcbs.RedetermineAllStats();
                    }
                }
            }
        }
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (this.Pawn.IsHashIntervalTick(120, delta) && this.Props.bionicShieldDef != null && this.Props.makesShield && !this.Pawn.health.hediffSet.HasHediff(this.Props.bionicShieldDef) && this.parent.Severity >= this.Props.minSeverityToGenerate)
            {
                Hediff shield = HediffMaker.MakeHediff(this.Props.bionicShieldDef, this.Pawn, null);
                this.Pawn.health.AddHediff(shield);
            }
        }
        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            if (this.Props.bionicShieldDef != null)
            {
                Hediff shield = this.Pawn.health.hediffSet.GetFirstHediffOfDef(this.Props.bionicShieldDef);
                if (shield != null)
                {
                    HediffComp_BionicShield hcbs = shield.TryGetComp<HediffComp_BionicShield>();
                    hcbs.RedetermineAllStats();
                }
            }
        }
    }
}
