using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using HarmonyLib;
using HautsBionics;

namespace HautsBionics_PFF
{
    [StaticConstructorOnStartup]
    public class HautsBionics_PFF
    {
        private static readonly Type patchType = typeof(HautsBionics_PFF);
        static HautsBionics_PFF()
        {
            Harmony harmony = new Harmony(id: "rimworld.hautarche.hautsbionics.pff");
            harmony.Patch(AccessTools.Method(typeof(HVBUtility), nameof(HVBUtility.HVB_EvaluateFins)),
                          postfix: new HarmonyMethod(patchType, nameof(HVBPFF_EvaluateFinsPostfix)));
            harmony.Patch(AccessTools.Method(typeof(HVBUtility), nameof(HVBUtility.HVB_RecalculateFinPower)),
                          postfix: new HarmonyMethod(patchType, nameof(HVBPFF_RecalculateFinPowerPostfix)));
        }
        public static void HVBPFF_EvaluateFinsPostfix(Pawn pawn)
        {
            if (pawn.health.hediffSet.HasHediff(HVBPFFDefOf.HVB_EnoughFinsToSwim))
            {
                if (HVBUtility.HVB_RecalculateFinPower(pawn) < 1f)
                {
                    Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(HVBPFFDefOf.HVB_EnoughFinsToSwim);
                    pawn.health.RemoveHediff(hediff);
                }
            } else {
                if (HVBUtility.HVB_RecalculateFinPower(pawn) >= 1f)
                {
                    Hediff hediff = HediffMaker.MakeHediff(HVBPFFDefOf.HVB_EnoughFinsToSwim, pawn);
                    pawn.health.AddHediff(hediff, null);
                }
            }
        }
        public static void HVBPFF_RecalculateFinPowerPostfix(ref float __result, Pawn pawn)
        {
            __result = 0f;
            foreach (Hediff h in pawn.health.hediffSet.hediffs)
            {
                if (h is HediffWithComps hwc)
                {
                    HediffComp_Fin hcf = hwc.TryGetComp<HediffComp_Fin>();
                    if (hcf != null)
                    {
                        __result += hcf.Props.finPower;
                    }
                }
            }
        }
    }
    [DefOf]
    public static class HVBPFFDefOf
    {
        static HVBPFFDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HVBPFFDefOf));
        }
        public static HediffDef HVB_EnoughFinsToSwim;
    }
}
