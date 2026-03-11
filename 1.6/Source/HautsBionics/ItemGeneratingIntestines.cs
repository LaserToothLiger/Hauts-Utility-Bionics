using HautsFramework;
using System.Collections.Generic;
using Verse;

namespace HautsBionics
{
    //various intestines (added if you have Dubs Bad Hygiene) CreateThingsBySpendingSeverity. They gain severityPerDay, unless the pawn has any hediff found in the progressDisabledBy list (e.g. malnutrition).
    public class HediffCompProperties_DubsIntestine : HediffCompProperties_CreateThingsBySpendingSeverity
    {
        public HediffCompProperties_DubsIntestine()
        {
            this.compClass = typeof(HediffComp_DubsIntestine);
        }
        public float severityPerDay;
        public List<HediffDef> progressDisabledBy;
    }
    public class HediffComp_DubsIntestine : HediffComp_CreateThingsBySpendingSeverity
    {
        public new HediffCompProperties_DubsIntestine Props
        {
            get
            {
                return (HediffCompProperties_DubsIntestine)this.props;
            }
        }
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (this.Pawn.IsHashIntervalTick(300, delta))
            {
                foreach (HediffDef hd in this.Props.progressDisabledBy)
                {
                    if (this.Pawn.health.hediffSet.HasHediff(hd))
                    {
                        return;
                    }
                }
                this.parent.Severity += this.Props.severityPerDay / 200f;
            }
        }
    }
}
