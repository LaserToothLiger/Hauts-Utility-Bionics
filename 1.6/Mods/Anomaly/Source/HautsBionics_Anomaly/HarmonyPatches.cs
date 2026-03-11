using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using HarmonyLib;
using HautsFramework;
using System.Reflection;

namespace HautsBionics_Anomaly
{
    [StaticConstructorOnStartup]
    public class HautsBionics_Anomaly
    {
        private static readonly Type patchType = typeof(HautsBionics_Anomaly);
        static HautsBionics_Anomaly()
        {
            Harmony harmony = new Harmony(id: "rimworld.hautarche.hautsbionics.anomaly");
            //tweaks
            harmony.Patch(AccessTools.Method(typeof(Pawn_InteractionsTracker), nameof(Pawn_InteractionsTracker.TryInteractWith)),
                          prefix: new HarmonyMethod(patchType, nameof(HVBATryInteractWithPrefix)));
            harmony.Patch(AccessTools.Method(typeof(Pawn_CreepJoinerTracker), nameof(Pawn_CreepJoinerTracker.DoSurgicalInspection)),
                           postfix: new HarmonyMethod(patchType, nameof(HVBADoSurgicalInspectionPostfix)));
            harmony.Patch(AccessTools.Method(typeof(TraitModExtensionUtility), nameof(TraitModExtensionUtility.AddTraitGrantedStuff)),
                           postfix: new HarmonyMethod(patchType, nameof(HVBAAddTraitGrantedStuffPostfix)));
            if (ModsConfig.IsActive("lts.I"))
            {
                harmony.Patch(AccessTools.Method(typeof(PregnancyUtility), nameof(PregnancyUtility.ApplyBirthOutcome)),
                               postfix: new HarmonyMethod(patchType, nameof(HVB_ApplyBirthOutcomePostfix)));
            }
        }
        internal static object GetInstanceField(Type type, object instance, string fieldName)
        {
            BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Static;
            FieldInfo field = type.GetField(fieldName, bindFlags);
            return field.GetValue(instance);
        }
        //see HediffComp_InhumanRambler in Voidshards_SpecificBionicMechanics.csc
        public static bool HVBATryInteractWithPrefix(ref bool __result, Pawn_InteractionsTracker __instance, ref InteractionDef intDef)
        {
            if (Find.Anomaly.Level > 0)
            {
                Pawn pawn = GetInstanceField(typeof(Pawn_InteractionsTracker), __instance, "pawn") as Pawn;
                float chanceToRamble = 0f;
                foreach (Hediff h in pawn.health.hediffSet.hediffs)
                {
                    if (h is HediffWithComps hwc)
                    {
                        HediffComp_InhumanRambler ir = hwc.TryGetComp<HediffComp_InhumanRambler>();
                        if (ir != null && (Find.Anomaly.Level < 6 || !ir.Props.noChanceIfVoidDisrupted))
                        {
                            chanceToRamble += ir.Props.chancePerSeverity * hwc.Severity;
                        }
                    }
                }
                if (Rand.Chance(chanceToRamble))
                {
                    __result = true;
                    intDef = InteractionDefOf.InhumanRambling;
                    SocialInteractionUtility.ImitateInteractionWithNoPawn(pawn, InteractionDefOf.InhumanRambling);
                    GenClamor.DoClamor(pawn, 9.9f, delegate (Thing source, Pawn hearer)
                    {
                        if (hearer != source && hearer.needs.mood != null)
                        {
                            hearer.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.HeardInhumanRambling, pawn, null);
                        }
                    });
                    return false;
                }
            }
            return true;
        }
        /*Dark research projects can only be unlocked by the player encountering a new specific Thing or Incident. All creepjoiners have the same ThingDef, so we resort to an incident to unlock the research project.
         * The creepjoiner intro letter for a Homunculus encourages you to surgically inspect them, and you obviously want to inspect most CJs anyways. Doing so creates an incident that unlocks the research to unlock voidshards.
         * Convoluted? I... guess so?*/
        public static void HVBADoSurgicalInspectionPostfix(Pawn_CreepJoinerTracker __instance, Pawn surgeon)
        {
            if (__instance.benefit == HVBAnomalyDefOf.HVB_HomunculusBenefit && __instance.Pawn.story != null && __instance.Pawn.story.traits.HasTrait(HVBAnomalyDefOf.HVB_HomunculusTrait) && surgeon.MapHeld != null && HVBAnomalyDefOf.HVB_VoidshardBionics.IsHidden)
            {
                IncidentParms parms = StorytellerUtility.DefaultParmsNow(HVBAnomalyDefOf.HVB_HomunculusExamined.category, Find.AnyPlayerHomeMap);
                IncidentParms incidentParms = new IncidentParms();
                incidentParms.target = surgeon.MapHeld;
                incidentParms.points = 100;
                incidentParms.forced = true;
                HVBAnomalyDefOf.HVB_HomunculusExamined.Worker.TryExecute(parms);
                ChoiceLetter notification = LetterMaker.MakeLetter(
                "HVB_HomunculusLetter".Translate(), "HVB_HomunculusText".Translate().Formatted(surgeon.Named("PAWN"),__instance.Pawn.Named("CREEP")).AdjustedFor(__instance.Pawn,"CREEP",true).Resolve(), LetterDefOf.NeutralEvent, new LookTargets(surgeon), null, null, null);
                Find.LetterStack.ReceiveLetter(notification, null);
            }
        }
        //the main effect of Homunculus is to add a shitton of Voidshard bionics to the pawn. (It has other effects, but those are not covered by this Harmony patch). To minimize abuse with dev mode, CharEdit, or similar, it only works on creepjoiners
        public static void HVBAAddTraitGrantedStuffPostfix(Trait t, Pawn pawn)
        {
            if (t.def == HVBAnomalyDefOf.HVB_HomunculusTrait && pawn.def == ThingDefOf.CreepJoiner)
            {
                Func<HediffDef, bool> predicate;
                predicate = (HediffDef x) => x.comps != null && x.HasComp(typeof(HediffComp_Voidshard));
                IEnumerable<HediffDef> voidshards = DefDatabase<HediffDef>.AllDefs.Where(predicate);
                if (voidshards.Any<HediffDef>())
                {
                    int max = new IntRange(5, 8).RandomInRange;
                    for (int i = 0; i <= Math.Min(voidshards.Count(),max); i++)
                    {
                        HediffDef toInstall = voidshards.RandomElement();
                        IEnumerable<RecipeDef> source = from x in DefDatabase<RecipeDef>.AllDefs
                                                        where x.addsHediff == toInstall
                                                        select x;
                        if (source.Any<RecipeDef>())
                        {
                            RecipeDef recipeDef = source.RandomElement();
                            if (recipeDef.appliedOnFixedBodyParts != null)
                            {
                                List<BodyPartRecord> bodyPartsToHitUp = new List<BodyPartRecord>();
                                foreach (BodyPartDef bpd in recipeDef.appliedOnFixedBodyParts)
                                {
                                    List<BodyPartRecord> bpList = pawn.RaceProps.body.AllParts;
                                    for (int k = 0; k < bpList.Count; k++)
                                    {
                                        BodyPartRecord bodyPartRecord = bpList[k];
                                        if (bodyPartRecord.def == bpd)
                                        {
                                            bodyPartsToHitUp.Add(bodyPartRecord);
                                        }
                                    }
                                }
                                if (bodyPartsToHitUp.Count > 0)
                                {
                                    BodyPartRecord finalBpr = bodyPartsToHitUp.RandomElement();
                                    Hediff hediff = HediffMaker.MakeHediff(toInstall,pawn,finalBpr);
                                    pawn.health.AddHediff(hediff, finalBpr);
                                }
                            }
                        }
                    }
                }
            }
        }
        //Integrated Implants adds the archowomb, which imposes a condition on all pawns birthed by the host. The Voidshard version of that imposes a different, more Void-themed condition
        public static void HVB_ApplyBirthOutcomePostfix(ref Thing __result, Thing birtherThing)
        {
            if (ModsConfig.BiotechActive && birtherThing is Pawn p && __result is Pawn baby && p.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamedSilentFail("HVB_VoidshardWomb")))
            {
                baby.health.AddHediff(HVBAnomalyDefOf.HVB_Voidborn);
                if (Rand.Chance(0.5f))
                {
                    int num = Rand.RangeInclusive(0, 3);
                    switch (num)
                    {
                        case 0:
                            FleshbeastUtility.TryGiveMutation(baby, HediffDefOf.Tentacle);
                            return;
                        case 1:
                            FleshbeastUtility.TryGiveMutation(baby, HediffDefOf.FleshWhip);
                            return;
                        case 2:
                            FleshbeastUtility.TryGiveMutation(baby, HediffDefOf.FleshmassLung);
                            return;
                        case 3:
                            FleshbeastUtility.TryGiveMutation(baby, HediffDefOf.FleshmassStomach);
                            return;
                        default:
                            Log.Error("Unhandled outcome in voidwomb trigger interaction " + num.ToString());
                            break;
                    }
                }
            }
        }
    }
}
