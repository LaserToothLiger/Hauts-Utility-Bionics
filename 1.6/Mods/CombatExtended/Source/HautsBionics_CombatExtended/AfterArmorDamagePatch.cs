using CombatExtended;
using HarmonyLib;
using HautsBionics;
using RimWorld;
using System;
using Verse;

namespace HautsBionics_CombatExtended
{
    [StaticConstructorOnStartup]
    public class HautsBionics_CombatExtended
    {
        private static readonly Type patchType = typeof(HautsBionics_CombatExtended);
        static HautsBionics_CombatExtended()
        {
            Harmony harmony = new Harmony(id: "rimworld.hautarche.hautsbionics.combatExtended");
            harmony.Patch(AccessTools.Method(typeof(ArmorUtilityCE), nameof(ArmorUtilityCE.GetAfterArmorDamage)),
                          postfix: new HarmonyMethod(patchType, nameof(HVB_GetAfterArmorDamagePostfix)));
        }
        //makes the damage reduction effects of the hardhead protector and center mass laminar work
        public static void HVB_GetAfterArmorDamagePostfix(ref DamageInfo __result, Pawn pawn, BodyPartRecord hitPart)
        {
            if ((hitPart.IsInGroup(BodyPartGroupDefOf.FullHead) || hitPart.IsInGroup(BodyPartGroupDefOf.UpperHead)) && pawn.health.hediffSet.HasHediff(HVBDefOf.HVB_HardheadProtector))
            {
                __result.SetAmount(__result.Amount*0.7f);
            }
            if (hitPart.IsInGroup(BodyPartGroupDefOf.Torso) && pawn.health.hediffSet.HasHediff(HVBDefOf.HVB_CenterMassLaminar))
            {
                __result.SetAmount(__result.Amount * 0.8f);
            }
        }
    }
}
