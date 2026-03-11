using Verse;

namespace HautsBionics
{
    //the charging and ready versions of a Lazarus Seed are different hediffs. The "readiness" is obviously expended to bring the pawn back to life, so when they come back to life, the ready version replaces itself with the charging version
    public class Hediff_LazarusSeed : Hediff_Implant
    {
        public override void Notify_Resurrected()
        {
            base.Notify_Resurrected();
            Hediff hediff = HediffMaker.MakeHediff(HVBDefOf.HVB_LazarusSeedCharging, this.pawn, this.Part);
            this.pawn.health.AddHediff(hediff, this.Part);
            this.pawn.health.RemoveHediff(this);
        }
    }
}
