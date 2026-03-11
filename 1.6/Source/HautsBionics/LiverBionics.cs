using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace HautsBionics
{
    /*Dosage sustainers double the duration of drug highs and chemical dependencies. They do this by periodically adding back half the severity such hediffs lost since the last addition.
     * This is done instead of some means of doubling the starting durations of those hediffs so that the dosage sustainer works even for highs/dependencies that existed before its installation.*/
    public class Hediff_DosageSustainer : Hediff_AddedPart
    {
        public override void PostTickInterval(int delta)
        {
            base.PostTickInterval(delta);
            if (this.pawn.IsHashIntervalTick(240, delta))
            {
                foreach (Hediff h in this.pawn.health.hediffSet.hediffs)
                {
                    if (h is Hediff_High high)
                    {
                        foreach (HediffComp hc in high.comps)
                        {
                            if (hc is HediffComp_SeverityPerDay spd)
                            {
                                h.Severity += Math.Abs(spd.SeverityChangePerDay()) * 0.002f;
                                break;
                            }
                        }
                    }
                    else if (h is Hediff_ChemicalDependency cd)
                    {
                        foreach (HediffComp hc in cd.comps)
                        {
                            if (hc is HediffComp_SeverityPerDay spd)
                            {
                                h.Severity -= Math.Abs(spd.SeverityChangePerDay()) * 0.002f;
                                break;
                            }
                        }
                    }
                }
            }
        }
    }
    //Archotech neutralizers remove a bunch of bad conditions every 3.33 seconds
    public class Hediff_Neutralizer : Hediff_AddedPart
    {
        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            this.NoDrugsNoDiseases();
        }
        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (this.pawn.IsHashIntervalTick(200, delta))
            {
                this.NoDrugsNoDiseases();
            }
        }
        private void NoDrugsNoDiseases()
        {
            List<Hediff> hediffsToRemove = new List<Hediff>();
            foreach (Hediff h in this.pawn.health.hediffSet.hediffs)
            {
                if ((h.def.makesSickThought && h.def.isBad && h.def.tendable) || h is Hediff_Addiction || h is Hediff_High || h.def == HediffDefOf.DrugOverdose)
                {
                    hediffsToRemove.Add(h);
                }
            }
            foreach (Hediff h in hediffsToRemove)
            {
                this.pawn.health.RemoveHediff(h);
            }
        }
    }
}
