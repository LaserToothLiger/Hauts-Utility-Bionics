using HarmonyLib;
using HautsBionics;
using HautsFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace HautsBionics_Royalty
{
    [StaticConstructorOnStartup]
    public static class HautsBionics_Royalty
    {
        private static readonly Type patchType = typeof(HautsBionics_Royalty);
        static HautsBionics_Royalty()
        {
            Harmony harmony = new Harmony(id: "rimworld.hautarche.hautsbionics.royalty");
            harmony.Patch(AccessTools.Method(typeof(HautsUtility), nameof(HautsUtility.TotalPsyfocusRefund)),
                          postfix: new HarmonyMethod(patchType, nameof(HVB_TotalPsyfocusRefundPostfix)));
        }
        public static void HVB_TotalPsyfocusRefundPostfix(ref float __result, Pawn pawn, float psyfocusCost, bool isWord)
        {
            if (isWord)
            {
                foreach (Hediff h in pawn.health.hediffSet.hediffs)
                {
                    if (h is Hediff_Silvertongue)
                    {
                        __result += (psyfocusCost - __result) * 0.35f;
                        break;
                    }
                }
            }
        }
    }
    public class Hediff_Silvertongue : Hediff_AddedPart
    {

    }
}
