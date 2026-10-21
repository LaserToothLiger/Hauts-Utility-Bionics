using RimWorld;
using Verse;

namespace HautsBionics_Anomaly
{
    [DefOf]
    public static class HVBAnomalyDefOf
    {
        static HVBAnomalyDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HVBAnomalyDefOf));
        }
        public static HediffDef HVB_DownsideNothing;
        public static ResearchProjectDef HVB_VoidshardBionics;
        public static TraitDef HVB_HomunculusTrait;
        public static CreepJoinerBenefitDef HVB_HomunculusBenefit;
        public static IncidentDef HVB_HomunculusExamined;

        [MayRequireBiotech]
        public static HediffDef HVB_Voidborn;
    }
}
