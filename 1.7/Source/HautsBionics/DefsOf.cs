using RimWorld;
using Verse;

namespace HautsBionics
{
    [DefOf]
    public static class HVBDefOf
    {
        static HVBDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HVBDefOf));
        }
        public static RimWorld.AbilityDef HVB_BionicPounce;
        public static RimWorld.AbilityDef HVB_ToggleBreathtaker;
        public static RimWorld.AbilityDef HVB_ToggleEarthshaker;

        public static HediffDef HVB_HardheadProtector;
        public static HediffDef HVB_PanoptesSkull;
        public static HediffDef HVB_PsychicFoilBarrier;
        public static HediffDef HVB_CognoCensor;
        public static HediffDef HVB_CognoStraitjacketComa;
        public static HediffDef HVB_PurifierJaw;
        public static HediffDef HVB_CenterMassLaminar;
        public static HediffDef HVB_TemperedHeart;
        public static HediffDef HVB_LazarusSeedCharging;
        public static HediffDef HVB_LazarusSeed;
        public static HediffDef HVB_RefineryStomach;
        public static HediffDef HVB_ArchotechRefineryStomach;
        public static HediffDef HVB_ArchotechNeutralizer;
        public static HediffDef HVB_AugmentedMarrowCounter;
        public static HediffDef HVB_PsychicTrepanation;
        public static HediffDef HVB_NeuralResocialization;
        public static HediffDef HVB_BrokenResoc;
        [MayRequireIdeology]
        public static HediffDef HVB_Gaucrown;
        [MayRequireRoyalty]
        public static HediffDef HVB_EltexSilvertongue;
        [MayRequireOdyssey]
        public static HediffDef HVB_PsilocapFilter;

        public static RecipeDef HVB_PerformPsychicTrepanation;
        public static RecipeDef HVB_InstallTrachealIntubation;
        public static RecipeDef HVB_InstallNeuralResocialization;

        public static StatDef HVB_RemainingMarrow;
        public static StatDef HVB_MarrowEfficacy;

        //public static ThingDef HVB_IEDExplosionTimer;
        public static ThingDef HVB_GrabFlyer;

        public static ThoughtDef HVB_CognoCensorship;
    }
    [DefOf]
    public static class HVBThingDefOf
    {
        static HVBThingDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HVBDefOf));
        }
        public static ThingDef HVB_HardheadProtector;
        public static ThingDef HVB_PanoptesSkull;
        public static ThingDef HVB_PsychicFoilBarrier;
        [MayRequireIdeology]
        public static ThingDef HVB_Gaucrown;
    }
}
