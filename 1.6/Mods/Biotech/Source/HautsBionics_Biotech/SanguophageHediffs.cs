using RimWorld;
using Verse;

namespace HautsBionics_Biotech
{
    //sets the hediff's severity to the pawn's current deathrest level. If it doesn't need deathrest, sets it to the minSeverity instead
    public class HediffCompProperties_DeathrestSeverity : HediffCompProperties
    {
        public HediffCompProperties_DeathrestSeverity()
        {
            this.compClass = typeof(HediffComp_DeathrestSeverity);
        }
    }
    public class HediffComp_DeathrestSeverity : HediffComp
    {
        public HediffCompProperties_DeathrestSeverity Props
        {
            get
            {
                return (HediffCompProperties_DeathrestSeverity)this.props;
            }
        }
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (this.Pawn.IsHashIntervalTick(15, delta))
            {
                Need_Deathrest nd = this.Pawn.needs.TryGetNeed<Need_Deathrest>();
                if (nd != null)
                {
                    this.parent.Severity = nd.CurLevel;
                }
                else
                {
                    this.parent.Severity = this.parent.def.minSeverity;
                }
            }
        }
    }
    //the Hemogen Burning Stomach induces rapid hemogen loss in pawns with a hemogen gene. Its severity is equal to the pawn's current hemogen level. On a non-hemogenic pawn, its severity is minSeverity instead.
    public class HediffCompProperties_HBS : HediffCompProperties
    {
        public HediffCompProperties_HBS()
        {
            this.compClass = typeof(HediffComp_HBS);
        }
        public float hemogenDrainPerDay;
    }
    public class HediffComp_HBS : HediffComp
    {
        public HediffCompProperties_HBS Props
        {
            get
            {
                return (HediffCompProperties_HBS)this.props;
            }
        }
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (this.Pawn.IsHashIntervalTick(15, delta) && this.Pawn.genes != null)
            {
                Gene_Hemogen gene_Hemogen = this.Pawn.genes.GetFirstGeneOfType<Gene_Hemogen>();
                if (gene_Hemogen != null)
                {
                    GeneUtility.OffsetHemogen(this.Pawn, -this.Props.hemogenDrainPerDay / 4000f, true);
                    this.parent.Severity = gene_Hemogen.Value;
                } else {
                    this.parent.Severity = this.parent.def.minSeverity;
                }
            }
        }
    }
}
