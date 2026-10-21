using Hauts_CombatExtended;
using HautsBionics;
using RimWorld;
using Verse;

namespace HautsBionics_CombatExtended
{
    //the non-CE version is a derivative of Verb_AbilityShootDontMove, so...
    public class Verb_AbilityShootCE_BionicGraser : Verb_AbilityShootCE_DontMove
    {
        public override bool TryCastShot()
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
                                h.Severity += hcrb.Props.severityPerShot * (hcrb.Props.resistanceStat != null ? p.GetStatValue(hcrb.Props.resistanceStat) : 1f);
                            }
                            break;
                        }
                    }
                }
            }
            return base.TryCastShot();
        }
    }
}
