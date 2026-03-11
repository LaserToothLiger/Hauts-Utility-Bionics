using Verse;

namespace HautsBionics_Biotech
{
    public class HediffCompProperties_BuffMechsInCommandRange : HediffCompProperties
    {
        public HediffCompProperties_BuffMechsInCommandRange()
        {
            this.compClass = typeof(HedifComp_BuffMechsInCommandRange);
        }
        public HediffDef hediff;
    }
    public class HedifComp_BuffMechsInCommandRange : HediffComp
    {
        public HediffCompProperties_BuffMechsInCommandRange Props
        {
            get
            {
                return (HediffCompProperties_BuffMechsInCommandRange)this.props;
            }
        }
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (this.Pawn.IsHashIntervalTick(15) && this.Pawn.mechanitor != null && !this.Pawn.mechanitor.OverseenPawns.NullOrEmpty())
            {
                foreach (Pawn p in this.Pawn.mechanitor.OverseenPawns)
                {
                    if (p.Spawned && MechanitorUtility.InMechanitorCommandRange(p, p.Position))
                    {
                        p.health.AddHediff(this.Props.hediff);
                    }
                }
            }
        }
    }
}
