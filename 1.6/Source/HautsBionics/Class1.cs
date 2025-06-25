using System;
using System.Collections;
using System.Reflection;
using RimWorld;
using Verse;
using HarmonyLib;
using System.Collections.Generic;
using Verse.AI;
using HautsFramework;
using System.Text;
using UnityEngine;
using RimWorld.Planet;
using Verse.Sound;
using System.Linq;
using VEF.Abilities;
using VEF;
using static RimWorld.Dialog_BeginRitual;
using Verse.Noise;
using static HarmonyLib.Code;
using System.Runtime.CompilerServices;
using static UnityEngine.GraphicsBuffer;
using System.Security.Cryptography;
using System.Net.NetworkInformation;

namespace HautsBionics
{
    [StaticConstructorOnStartup]
    public static class HautsBionics
    {
        private static readonly Type patchType = typeof(HautsBionics);
        static HautsBionics()
        {
            Harmony harmony = new Harmony(id: "rimworld.hautarche.hautsbionics.main");
            harmony.Patch(AccessTools.Method(typeof(ThingSetMaker), nameof(ThingSetMaker.Generate), new[] { typeof(ThingSetMakerParams) }),
                          postfix: new HarmonyMethod(patchType, nameof(HVB_ThingSetMaker_GeneratePostfix)));
            MethodInfo methodInfo = typeof(PawnTechHediffsGenerator).GetMethod("InstallPart", BindingFlags.NonPublic | BindingFlags.Static);
            harmony.Patch(methodInfo,
                          prefix: new HarmonyMethod(patchType, nameof(HVB_InstallPartPrefix)));
            harmony.Patch(AccessTools.Method(typeof(PawnTechHediffsGenerator), nameof(PawnTechHediffsGenerator.GenerateTechHediffsFor)),
                          postfix: new HarmonyMethod(patchType, nameof(HVB_GenerateTechHediffsForPostfix)));
            harmony.Patch(AccessTools.Method(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.RestorePart)),
                          postfix: new HarmonyMethod(patchType, nameof(HVB_RestorePartPostfix)));
            harmony.Patch(AccessTools.Method(typeof(ArmorUtility), nameof(ArmorUtility.GetPostArmorDamage)),
                          postfix: new HarmonyMethod(patchType, nameof(HVB_GetPostArmorDamagePostfix)));
            harmony.Patch(AccessTools.Method(typeof(Pawn_InteractionsTracker), nameof(Pawn_InteractionsTracker.TryInteractWith)),
                          prefix: new HarmonyMethod(patchType, nameof(HVB_TryInteractWithPrefix)));
            harmony.Patch(AccessTools.Method(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState)),
                          postfix: new HarmonyMethod(patchType, nameof(HVB_TryStartMentalStatePostfix)));
            harmony.Patch(AccessTools.Method(typeof(FoodUtility), nameof(FoodUtility.ThoughtsFromIngesting)),
                          prefix: new HarmonyMethod(patchType, nameof(HVB_ThoughtsFromIngestingPrefix)));
            MethodInfo methodInfo2 = typeof(ImmunityHandler).GetMethod("AnyHediffMakesFullyImmuneTo", BindingFlags.Instance | BindingFlags.NonPublic);
            harmony.Patch(methodInfo2,
                          postfix: new HarmonyMethod(patchType, nameof(HVB_AnyHediffMakesFullyImmuneToPostfix)));
            methodInfo = typeof(PawnFlyer).GetMethod("LandingEffects", BindingFlags.NonPublic | BindingFlags.Instance);
            harmony.Patch(methodInfo,
                          postfix: new HarmonyMethod(patchType, nameof(HVB_LandingEffectsPostfix)));
            methodInfo = typeof(AbilityPawnFlyer).GetMethod("LandingEffects", BindingFlags.NonPublic | BindingFlags.Instance);
            Log.Message("HVB_Initialize".Translate().CapitalizeFirst());
        }
        internal static object GetInstanceField(Type type, object instance, string fieldName)
        {
            BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Static;
            FieldInfo field = type.GetField(fieldName, bindFlags);
            return field.GetValue(instance);
        }
        public static void HVB_ThingSetMaker_GeneratePostfix(ref List<Thing> __result, ThingSetMakerParams parms)
        {
            if (parms.traderDef != null && parms.traderDef.stockGenerators != null)
            {
                foreach (StockGenerator sg in parms.traderDef.stockGenerators)
                {
                    if (sg is StockGenerator_Tag sgt && (sgt.tradeTag.Equals("TechHediff") || sgt.tradeTag.Equals("Bionic") || sgt.tradeTag.Equals("ImplantEmpireCommon") || sgt.tradeTag.Equals("ImplantEmpireRoyal")))
                    {
                        for (int i = 0; i < Math.Floor(HVB_Mod.settings.bionicsForSaleMultiplier) - 1; i++)
                        {
                            Faction makingFaction = parms.makingFaction;
                            int forTile;
                            if (parms.tile != null)
                            {
                                forTile = parms.tile.Value;
                            } else if (Find.AnyPlayerHomeMap != null) {
                                forTile = Find.AnyPlayerHomeMap.Tile;
                            } else if (Find.CurrentMap != null) {
                                forTile = Find.CurrentMap.Tile;
                            } else {
                                forTile = -1;
                            }
                            foreach (Thing thing in sgt.GenerateThings(forTile, makingFaction))
                            {
                                if (!thing.def.tradeability.TraderCanSell())
                                {
                                    Log.Error(string.Concat(new object[]
                                    {
                                    parms.traderDef,
                                    " generated carrying ",
                                    thing,
                                    " which can't be sold by traders. Ignoring..."
                                    }));
                                } else {
                                    thing.PostGeneratedForTrader(parms.traderDef, forTile, makingFaction);
                                    __result.Add(thing);
                                }
                            }
                        }
                    }
                }
            }
        }
        public static void HVB_InstallPartPrefix(Pawn pawn, ref ThingDef partDef)
        {
            if (pawn.GetMainPsylinkSource() != null)
            {
                if (partDef == HVBThingDefOf.HVB_PsychicFoilBarrier)
                {
                    if (ModsConfig.IdeologyActive && Rand.Value <= 0.25f && !pawn.health.hediffSet.HasHediff(HVBDefOf.HVB_Gaucrown))
                    {
                        partDef = HVBThingDefOf.HVB_Gaucrown;
                    } else if (Rand.Value <= 0.83f) {
                        if (!pawn.health.hediffSet.HasHediff(HVBDefOf.HVB_HardheadProtector))
                        {
                            partDef = HVBThingDefOf.HVB_HardheadProtector;
                        } else if (!pawn.health.hediffSet.HasHediff(HVBDefOf.HVB_PanoptesSkull)) {
                            partDef = HVBThingDefOf.HVB_PanoptesSkull;
                        }
                    } else if (!pawn.health.hediffSet.HasHediff(HVBDefOf.HVB_PanoptesSkull)) {
                        partDef = HVBThingDefOf.HVB_PanoptesSkull;
                    }
                }
            }
        }
        public static void HVB_GenerateTechHediffsForPostfix(Pawn pawn)
        {
            for (int i = pawn.health.hediffSet.hediffs.Count - 1; i >= 0; i--)
            {
                if (pawn.health.hediffSet.hediffs[i] is HediffWithComps hwc && hwc.Part != null && pawn.health.hediffSet.PartIsMissing(hwc.Part))
                {
                    foreach (HediffComp hc in hwc.comps)
                    {
                        if (hc is HediffComp_AddOn)
                        {
                            BodyPartRecord part = hwc.Part;
                            while (pawn.health.hediffSet.PartIsMissing(part))
                            {
                                if (part.parent != null)
                                {
                                    part = part.parent;
                                    if (!pawn.health.hediffSet.PartIsMissing(part))
                                    {
                                        hwc.Part = part;
                                        break;
                                    }
                                } else {
                                    pawn.health.hediffSet.hediffs.RemoveAt(i);
                                    hwc.PostRemoved();
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            if (pawn.RaceProps.Humanlike)
            {
                if (pawn.GetMainPsylinkSource() != null)
                {
                    if (Rand.Value <= 0.1f)
                    {
                        HVBUtility.HVB_DoSurgery(pawn, HVBDefOf.HVB_PerformPsychicTrepanation);
                    }
                    if (Rand.Value <= 0.17f && pawn.kindDef.techHediffsTags != null && pawn.kindDef.techHediffsTags.Contains("ImplantEmpireRoyal"))
                    {
                        int max = pawn.kindDef.techHediffsMaxAmount;
                        foreach (Hediff h in pawn.health.hediffSet.hediffs)
                        {
                            if (h is Hediff_Implant)
                            {
                                max--;
                            }
                        }
                        if (max > 0 || (max == 0 && Rand.Value <= 0.5f))
                        {
                            List<ThingDef> allDefs = DefDatabase<ThingDef>.AllDefs.Where((ThingDef x) => x.isTechHediff && x.techHediffsTags != null && x.techHediffsTags.Contains("HVB_Warlock") && (!pawn.WorkTagIsDisabled(WorkTags.Violent) || !x.violentTechHediff) && (pawn.kindDef.techHediffsDisallowTags == null || !pawn.kindDef.techHediffsDisallowTags.Any((string tag) => x.techHediffsTags.Contains(tag)))).ToList();
                            if ((float)pawn.GetPsylinkLevel() / (float)pawn.GetMaxPsylinkLevel() >= 0.8f)
                            {
                                List<ThingDef> allDefs2 = DefDatabase<ThingDef>.AllDefs.Where((ThingDef x) => x.isTechHediff && x.techHediffsTags != null && x.techHediffsTags.Contains("HVB_SuperPsychic") && (!pawn.WorkTagIsDisabled(WorkTags.Violent) || !x.violentTechHediff) && (pawn.kindDef.techHediffsDisallowTags == null || !pawn.kindDef.techHediffsDisallowTags.Any((string tag) => x.techHediffsTags.Contains(tag)))).ToList();
                                allDefs.AddRange(allDefs2);
                            }
                            if (allDefs.Any<ThingDef>())
                            {
                                ThingDef thingDef = allDefs.RandomElement<ThingDef>();
                                IEnumerable<RecipeDef> source = from x in DefDatabase<RecipeDef>.AllDefs
                                                                where x.IsIngredient(thingDef) && pawn.def.AllRecipes.Contains(x)
                                                                select x;
                                if (source.Any<RecipeDef>())
                                {
                                    RecipeDef recipeDef = source.RandomElement<RecipeDef>();
                                    if (recipeDef.Worker.GetPartsToApplyOn(pawn, recipeDef).Any<BodyPartRecord>())
                                    {
                                        recipeDef.Worker.ApplyOnPawn(pawn, recipeDef.Worker.GetPartsToApplyOn(pawn, recipeDef).RandomElement<BodyPartRecord>(), null, new List<Thing>(), null);
                                        return;
                                    }
                                } else {
                                    CompProperties_UseEffectInstallImplant compProperties = thingDef.GetCompProperties<CompProperties_UseEffectInstallImplant>();
                                    if (compProperties != null)
                                    {
                                        List<BodyPartRecord> partsWithDef = pawn.RaceProps.body.GetPartsWithDef(compProperties.bodyPart);
                                        pawn.health.AddHediff(compProperties.hediffDef, partsWithDef.NullOrEmpty<BodyPartRecord>() ? null : partsWithDef.RandomElement<BodyPartRecord>(), null, null);
                                    }
                                }
                            }
                        }
                    }
                } else if (Rand.Value <= 0.004f) {
                    HVBUtility.HVB_DoSurgery(pawn, HVBDefOf.HVB_PerformPsychicTrepanation);
                }
                if (pawn.trader == null && !pawn.kindDef.trader && pawn.kindDef.titleRequired == null && pawn.kindDef.minTitleRequired == null && (pawn.kindDef.royalTitleChance <= 0f || pawn.kindDef.titleSelectOne == null))
                {
                    if (Rand.Value <= (1 - pawn.health.capacities.GetLevel(PawnCapacityDefOf.Breathing)))
                    {
                        HVBUtility.HVB_DoSurgery(pawn, HVBDefOf.HVB_InstallTrachealIntubation);
                    }
                    float resocChance = 0f;
                    if (pawn.Faction != null)
                    {
                        if (pawn.Faction.leader == pawn)
                        {
                            resocChance = 0f;
                        } else if (pawn.Faction.def.techLevel == TechLevel.Industrial) {
                            resocChance = 0.005f;
                        } else if (pawn.Faction.def.techLevel > TechLevel.Industrial) {
                            resocChance = 0.02f;
                        }
                    } else {
                        resocChance = 0.0025f;
                    }
                    if (Rand.Value <= resocChance)
                    {
                        HVBUtility.HVB_DoSurgery(pawn, HVBDefOf.HVB_InstallNeuralResocialization);
                    }
                }
            }
        }
        public static void HVB_RestorePartPostfix(Pawn_HealthTracker __instance, BodyPartRecord part)
        {
            Pawn pawn = GetInstanceField(typeof(Pawn_HealthTracker), __instance, "pawn") as Pawn;
            if (!PawnGenerator.IsBeingGenerated(pawn))
            {
                for (int i = __instance.hediffSet.hediffs.Count - 1; i >= 0; i--)
                {
                    Hediff hediff = __instance.hediffSet.hediffs[i];
                    if (hediff.Part == part && hediff.TryGetComp<HediffComp_AddOn>() != null)
                    {
                        __instance.hediffSet.hediffs.RemoveAt(i);
                        hediff.PostRemoved();
                    }
                }
            }
        }
        public static void HVB_GetPostArmorDamagePostfix(ref float __result, Pawn pawn, BodyPartRecord part)
        {
            if ((part.IsInGroup(BodyPartGroupDefOf.FullHead) || part.IsInGroup(BodyPartGroupDefOf.UpperHead)) && pawn.health.hediffSet.HasHediff(HVBDefOf.HVB_HardheadProtector))
            {
                __result *= 0.7f;
            }
            if (part.IsInGroup(BodyPartGroupDefOf.Torso) && pawn.health.hediffSet.HasHediff(HVBDefOf.HVB_CenterMassLaminar))
            {
                __result *= 0.8f;
            }
        }
        public static bool HVB_TryInteractWithPrefix(ref bool __result, Pawn_InteractionsTracker __instance, ref InteractionDef intDef)
        {
            Pawn pawn = GetInstanceField(typeof(Pawn_InteractionsTracker), __instance, "pawn") as Pawn;
            if ((intDef == InteractionDefOf.Insult || intDef == DefDatabase<InteractionDef>.GetNamed("Slight")) && pawn.health.hediffSet.HasHediff(HVBDefOf.HVB_CognoCensor))
            {
                pawn.needs.mood.thoughts.memories.TryGainMemory(HVBDefOf.HVB_CognoCensorship);
                __result = true;
                return false;
            }
            return true;
        }
        public static void HVB_TryStartMentalStatePostfix(ref bool __result, MentalStateHandler __instance, MentalStateDef stateDef)
        {
            Pawn pawn = GetInstanceField(typeof(MentalStateHandler), __instance, "pawn") as Pawn;
            if (__result && stateDef != MentalStateDefOf.PanicFlee && Rand.Value <= 0.1f && pawn.health.hediffSet.HasHediff(HVBDefOf.HVB_NeuralResocialization))
            {
                pawn.health.RemoveHediff(pawn.health.hediffSet.GetFirstHediffOfDef(HVBDefOf.HVB_NeuralResocialization));
                if (PawnUtility.ShouldSendNotificationAbout(pawn))
                {
                    Messages.Message("HVB_ResocBroke".Translate().CapitalizeFirst().Formatted(pawn.Named("PAWN")).AdjustedFor(pawn, "PAWN", true).Resolve(), pawn, MessageTypeDefOf.NegativeHealthEvent, true);
                }
            }
        }
        public static bool HVB_ThoughtsFromIngestingPrefix(ref List<FoodUtility.ThoughtFromIngesting> __result, Pawn ingester, Thing foodSource, ThingDef foodDef)
        {
            if (ingester.health.hediffSet.HasHediff(HVBDefOf.HVB_PurifierJaw) && FoodUtility.GetMeatSourceCategoryFromCorpse(foodSource) != MeatSourceCategory.Humanlike)
            {
                List<FoodUtility.ThoughtFromIngesting> ingestThoughts = new List<FoodUtility.ThoughtFromIngesting>();
                __result = ingestThoughts;
                return false;
            }
            return true;
        }
        public static void HVB_AnyHediffMakesFullyImmuneToPostfix(ref bool __result, ImmunityHandler __instance, ref Hediff sourceHediff)
        {
            if (__instance.pawn.health.hediffSet.HasHediff(HVBDefOf.HVB_ArchotechNeutralizer))
            {
                sourceHediff = __instance.pawn.health.hediffSet.GetFirstHediffOfDef(HVBDefOf.HVB_ArchotechNeutralizer);
                __result = true;
            }
        }
        public static void HVB_LandingEffectsPostfix(PawnFlyer __instance)
        {
            HVBUtility.HVB_LandingEffects(__instance);
        }
    }
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
    public class Recipe_AddImplant : Recipe_Surgery
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            return MedicalRecipesUtility.GetFixedPartsToApplyOn(recipe, pawn, (BodyPartRecord record) => pawn.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Undefined, null, null).Contains(record) && !pawn.health.hediffSet.hediffs.Any((Hediff x) => x.Part == record && (x.def == recipe.addsHediff || !recipe.CompatibleWithHediff(x.def))));
        }
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (billDoer != null)
            {
                if (base.CheckSurgeryFail(billDoer, pawn, ingredients, part, bill))
                {
                    return;
                }
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, new object[]
                {
                    billDoer,
                    pawn
                });
            }
            pawn.health.AddHediff(this.recipe.addsHediff, part, null, null);
        }
    }
    public class Recipe_AddTongue : Recipe_Surgery
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            return MedicalRecipesUtility.GetFixedPartsToApplyOn(recipe, pawn, (BodyPartRecord record) => pawn.def.race.body.AllParts.Contains(record) && (record.parent == null || !pawn.health.hediffSet.PartIsMissing(record.parent)) && !pawn.health.hediffSet.hediffs.Any((Hediff x) => x.Part == record && (x.def == recipe.addsHediff || !recipe.CompatibleWithHediff(x.def))));
        }
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            bool flag2 = !PawnGenerator.IsBeingGenerated(pawn) && this.IsViolationOnPawn(pawn, part, Faction.OfPlayer);
            Hediff hediff = null;
            if (billDoer != null)
            {
                if (base.CheckSurgeryFail(billDoer, pawn, ingredients, part, bill))
                {
                    return;
                }
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, new object[] { billDoer, pawn });
                hediff = pawn.health.hediffSet.GetDirectlyAddedPartFor(part);
                if (part != null)
                {
                    MedicalRecipesUtility.RestorePartAndSpawnAllPreviousParts(pawn, part, billDoer.Position, billDoer.Map);
                }
                if (MedicalRecipesUtility.IsClean(pawn, part) && flag2 && part.def.spawnThingOnRemoved != null)
                {
                    ThoughtUtility.GiveThoughtsForPawnOrganHarvested(pawn, billDoer);
                }
                if (flag2)
                {
                    base.ReportViolation(pawn, billDoer, pawn.HomeFaction, -70, null);
                }
                if (ModsConfig.IdeologyActive)
                {
                    Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.InstalledProsthetic, billDoer.Named(HistoryEventArgsNames.Doer)), true);
                }
            } else if (pawn.Map != null) {
                if (part != null)
                {
                    MedicalRecipesUtility.RestorePartAndSpawnAllPreviousParts(pawn, part, pawn.Position, pawn.Map);
                }
            } else if (part != null) {
                pawn.health.RestorePart(part, null, true);
            }
            pawn.health.AddHediff(this.recipe.addsHediff, part, null, null);
            if (hediff != null)
            {
                hediff.Notify_SurgicallyReplaced(billDoer);
            }
        }
        public override bool IsViolationOnPawn(Pawn pawn, BodyPartRecord part, Faction billDoerFaction)
        {
            return ((pawn.Faction != billDoerFaction && pawn.Faction != null) || pawn.IsQuestLodger()) && (this.recipe.addsHediff.addedPartProps == null || !this.recipe.addsHediff.addedPartProps.betterThanNatural) && HealthUtility.PartRemovalIntent(pawn, part) == BodyPartRemovalIntent.Harvest;
        }
    }
    public class Recipe_AddImplantToLimb : Recipe_Surgery
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            return MedicalRecipesUtility.GetFixedPartsToApplyOn(recipe, pawn, (BodyPartRecord record) => pawn.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Undefined, null, null).Contains(record) && (record.groups.Contains(DefDatabase<BodyPartGroupDef>.GetNamed("Hands")) || record.groups.Contains(DefDatabase<BodyPartGroupDef>.GetNamed("Feet")) || pawn.health.hediffSet.HasDirectlyAddedPartFor(record)) && !pawn.health.hediffSet.hediffs.Any((Hediff x) => x.Part == record && (x.def == recipe.addsHediff || !recipe.CompatibleWithHediff(x.def))));
        }
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (billDoer != null)
            {
                if (base.CheckSurgeryFail(billDoer, pawn, ingredients, part, bill))
                {
                    return;
                }
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, new object[]
                {
                    billDoer,
                    pawn
                });
            }
            pawn.health.AddHediff(this.recipe.addsHediff, part, null, null);
        }
    }
    public class Recipe_InstallLevellingImplant : Recipe_Surgery
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            Hediff h = pawn.health.hediffSet.GetFirstHediffOfDef(recipe.addsHediff);
            if (h != null && h is Hediff_Level hl && hl.level >= hl.def.maxSeverity)
            {
                return MedicalRecipesUtility.GetFixedPartsToApplyOn(recipe, pawn, (BodyPartRecord record) => hl.level < hl.def.maxSeverity);
            }
            return MedicalRecipesUtility.GetFixedPartsToApplyOn(recipe, pawn, (BodyPartRecord record) => !pawn.health.hediffSet.hediffs.Any((Hediff x) => x.Part == record && !recipe.CompatibleWithHediff(x.def)));
        }
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (billDoer != null)
            {
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, new object[]
                {
                    billDoer,
                    pawn
                });
                Hediff firstHediffOfDef = pawn.health.hediffSet.GetFirstHediffOfDef(this.recipe.addsHediff, false);
                if (firstHediffOfDef == null)
                {
                    pawn.health.AddHediff(this.recipe.addsHediff, part, null, null);
                }
                else
                {
                    ((Hediff_Level)firstHediffOfDef).ChangeLevel(1);
                }
                if (!PawnGenerator.IsBeingGenerated(pawn) && this.IsViolationOnPawn(pawn, part, Faction.OfPlayer))
                {
                    base.ReportViolation(pawn, billDoer, pawn.HomeFaction, -70, null);
                }
                if (ModsConfig.IdeologyActive)
                {
                    Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.InstalledProsthetic, billDoer.Named(HistoryEventArgsNames.Doer)), true);
                }
            }
        }
    }
    public class Recipe_InstallArtificialLevellingBodyPart : Recipe_Surgery
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            Hediff h = pawn.health.hediffSet.GetFirstHediffOfDef(recipe.addsHediff);
            if (h != null && h is Hediff_Level hl)
            {
                return MedicalRecipesUtility.GetFixedPartsToApplyOn(recipe, pawn, (BodyPartRecord record) => hl.level < hl.def.maxSeverity);
            }
            return MedicalRecipesUtility.GetFixedPartsToApplyOn(recipe, pawn, delegate (BodyPartRecord record)
            {
                IEnumerable<Hediff> source = from x in pawn.health.hediffSet.hediffs
                                             where x.Part == record
                                             select x;
                return (source.Count<Hediff>() != 1 || source.First<Hediff>().def != recipe.addsHediff) && (record.parent == null || pawn.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Undefined, null, null).Contains(record.parent)) && (!pawn.health.hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(record) || pawn.health.hediffSet.HasDirectlyAddedPartFor(record));
            });
        }
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            Hediff firstHediffOfDef = pawn.health.hediffSet.GetFirstHediffOfDef(this.recipe.addsHediff, false);
            bool flag = !PawnGenerator.IsBeingGenerated(pawn) && this.IsViolationOnPawn(pawn, part, Faction.OfPlayer);
            if (billDoer != null)
            {
                if (base.CheckSurgeryFail(billDoer, pawn, ingredients, part, bill))
                {
                    return;
                }
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, new object[]
                {
                    billDoer,
                    pawn
                });
                if (MedicalRecipesUtility.IsClean(pawn, part) && flag && part.def.spawnThingOnRemoved != null)
                {
                    ThoughtUtility.GiveThoughtsForPawnOrganHarvested(pawn, billDoer);
                }
                if (flag)
                {
                    base.ReportViolation(pawn, billDoer, pawn.HomeFaction, -70, null);
                }
                if (ModsConfig.IdeologyActive)
                {
                    Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.InstalledProsthetic, billDoer.Named(HistoryEventArgsNames.Doer)), true);
                }
            }
            if (firstHediffOfDef == null)
            {
                if (pawn.Map != null)
                {
                    MedicalRecipesUtility.RestorePartAndSpawnAllPreviousParts(pawn, part, pawn.Position, pawn.Map);
                }
                else
                {
                    pawn.health.RestorePart(part, null, true);
                }
                pawn.health.AddHediff(this.recipe.addsHediff, part, null, null);
            }
            else
            {
                ((Hediff_Level)firstHediffOfDef).ChangeLevel(1);
            }
        }
    }
    public class Recipe_AddMarrow : Recipe_Surgery
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            if (pawn.GetStatValue(HVBDefOf.HVB_RemainingMarrow) <= 0)
            {
                return MedicalRecipesUtility.GetFixedPartsToApplyOn(recipe, pawn, (BodyPartRecord record) => pawn.GetStatValue(HVBDefOf.HVB_RemainingMarrow) > 0);
            }
            return MedicalRecipesUtility.GetFixedPartsToApplyOn(recipe, pawn, (BodyPartRecord record) => !pawn.health.hediffSet.hediffs.Any((Hediff x) => x.Part == record && !recipe.CompatibleWithHediff(x.def)));
        }
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (billDoer != null)
            {
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, new object[]
                {
                    billDoer,
                    pawn
                });
                Hediff firstHediffOfDef = pawn.health.hediffSet.GetFirstHediffOfDef(this.recipe.addsHediff, false);
                if (firstHediffOfDef == null)
                {
                    pawn.health.AddHediff(this.recipe.addsHediff, part, null, null);
                }
                else
                {
                    ((Hediff_Level)firstHediffOfDef).ChangeLevel(1);
                }
                if (!PawnGenerator.IsBeingGenerated(pawn) && this.IsViolationOnPawn(pawn, part, Faction.OfPlayer))
                {
                    base.ReportViolation(pawn, billDoer, pawn.HomeFaction, -70, null);
                }
                if (ModsConfig.IdeologyActive)
                {
                    Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.InstalledProsthetic, billDoer.Named(HistoryEventArgsNames.Doer)), true);
                }
            }
        }
    }
    public class Recipe_RemoveLevellingImplant : Recipe_RemoveImplant
    {
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (pawn.story != null)
            {
                foreach (Trait t in pawn.story.traits.allTraits)
                {
                    if (t.def.HasModExtension<CannotRemoveBionicsFrom>())
                    {
                        return;
                    }
                }
            }
            MedicalRecipesUtility.IsClean(pawn, part);
            if (billDoer != null)
            {
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, new object[]
                {
                    billDoer,
                    pawn
                });
                if (!pawn.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Undefined, null, null).Contains(part))
                {
                    return;
                }
                Hediff hediff = pawn.health.hediffSet.hediffs.FirstOrDefault((Hediff x) => x.def == this.recipe.removesHediff);
                if (hediff != null)
                {
                    if (hediff.def.spawnThingOnRemoved != null)
                    {
                        GenSpawn.Spawn(hediff.def.spawnThingOnRemoved, billDoer.Position, billDoer.Map, WipeMode.Vanish);
                    }
                    ((Hediff_Level)hediff).ChangeLevel(-1);
                }
            }
            if (this.IsViolationOnPawn(pawn, part, Faction.OfPlayer))
            {
                base.ReportViolation(pawn, billDoer, pawn.HomeFaction, -70, null);
            }
        }
    }
    public class CompProperties_AbilityBansheeWail : CompProperties_AbilityNova
    {
        public int stunTicks;
        public PawnCapacityDef stunScalarCapacity = null;
        public StatDef stunScalarStat = null;
        public float aiHearingDifferenceToUse = 0.5f;
    }
    public class CompAbilityEffect_BansheeWail : CompAbilityEffect_Nova
    {
        public new CompProperties_AbilityBansheeWail Props
        {
            get
            {
                return (CompProperties_AbilityBansheeWail)this.props;
            }
        }
        public float StunTime(Pawn pawn)
        {
            float stunTime = this.Props.stunTicks;
            if (this.Props.stunScalarCapacity != null)
            {
                stunTime *= pawn.health.capacities.GetLevel(this.Props.stunScalarCapacity);
            }
            if (this.Props.stunScalarStat != null)
            {
                stunTime *= pawn.GetStatValue(this.Props.stunScalarStat);
            }
            return stunTime;
        }
        public override void AffectSelf()
        {
            base.AffectSelf();
            this.parent.pawn.stances.stunner.StunFor((int)(StunTime(this.parent.pawn)/2f), this.parent.pawn, false, true);
        }
        public override void AffectPawn(Pawn pawn)
        {
            base.AffectPawn(pawn);
            pawn.stances.stunner.StunFor((int)StunTime(pawn), this.parent.pawn, false, true);
        }
        public override bool VictimCounter()
        {
            this.hearingTotal = 0;
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(this.parent.pawn.Position, this.parent.pawn.Map, this.Radius, true))
            {
                if (thing is Pawn p && !p.stances.stunner.Stunned)
                {
                    if (p.HostileTo(this.parent.pawn) && !p.ThreatDisabled(this.parent.pawn) && this.parent.pawn.Map.attackTargetsCache.GetPotentialTargetsFor(this.parent.pawn).Contains(p))
                    {
                        this.hearingTotal += p.health.capacities.GetLevel(PawnCapacityDefOf.Hearing);
                    }
                    else if (this.Props.aiDislikesFriendlyFire && p.Faction != null)
                    {
                        this.hearingTotal -= p.health.capacities.GetLevel(PawnCapacityDefOf.Hearing);
                    }
                }
            }
            return this.parent.pawn.Spawned && this.hearingTotal > this.Props.aiHearingDifferenceToUse;
        }
        private float hearingTotal = 0;
    }
    public class CompAbilityEffect_EMPDischarge : CompAbilityEffect_Nova
    {
        public override void AffectSelf()
        {
            base.AffectSelf();
            GenExplosion.DoExplosion(this.parent.pawn.PositionHeld, this.parent.pawn.Map, Math.Min(this.Radius, this.Props.maxRadius), DamageDefOf.EMP, null, -1, -1f, null, null, null, null, null, 0f, 1, null, null, 255, false, null, 0f, 1, 0f, false, null, null, null, true, 1f, 0f, true, null, 1f);
        }
        public override bool VictimCounter()
        {
            if (HautsUtility.ReactsToEMP(this.parent.pawn))
            {
                return false;
            }
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(this.parent.pawn.Position, this.parent.pawn.Map, this.Radius, true))
            {
                if (thing.HostileTo(this.parent.pawn))
                {
                    if (thing is Building_Turret)
                    {
                        return true;
                    } else if (thing is Pawn p && HautsUtility.ReactsToEMP(p) && !p.ThreatDisabled(this.parent.pawn) && this.parent.pawn.Map.attackTargetsCache.GetPotentialTargetsFor(this.parent.pawn).Contains(p)) {
                        return true;
                    }
                }
            }
            return false;
        }
        public override float Radius {
            get {
                return base.Radius * (this.parent.pawn.BodySize+this.parent.pawn.GetStatValue(VEFDefOf.VEF_BodySize_Offset))* this.parent.pawn.GetStatValue(VEFDefOf.VEF_BodySize_Multiplier);
            }
        }
        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            if (base.AICanTargetNow(target))
            {
                if (target.Thing is Building_Turret)
                {
                    return true;
                }
            }
            return ((target.Pawn != null && target.Pawn.RaceProps.IsMechanoid) || target.Thing is Building_Turret) && base.AICanTargetNow(target);
        }
    }
    public class CompProperties_AbilityInflictMechanites : CompProperties_AbilityAiScansForTargets
    {
        public List<HediffDef> possibleHediffs;
    }
    public class CompAbilityEffect_InflictMechanites : CompAbilityEffect_AiScansForTargets
    {
        public new CompProperties_AbilityInflictMechanites Props
        {
            get
            {
                return (CompProperties_AbilityInflictMechanites)this.props;
            }
        }
        public override float Range
        {
            get
            {
                return Math.Max(base.Range, 2f);
            }
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Hediff hediff = HediffMaker.MakeHediff(this.Props.possibleHediffs.RandomElement(), target.Pawn, null);
            target.Pawn.health.AddHediff(hediff, null, null, null);
        }
        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            if (target.Pawn != null)
            {
                return this.SufficientlyPained(target.Pawn);
            }
            return false;
        }
        public override bool AdditionalQualifiers(Thing thing)
        {
            if (thing is Pawn p)
            {
                return this.SufficientlyPained(p);
            }
            return false;
        }
        public bool SufficientlyPained(Pawn pawn)
        {
            if (!pawn.ThreatDisabled(this.parent.pawn))
            {
                float avgPain = 0f;
                foreach (HediffDef h in this.Props.possibleHediffs)
                {
                    if (!pawn.health.hediffSet.HasHediff(h) && h.stages != null)
                    {
                        avgPain += h.stages[h.StageAtSeverity(h.initialSeverity)].painOffset;
                    }
                }
                avgPain /= this.Props.possibleHediffs.Count;
                return pawn.health.hediffSet.PainTotal + avgPain >= pawn.GetStatValue(StatDefOf.PainShockThreshold, true, -1);
            }
            return false;
        }
    }
    public class CompProperties_AbilityCO : CompProperties_AbilityGiveHediff
    {
        public HediffDef secondHediff;
        public float secondHediffSeverity;
        public BodyPartDef secondHediffPart;
    }
    public class CompAbilityEffect_GiveHediffCO : CompAbilityEffect_GiveHediff
    {
        public new CompProperties_AbilityCO Props
        {
            get
            {
                return (CompProperties_AbilityCO)this.props;
            }
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Hediff hediff = this.parent.pawn.health.hediffSet.GetFirstHediffOfDef(this.Props.secondHediff);
            if (hediff != null)
            {
                hediff.Severity += this.Props.secondHediffSeverity;
            } else {
                hediff = HediffMaker.MakeHediff(this.Props.secondHediff,this.parent.pawn, this.Props.secondHediffPart != null ? this.parent.pawn.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined,BodyPartDepth.Undefined).Where((BodyPartRecord bpr) => bpr.def == this.Props.secondHediffPart).RandomElement(): null);
                hediff.Severity = this.Props.secondHediffSeverity;
                this.parent.pawn.health.AddHediff(hediff,hediff.Part);
            }
        }
    }
    public class CompAbilityEffect_Toggle : CompAbilityEffect
    {
        public override void Apply(GlobalTargetInfo target)
        {
            this.enabled = !this.enabled;
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            this.enabled = !this.enabled;
        }
        public override string ExtraTooltipPart()
        {
            return this.enabled ? "HVB_Toggler".Translate() : "HVB_Toggler2".Translate();
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look<bool>(ref this.enabled, "enabled", true);
        }
        public bool enabled = true;
    }
    public class CompProperties_AbilityMrFantastic : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityMrFantastic()
        {
            this.compClass = typeof(CompAbilityEffect_MrFantastic);
        }
        public Color color;
    }
    public class CompAbilityEffect_MrFantastic : CompAbilityEffect
    {
        public new CompProperties_AbilityMrFantastic Props
        {
            get
            {
                return (CompProperties_AbilityMrFantastic)this.props;
            }
        }
        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return target.Pawn != null;
        }
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (target.HasThing)
            {
                if (target.Thing is Plant p)
                {
                    if (!p.def.plant.IsTree)
                    {
                        return false;
                    }
                } else if (target.Thing.def.category != ThingCategory.Building && target.Thing.def.category != ThingCategory.Pawn && (target.Thing.def.category != ThingCategory.Item || !target.Thing.def.EverHaulable)) {
                    return false;
                }
            }
            return base.Valid(target, throwMessages);
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (target.Thing != null)
            {
                if (target.Thing.def.category != ThingCategory.Building && target.Thing.def.category != ThingCategory.Plant && MassUtility.FreeSpace(this.parent.pawn) >= target.Thing.GetStatValue(StatDefOf.Mass))
                {
                    int ticksToDisappear = (Mathf.Max(target.Thing.PositionHeld.DistanceTo(this.parent.pawn.PositionHeld), 1f) / HVBDefOf.HVB_GrabFlyer.pawnFlyer.flightSpeed).SecondsToTicks();
                    this.DoLink(this.parent.pawn, target.Thing, this.parent.pawn.Position, this.parent.pawn.Map, ticksToDisappear);
                    if (!(target.Thing is Pawn))
                    {
                        this.parent.ResetCooldown();
                    }
                } else {
                    this.DoLink(target.Thing, this.parent.pawn, target.Cell, target.Thing.Map, (Mathf.Max(target.Thing.PositionHeld.DistanceTo(this.parent.pawn.PositionHeld), 1f) / HVBDefOf.HVB_GrabFlyer.pawnFlyer.flightSpeed).SecondsToTicks());
                }
            }
        }
        private void DoLink(Thing other, Thing flyer, IntVec3 destination, Map map, int ticksToDisappear)
        {
            GrabFlyer gFlyer = GrabFlyer.MakeFlyer(HVBDefOf.HVB_GrabFlyer, flyer, other, this.Props.color, destination, null, null, false, null, null, default(LocalTargetInfo));
            GenSpawn.Spawn(gFlyer, destination, map, WipeMode.Vanish);
        }
    }
    public class GrabFlyer : Thing, IThingHolder
    {
        public Thing FlyingThing
        {
            get
            {
                if (this.innerContainer.InnerListForReading.Count <= 0)
                {
                    return null;
                }
                return this.innerContainer.InnerListForReading[0];
            }
        }
        public Pawn FlyingPawn
        {
            get
            {
                return this.FlyingThing as Pawn;
            }
        }
        public Thing CarriedThing
        {
            get
            {
                return this.carriedThing;
            }
        }
        public override Vector3 DrawPos
        {
            get
            {
                this.RecomputePosition();
                return this.effectivePos;
            }
        }
        private void RecomputePosition()
        {
            if (this.positionLastComputedTick == this.ticksFlying)
            {
                return;
            }
            this.positionLastComputedTick = this.ticksFlying;
            float num = (float)this.ticksFlying / (float)this.ticksFlightTime;
            float num2 = this.def.pawnFlyer.Worker.AdjustedProgress(num);
            this.effectiveHeight = this.def.pawnFlyer.Worker.GetHeight(num2);
            this.groundPos = Vector3.Lerp(this.startVec, this.DestinationPos, num2);
            Vector3 vector = Altitudes.AltIncVect * this.effectiveHeight;
            Vector3 vector2 = Vector3.forward * (this.def.pawnFlyer.heightFactor * this.effectiveHeight);
            this.effectivePos = this.groundPos + vector + vector2;
            base.Position = this.groundPos.ToIntVec3();
        }
        public ThingOwner GetDirectlyHeldThings()
        {
            return this.innerContainer;
        }
        public GrabFlyer()
        {
            this.innerContainer = new ThingOwner<Thing>(this);
        }
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            Effecter effecter = this.flightEffecter;
            if (effecter != null)
            {
                effecter.Cleanup();
            }
            base.Destroy(mode);
        }
        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, this.GetDirectlyHeldThings());
        }
        public Vector3 DestinationPos
        {
            get
            {
                Thing flyingThing = this.FlyingThing;
                return GenThing.TrueCenter(this.destCell, flyingThing.Rotation, flyingThing.def.size, flyingThing.def.Altitude);
            }
        }
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                float num = Mathf.Max(this.flightDistance, 1f) / this.def.pawnFlyer.flightSpeed;
                num = Mathf.Max(num, this.def.pawnFlyer.flightDurationMin);
                this.ticksFlightTime = num.SecondsToTicks();
                this.ticksFlying = 0;
            }
        }
        protected virtual void RespawnPawn()
        {
            Thing flyingThing = this.FlyingThing;
            this.LandingEffects();
            Thing thing;
            this.innerContainer.TryDrop(flyingThing, this.destCell, flyingThing.MapHeld, ThingPlaceMode.Direct, out thing, null, null, false);
            Pawn pawn = flyingThing as Pawn;
            if (((pawn != null) ? pawn.drafter : null) != null)
            {
                pawn.drafter.Drafted = this.pawnWasDrafted;
                pawn.drafter.FireAtWill = this.pawnCanFireAtWill;
            }
            flyingThing.Rotation = base.Rotation;
            if (this.carriedThing != null && this.innerContainer.TryDrop(this.carriedThing, this.destCell, flyingThing.MapHeld, ThingPlaceMode.Direct, out thing, null, null, false) && pawn != null)
            {
                this.carriedThing.DeSpawn(DestroyMode.Vanish);
                if (!pawn.carryTracker.TryStartCarry(this.carriedThing))
                {
                    Log.Error("Could not carry " + this.carriedThing.ToStringSafe<Thing>() + " after respawning flyer pawn.");
                }
            }
            if (pawn != null)
            {
                if (this.jobQueue != null)
                {
                    pawn.jobs.RestoreCapturedJobs(this.jobQueue, true);
                }
                pawn.jobs.CheckForJobOverride(0f);
                if (this.def.pawnFlyer.stunDurationTicksRange != IntRange.Zero)
                {
                    pawn.stances.stunner.StunFor(this.def.pawnFlyer.stunDurationTicksRange.RandomInRange, null, false, false, false);
                }
                if (this.triggeringAbility != null)
                {
                    RimWorld.Ability ability = pawn.abilities.GetAbility(this.triggeringAbility, false);
                    if (((ability != null) ? ability.comps : null) != null)
                    {
                        using (List<AbilityComp>.Enumerator enumerator = ability.comps.GetEnumerator())
                        {
                            while (enumerator.MoveNext())
                            {
                                ICompAbilityEffectOnJumpCompleted compAbilityEffectOnJumpCompleted;
                                if ((compAbilityEffectOnJumpCompleted = enumerator.Current as ICompAbilityEffectOnJumpCompleted) != null)
                                {
                                    compAbilityEffectOnJumpCompleted.OnJumpCompleted(this.startVec.ToIntVec3(), this.target);
                                }
                            }
                        }
                    }
                }
            }
        }
        private void LandingEffects()
        {
            SoundDef soundDef = this.soundLanding;
            if (soundDef != null)
            {
                soundDef.PlayOneShot(new TargetInfo(base.Position, base.Map, false));
            }
            FleckMaker.ThrowDustPuff(this.DestinationPos + Gen.RandomHorizontalVector(0.5f), base.Map, 2f);
        }
        protected override void TickInterval(int delta)
        {
            if (this.flightEffecter == null && this.flightEffecterDef != null)
            {
                this.flightEffecter = this.flightEffecterDef.Spawn();
                this.flightEffecter.Trigger(this, TargetInfo.Invalid, -1);
            } else {
                Effecter effecter = this.flightEffecter;
                if (effecter != null)
                {
                    effecter.EffectTick(this, TargetInfo.Invalid);
                }
            }
            if (this.ticksFlying >= this.ticksFlightTime)
            {
                this.RespawnPawn();
                this.Destroy(DestroyMode.Vanish);
            } else {
                if (this.IsHashIntervalTick(15, delta))
                {
                    this.CheckDestination();
                }
                this.innerContainer.DoTick();
            }
            this.ticksFlying += delta;
        }
        private void CheckDestination()
        {
            if (!JumpUtility.ValidJumpTarget(this.FlyingThing, base.Map, this.destCell))
            {
                int num = GenRadial.NumCellsInRadius(3.9f);
                for (int i = 0; i < num; i++)
                {
                    IntVec3 intVec = this.destCell + GenRadial.RadialPattern[i];
                    if (JumpUtility.ValidJumpTarget(this.FlyingThing, base.Map, intVec))
                    {
                        this.destCell = intVec;
                        return;
                    }
                }
            }
        }
        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            this.RecomputePosition();
            if (this.FlyingPawn != null)
            {
                this.FlyingPawn.DynamicDrawPhaseAt(phase, this.effectivePos, false);
            } else {
                Thing flyingThing = this.FlyingThing;
                if (flyingThing != null)
                {
                    flyingThing.DynamicDrawPhaseAt(phase, this.effectivePos, false);
                }
            }
            base.DynamicDrawPhaseAt(phase, drawLoc, flip);
        }
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            GenDraw.DrawLineBetween(this.other.TrueCenter(), this.DrawPos, AltitudeLayer.Conduits.AltitudeFor(), this.Rope, 0.25f);
            this.DrawShadow(this.groundPos, this.effectiveHeight);
            if (this.CarriedThing != null && this.FlyingPawn != null)
            {
                PawnRenderUtility.DrawCarriedThing(this.FlyingPawn, this.effectivePos, this.CarriedThing);
            }
        }
        private void DrawShadow(Vector3 drawLoc, float height)
        {
            Material shadowMaterial = this.def.pawnFlyer.ShadowMaterial;
            if (shadowMaterial == null)
            {
                return;
            }
            float num = Mathf.Lerp(1f, 0.6f, height);
            Vector3 vector = new Vector3(num, 1f, num);
            Matrix4x4 matrix4x = default(Matrix4x4);
            matrix4x.SetTRS(drawLoc, Quaternion.identity, vector);
            Graphics.DrawMesh(MeshPool.plane10, matrix4x, shadowMaterial, 0);
        }
        public static GrabFlyer MakeFlyer(ThingDef flyingDef, Thing thing, Thing other, Color color, IntVec3 destCell, EffecterDef flightEffecterDef, SoundDef landingSound, bool flyWithCarriedThing = false, Vector3? overrideStartVec = null, RimWorld.Ability triggeringAbility = null, LocalTargetInfo target = default(LocalTargetInfo))
        {
            GrabFlyer pawnFlyer = (GrabFlyer)ThingMaker.MakeThing(flyingDef, null);
            pawnFlyer.startVec = overrideStartVec ?? thing.TrueCenter();
            pawnFlyer.Rotation = thing.Rotation;
            pawnFlyer.flightDistance = thing.Position.DistanceTo(destCell);
            pawnFlyer.destCell = destCell;
            pawnFlyer.flightEffecterDef = flightEffecterDef;
            pawnFlyer.soundLanding = landingSound;
            pawnFlyer.triggeringAbility = ((triggeringAbility != null) ? triggeringAbility.def : null);
            pawnFlyer.target = target;
            pawnFlyer.other = other;
            pawnFlyer.ropeColor = color;
            pawnFlyer.Rope.color = pawnFlyer.ropeColor;
            if (thing is Pawn pawn)
            {
                pawnFlyer.pawnWasDrafted = pawn.Drafted;
                if (pawn.drafter != null)
                {
                    pawnFlyer.pawnCanFireAtWill = pawn.drafter.FireAtWill;
                }
                if (pawn.CurJob != null)
                {
                    if (pawn.CurJob.def == JobDefOf.CastJump)
                    {
                        pawn.jobs.EndCurrentJob(JobCondition.Succeeded, true, true);
                    } else {
                        pawn.jobs.SuspendCurrentJob(JobCondition.InterruptForced, true, null);
                    }
                }
                pawnFlyer.jobQueue = pawn.jobs.CaptureAndClearJobQueue();
                if (flyWithCarriedThing && pawn.carryTracker.CarriedThing != null && pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Direct, out pawnFlyer.carriedThing, null))
                {
                    if (pawnFlyer.carriedThing.holdingOwner != null)
                    {
                        pawnFlyer.carriedThing.holdingOwner.Remove(pawnFlyer.carriedThing);
                    }
                    pawnFlyer.carriedThing.DeSpawn(DestroyMode.Vanish);
                }
            }
            if (thing.Spawned)
            {
                thing.DeSpawn(DestroyMode.WillReplace);
            }
            if (!pawnFlyer.innerContainer.TryAdd(thing, true))
            {
                Log.Error("Could not add " + thing.ToStringSafe<Thing>() + " to a flyer.");
                thing.Destroy(DestroyMode.Vanish);
            }
            if (pawnFlyer.carriedThing != null && !pawnFlyer.innerContainer.TryAdd(pawnFlyer.carriedThing, true))
            {
                Log.Error("Could not add " + pawnFlyer.carriedThing.ToStringSafe<Thing>() + " to a flyer.");
            }
            return pawnFlyer;
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<Vector3>(ref this.startVec, "startVec", default(Vector3), false);
            Scribe_Values.Look<IntVec3>(ref this.destCell, "destCell", default(IntVec3), false);
            Scribe_Values.Look<Color>(ref this.ropeColor, "ropeColor", new Color(155f,165f,148f), false);
            Scribe_Values.Look<float>(ref this.flightDistance, "flightDistance", 0f, false);
            Scribe_Values.Look<bool>(ref this.pawnWasDrafted, "pawnWasDrafted", false, false);
            Scribe_Values.Look<bool>(ref this.pawnCanFireAtWill, "pawnCanFireAtWill", true, false);
            Scribe_Values.Look<int>(ref this.ticksFlightTime, "ticksFlightTime", 0, false);
            Scribe_Values.Look<int>(ref this.ticksFlying, "ticksFlying", 0, false);
            Scribe_Defs.Look<EffecterDef>(ref this.flightEffecterDef, "flightEffecterDef");
            Scribe_Defs.Look<SoundDef>(ref this.soundLanding, "soundLanding");
            Scribe_Defs.Look<RimWorld.AbilityDef>(ref this.triggeringAbility, "triggeringAbility");
            Scribe_References.Look<Thing>(ref this.carriedThing, "carriedThing", false);
            Scribe_References.Look<Thing>(ref this.other, "other", false);
            Scribe_Deep.Look<ThingOwner<Thing>>(ref this.innerContainer, "innerContainer", new object[] { this });
            Scribe_Deep.Look<JobQueue>(ref this.jobQueue, "jobQueue", Array.Empty<object>());
            Scribe_TargetInfo.Look(ref this.target, "target");
        }
        private Material Rope = MaterialPool.MatFrom("UI/Overlays/Rope", ShaderDatabase.SolidColor, GenColor.FromBytes(99, 70, 41, 255));
        private ThingOwner<Thing> innerContainer;
        protected Vector3 startVec;
        private IntVec3 destCell;
        private float flightDistance;
        private bool pawnWasDrafted;
        private bool pawnCanFireAtWill = true;
        protected int ticksFlightTime = 120;
        protected int ticksFlying;
        private JobQueue jobQueue;
        protected EffecterDef flightEffecterDef;
        protected SoundDef soundLanding;
        private Thing carriedThing;
        public Thing other;
        public Color ropeColor;
        private LocalTargetInfo target;
        private RimWorld.AbilityDef triggeringAbility;
        private Effecter flightEffecter;
        private int positionLastComputedTick = -1;
        private Vector3 groundPos;
        private Vector3 effectivePos;
        private float effectiveHeight;
    }
    public class GrabFlyerWorker : PawnFlyerWorker
    {
        public GrabFlyerWorker(PawnFlyerProperties properties) : base(properties)
        {
        }
        public override float GetHeight(float t)
        {
            return 0.1f;
        }
    }
    public class DamageWorker_DisintegrationBite : DamageWorker_Bite
    {
        public override DamageResult Apply(DamageInfo dinfo, Thing thing)
        {
            DamageResult dR = base.Apply(dinfo, thing);
            if (dinfo.Instigator is Pawn pawn && pawn.needs != null && pawn.needs.food != null && (thing is Plant || (thing is Pawn p && p.RaceProps.IsFlesh)))
            {
                pawn.needs.food.CurLevel += dR.totalDamageDealt / 1000f;
            }
            return dR;
        }
    }
    public class Hediff_SanityKeeper : Hediff_Implant
    {
        public override float PainOffset { 
            get {
                return Math.Max(0f,this.Severity - this.def.minSeverity);
            } 
        }
        public override void PostTickInterval(int delta)
        {
            base.PostTickInterval(delta);
            if (this.Severity > this.def.minSeverity)
            {
                if (this.pawn.IsHashIntervalTick(100, delta))
                {
                    this.pawn.health.hediffSet.DirtyCache();
                    if (this.pawn.health.ShouldBeDowned() && this.pawn.InMentalState)
                    {
                        Hediff hediff = HediffMaker.MakeHediff(HVBDefOf.HVB_CognoStraitjacketComa, this.pawn, this.pawn.health.hediffSet.GetBrain());
                        this.pawn.health.AddHediff(hediff);
                    }
                }
            }
        }
    }
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
                    } else if (h is Hediff_ChemicalDependency cd) {
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
    public class Hediff_Marrow : Hediff_Level
    {
        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            Hediff firstHediffOfDef = pawn.health.hediffSet.GetFirstHediffOfDef(HVBDefOf.HVB_AugmentedMarrowCounter, false);
            if (firstHediffOfDef == null)
            {
                Hediff hediff = HediffMaker.MakeHediff(HVBDefOf.HVB_AugmentedMarrowCounter, this.pawn, this.pawn.health.hediffSet.GetBrain());
                pawn.health.AddHediff(hediff, this.pawn.health.hediffSet.GetBrain(), null, null);
            }
            else
            {
                ((Hediff_MarrowCounter)firstHediffOfDef).MatchCurrentMarrowLevel();
            }
        }
        public override void ChangeLevel(int levelOffset)
        {
            base.ChangeLevel(levelOffset);
            Hediff firstHediffOfDef = pawn.health.hediffSet.GetFirstHediffOfDef(HVBDefOf.HVB_AugmentedMarrowCounter, false);
            if (firstHediffOfDef == null)
            {
                Hediff hediff = HediffMaker.MakeHediff(HVBDefOf.HVB_AugmentedMarrowCounter, this.pawn, this.pawn.health.hediffSet.GetBrain());
                pawn.health.AddHediff(hediff, this.pawn.health.hediffSet.GetBrain(), null, null);
                hediff.Severity = 1f;
            } else {
                firstHediffOfDef.Severity += levelOffset;
            }
        }
        public override void PostRemoved()
        {
            base.PostRemoved();
            Hediff firstHediffOfDef = pawn.health.hediffSet.GetFirstHediffOfDef(HVBDefOf.HVB_AugmentedMarrowCounter, false);
            if (firstHediffOfDef != null)
            {
                ((Hediff_MarrowCounter)firstHediffOfDef).MatchCurrentMarrowLevel();
            }
        }
    }
    public class Hediff_MarrowCounter : Hediff
    {
        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            this.MatchCurrentMarrowLevel();
        }
        public override void PostRemoved()
        {
            base.PostRemoved();
            int marrowTotal = this.CountMarrow();
            if (marrowTotal != 0)
            {
                Hediff hediff = HediffMaker.MakeHediff(this.def, this.pawn, this.pawn.health.hediffSet.GetBrain());
                this.pawn.health.AddHediff(hediff, this.pawn.health.hediffSet.GetBrain(), null, null);
                ((Hediff_MarrowCounter)hediff).MatchCurrentMarrowLevel();
            }
        }
        public int CountMarrow()
        {
            int marrowTotal = 0;
            foreach (Hediff h in this.pawn.health.hediffSet.hediffs)
            {
                if (h is Hediff_Marrow hm)
                {
                    marrowTotal += hm.level;
                }
            }
            return marrowTotal;
        }
        public void MatchCurrentMarrowLevel()
        {
            this.Severity = this.CountMarrow();
        }
    }
    public class Hediff_Resoc : Hediff_Implant
    {
        public override string TipStringExtra
        {
            get
            {
                if (this.hiddenTraits.Count > 0)
                {
                    string allHiddenTraits = "Masking the following traits: ";
                    for (int i = 0; i < this.hiddenTraits.Count; i++)
                    {
                        if (i != this.hiddenTraits.Count - 1)
                        {
                            allHiddenTraits += this.hiddenTraits[i].Label + ", ";
                        }
                        else
                        {
                            allHiddenTraits += this.hiddenTraits[i].Label;
                        }
                    }
                    return base.TipStringExtra + allHiddenTraits;
                }
                else
                {
                    return base.TipStringExtra;
                }
            }
        }
        public override void PostAdd(DamageInfo? dinfo)
        {
            if (this.pawn.GuestStatus == GuestStatus.Prisoner)
            {
                this.pawn.guest.will -= this.pawn.guest.will * Rand.Value;
                this.pawn.guest.resistance -= this.pawn.guest.resistance * Rand.Value;
            }
            if (!PawnGenerator.IsBeingGenerated(this.pawn))
            {
                this.MaskTraits();
            }
            if (this.pawn.health.hediffSet.HasHediff(HVBDefOf.HVB_BrokenResoc))
            {
                this.pawn.health.RemoveHediff(this.pawn.health.hediffSet.GetFirstHediffOfDef(HVBDefOf.HVB_BrokenResoc));
            }
            base.PostAdd(dinfo);
        }
        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (this.pawn.IsHashIntervalTick(150,delta))
            {
                this.MaskTraits();
            }
        }
        public override void PostRemoved()
        {
            if (this.pawn.story != null)
            {
                foreach (Trait t in this.hiddenTraits)
                {
                    Trait tnew = new Trait(t.def, t.Degree);
                    this.pawn.story.traits.GainTrait(tnew);
                    foreach (Trait t2 in this.pawn.story.traits.allTraits)
                    {
                        if (!t2.Suppressed && tnew != t2 && (tnew.def.ConflictsWith(t2) || tnew.def == t2.def))
                        {
                            t2.suppressedByTrait = true;
                        }
                    }
                }
            }
            if (Rand.Value <= 0.5f)
            {
                Hediff hediff = HediffMaker.MakeHediff(HVBDefOf.HVB_BrokenResoc, this.pawn, this.pawn.health.hediffSet.GetBrain());
                this.pawn.health.AddHediff(hediff, this.pawn.health.hediffSet.GetBrain());
            }
            base.PostRemoved();
        }
        private void MaskTraits()
        {
            if (this.pawn.story != null && !this.pawn.story.traits.allTraits.NullOrEmpty<Trait>())
            {
                bool hasRemovableTrait = true;
                while (hasRemovableTrait)
                {
                    List<Trait> removableTraits = new List<Trait>();
                    foreach (Trait t in this.pawn.story.traits.allTraits)
                    {
                        if (!HautsUtility.IsExciseTraitExempt(t.def,true) && !t.Suppressed && t.sourceGene == null)
                        {
                            removableTraits.Add(t);
                        }
                    }
                    if (removableTraits.Count == 0)
                    {
                        hasRemovableTrait = false;
                    }
                    else
                    {
                        Trait toRemove = removableTraits.RandomElement();
                        this.hiddenTraits.Add(toRemove);
                        this.pawn.story.traits.RemoveTrait(toRemove);
                        foreach (Trait t in this.pawn.story.traits.allTraits)
                        {
                            if (t.suppressedByTrait && toRemove.def.ConflictsWith(t))
                            {
                                t.suppressedByTrait = false;
                            }
                        }
                    }
                }
            }
        }
        public List<Trait> hiddenTraits = new List<Trait>();
    }
    public class Hediff_SwimUnlocked : Hediff
    {
        public override void PostRemoved()
        {
            base.PostRemoved();
            HVBUtility.HVB_EvaluateFins(this.pawn);
        }
    }
    public class HediffCompProperties_BionicShield : HediffCompProperties_DamageNegationShield
    {
        public HediffCompProperties_BionicShield()
        {
            this.compClass = typeof(HediffComp_BionicShield);
        }
    }
    public class HediffComp_BionicShield : HediffComp_DamageNegationShield
    {
        public new HediffCompProperties_BionicShield Props
        {
            get
            {
                return (HediffCompProperties_BionicShield)this.props;
            }
        }
        public override void RedetermineAllStats()
        {
            float netEnergyGainPerTick = this.Props.baseEnergyRechargeRate/60f;
            int netResetDelayTicks = this.Props.baseStartingTicksToReset;
            float netMaxEnergy = this.Props.baseMaxEnergy;
            bool anyGenerator = false;
            foreach (Hediff h in this.Pawn.health.hediffSet.hediffs)
            {
                HediffComp_ShieldGenerator hcsg = h.TryGetComp<HediffComp_ShieldGenerator>();
                if (hcsg != null && hcsg.Props.bionicShieldDef != null && hcsg.Props.bionicShieldDef == this.parent.def)
                {
                    netEnergyGainPerTick += hcsg.Props.energyRegenOffset/60f;
                    netResetDelayTicks = (int)(netResetDelayTicks*hcsg.Props.resetDelayFactor);
                    netMaxEnergy += hcsg.Props.maxEnergyOffset;
                    if (hcsg.Props.makesShield)
                    {
                        anyGenerator = true;
                    }
                }
            }
            netEnergyGainPerTick *= this.Props.rechargeRateScalar!=null?this.Pawn.GetStatValue(this.Props.rechargeRateScalar):1f;
            netMaxEnergy *= this.Props.maxEnergyScalar != null ? this.Pawn.GetStatValue(this.Props.maxEnergyScalar) : 1f;
            netMaxEnergy += this.Props.minSeverityToWork;
            this.EnergyGainPerTick = Math.Max(0f,netEnergyGainPerTick);
            this.ResetDelayTicks = Math.Max(1,netResetDelayTicks);
            this.MaxEnergy = Math.Max(0.001f,netMaxEnergy);
            if (!anyGenerator)
            {
                this.Pawn.health.RemoveHediff(this.parent);
            }
        }
    }
    public class HediffCompProperties_ShieldGenerator : HediffCompProperties
    {
        public HediffCompProperties_ShieldGenerator()
        {
            this.compClass = typeof(HediffComp_ShieldGenerator);
        }
        public float energyRegenOffset;
        public float resetDelayFactor = 1f;
        public float maxEnergyOffset;
        public HediffDef bionicShieldDef;
        public bool makesShield;
    }
    public class HediffComp_ShieldGenerator : HediffComp
    {
        public HediffCompProperties_ShieldGenerator Props
        {
            get
            {
                return (HediffCompProperties_ShieldGenerator)this.props;
            }
        }
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            if (this.Props.bionicShieldDef != null)
            {
                if (this.Props.makesShield && !this.Pawn.health.hediffSet.HasHediff(this.Props.bionicShieldDef))
                {
                    Hediff hediff = HediffMaker.MakeHediff(this.Props.bionicShieldDef, this.Pawn, null);
                    this.Pawn.health.AddHediff(hediff);
                }
                Hediff shield = this.Pawn.health.hediffSet.GetFirstHediffOfDef(this.Props.bionicShieldDef);
                if (shield != null)
                {
                    HediffComp_BionicShield hcbs = shield.TryGetComp<HediffComp_BionicShield>();
                    if (hcbs != null)
                    {
                        hcbs.RedetermineAllStats();
                    }
                }
            }
        }
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (this.Pawn.IsHashIntervalTick(120, delta) && this.Props.bionicShieldDef != null && this.Props.makesShield && !this.Pawn.health.hediffSet.HasHediff(this.Props.bionicShieldDef))
            {
                Hediff shield = HediffMaker.MakeHediff(this.Props.bionicShieldDef, this.Pawn, null);
                this.Pawn.health.AddHediff(shield);
            }
        }
        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            if (this.Props.bionicShieldDef != null)
            {
                Hediff shield = this.Pawn.health.hediffSet.GetFirstHediffOfDef(this.Props.bionicShieldDef);
                if (shield != null)
                {
                    HediffComp_BionicShield hcbs = shield.TryGetComp<HediffComp_BionicShield>();
                    hcbs.RedetermineAllStats();
                }
            }
        }
    }
    public class HediffCompProperties_GainRageWhenHit : HediffCompProperties
    {
        public HediffCompProperties_GainRageWhenHit()
        {
            this.compClass = typeof(HediffComp_GainRageWhenHit);
        }
        public HediffDef hediff;
        public float severityGain;
        public int setDurationTo;
    }
    public class HediffComp_GainRageWhenHit : HediffComp
    {
        public HediffCompProperties_GainRageWhenHit Props
        {
            get
            {
                return (HediffCompProperties_GainRageWhenHit)this.props;
            }
        }
        public override void Notify_PawnPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.Notify_PawnPostApplyDamage(dinfo, totalDamageDealt);
            if (totalDamageDealt > 0 && dinfo.Def != null && dinfo.Def.harmsHealth)
            {
                Hediff hediff = this.Pawn.health.hediffSet.GetFirstHediffOfDef(this.Props.hediff);
                if (hediff != null)
                {
                    hediff.Severity += this.Props.severityGain;
                    HediffComp_Disappears hcd = hediff.TryGetComp<HediffComp_Disappears>();
                    if (hcd != null)
                    {
                        hcd.SetDuration(this.Props.setDurationTo);
                    }
                } else {
                    hediff = HediffMaker.MakeHediff(this.Props.hediff,this.Pawn);
                    this.Pawn.health.AddHediff(hediff);
                }
            }
        }
    }
    public class HediffCompProperties_BreathtakerAura : HediffCompProperties_AuraHediff
    {
        public HediffCompProperties_BreathtakerAura()
        {
            this.compClass = typeof(HedifComp_BreathtakerAura);
        }
    }
    public class HedifComp_BreathtakerAura : HediffComp_AuraHediff
    {
        public override bool ShouldBeActive { 
            get {
                if (this.parent.pawn.abilities != null)
                {
                    foreach (RimWorld.Ability a in this.parent.pawn.abilities.abilities)
                    {
                        if (a.def == HVBDefOf.HVB_ToggleBreathtaker)
                        {
                            CompAbilityEffect_Toggle caet = a.CompOfType<CompAbilityEffect_Toggle>();
                            if (caet != null)
                            {
                                return caet.enabled;
                            }
                        }
                    }
                }
                return base.ShouldBeActive;
            } 
        }
    }
    public class HediffComp_AddOn : HediffComp
    {
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (this.Pawn.health.hediffSet.PartIsMissing(this.parent.Part))
            {
                this.Pawn.health.RemoveHediff(this.parent);
            }
        }
    }
    public class HediffCompProperties_Earthshaker : HediffCompProperties
    {
        public HediffCompProperties_Earthshaker()
        {
            this.compClass = typeof(HediffComp_Earthshaker);
        }
        public float shockwavePower = 1f;
        public float bonusRadiusPerSeverity;
    }
    public class HediffComp_Earthshaker : HediffComp
    {
        public HediffCompProperties_Earthshaker Props
        {
            get
            {
                return (HediffCompProperties_Earthshaker)this.props;
            }
        }
    }
    public class HediffCompProperties_DubsIntestine : HediffCompProperties_CreateThingsBySpendingSeverity
    {
        public HediffCompProperties_DubsIntestine()
        {
            this.compClass = typeof(HediffComp_DubsIntestine);
        }
        public float severityPerDay;
        public List<HediffDef> progressDisabledBy;
    }
    public class HediffComp_DubsIntestine : HediffComp_CreateThingsBySpendingSeverity
    {
        public new HediffCompProperties_DubsIntestine Props
        {
            get
            {
                return (HediffCompProperties_DubsIntestine)this.props;
            }
        }
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (this.Pawn.IsHashIntervalTick(300, delta))
            {
                foreach (HediffDef hd in this.Props.progressDisabledBy)
                {
                    if (this.Pawn.health.hediffSet.HasHediff(hd))
                    {
                        return;
                    }
                }
                this.parent.Severity += this.Props.severityPerDay / 200f;
            }
        }
    }
    public class HediffCompProperties_Firefoamer : HediffCompProperties_SeverityPerDay
    {
        public HediffCompProperties_Firefoamer()
        {
            this.compClass = typeof(HediffComp_Firefoamer);
        }
        public float radius;
        public float minSevToTrigger;
        public List<DamageDef> respondTo;
        public DamageDef damageType;
        public ThingDef postBurstSpawn;
    }
    public class HediffComp_Firefoamer : HediffComp_SeverityPerDay
    {
        public HediffCompProperties_Firefoamer Props
        {
            get
            {
                return (HediffCompProperties_Firefoamer)this.props;
            }
        }
        public override void Notify_PawnPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.Notify_PawnPostApplyDamage(dinfo, totalDamageDealt);
            if (this.Props.respondTo.Contains(dinfo.Def))
            {
                this.DetonateCheck();
            }
        }
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (this.Pawn.IsHashIntervalTick(250, delta))
            {
                Pawn p = this.Pawn;
                Room room = p.Position.GetRoom(p.Map);
                foreach (IntVec3 intVec in GenRadial.RadialCellsAround(p.Position, this.Props.radius, true))
                {
                    if (intVec.InBounds(p.Map) && !intVec.Fogged(p.Map))
                    {
                        Room room2 = intVec.GetRoom(p.Map);
                        if (room2 != null && room == room2 && FireUtility.NumFiresAt(intVec, p.Map) > 0)
                        {
                            this.DetonateCheck();
                            break;
                        }
                    }
                }
            }
        }
        public void DetonateCheck()
        {
            if (this.parent.Severity >= this.Props.minSevToTrigger && this.Pawn.SpawnedOrAnyParentSpawned)
            {
                GenExplosion.DoExplosion(this.Pawn.PositionHeld, this.Pawn.MapHeld, this.Props.radius, this.Props.damageType, this.Pawn, -1, -1f, SoundDefOf.Explosion_FirefoamPopper, null, null, null, this.Props.postBurstSpawn, 1f, 3, null, null, 255, true, null, 0f, 0, 0f, false, null, null, null, true, 1f, 0f, true, null, 1f, null, null);
                this.parent.Severity = this.parent.def.minSeverity;
            }
        }
    }
    public class HediffCompProperties_Fin : HediffCompProperties
    {
        public HediffCompProperties_Fin()
        {
            this.compClass = typeof(HediffComp_Fin);
        }
        public float finPower = 0.1f;
    }
    public class HediffComp_Fin : HediffComp
    {
        public HediffCompProperties_Fin Props
        {
            get
            {
                return (HediffCompProperties_Fin)this.props;
            }
        }
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            HVBUtility.HVB_EvaluateFins(this.Pawn);
        }
        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            HVBUtility.HVB_EvaluateFins(this.Pawn);
        }
    }
    public class ThoughtWorker_RadioSurfingEar : ThoughtWorker_Hediff
    {
        public override float MoodMultiplier(Pawn p)
        {
            return base.MoodMultiplier(p) * p.health.capacities.GetLevel(PawnCapacityDefOf.Hearing);
        }
    }
    public class Thought_GreatInhalation : Thought_Situational
    {
        protected override float BaseMoodOffset
        {
            get
            {
                float num = 0f;
                if (this.pawn.Spawned && this.pawn.Map != null)
                {
                    if (this.pawn.Position.AnyGas(this.pawn.Map, GasType.RotStink))
                    {
                        num -= 5;
                    }
                    if (this.pawn.Position.GetRoom(this.pawn.Map) != null && !this.pawn.Position.GetRoom(this.pawn.Map).PsychologicallyOutdoors)
                    {
                        num += (int)Math.Min(2f * this.pawn.Position.GetRoom(this.pawn.Map).GetStat(RoomStatDefOf.Cleanliness), 0);
                    } else {
                        num -= (Find.WorldGrid[this.pawn.Map.Tile].PrimaryBiome.diseaseMtbDays / 30f);
                    }
                    return num;
                }
                if (this.pawn.Tile != -1)
                {
                    num -= (1 + Find.WorldGrid[this.pawn.Tile].PrimaryBiome.diseaseMtbDays / 30f);
                }
                return num;
            }
        }
    }
    public class ThoughtWorker_TheNoseKnows : ThoughtWorker
    {
        protected override ThoughtState CurrentSocialStateInternal(Pawn pawn, Pawn other)
        {
            if (!RelationsUtility.PawnsKnowEachOther(pawn, other))
            {
                return false;
            }
            if (other.health.hediffSet.HasHediff(this.def.hediff) && !pawn.health.hediffSet.PartIsMissing(pawn.RaceProps.body.GetPartsWithDef(DefDatabase<BodyPartDef>.GetNamed("Nose")).RandomElement()))
            {
                return ThoughtState.ActiveDefault;
            }
            return false;
        }
    }
    public class HVBUtility
    {
        public static void HVB_DoSurgery(Pawn pawn, RecipeDef recipeDef)
        {
            if (recipeDef.Worker.GetPartsToApplyOn(pawn, recipeDef).Any<BodyPartRecord>() && (recipeDef.addsHediff == null || pawn.health.hediffSet.PainTotal + recipeDef.addsHediff.stages[recipeDef.addsHediff.StageAtSeverity(recipeDef.addsHediff.initialSeverity)].painOffset < pawn.GetStatValue(StatDefOf.PainShockThreshold)))
            {
                recipeDef.Worker.ApplyOnPawn(pawn, recipeDef.Worker.GetPartsToApplyOn(pawn, recipeDef).RandomElement<BodyPartRecord>(), null, new List<Thing>(), null);
            }
        }
        public static void HVB_LandingEffects(PawnFlyer pawnFlyer)
        {
            if (pawnFlyer.FlyingPawn != null)
            {
                Pawn pawn = pawnFlyer.FlyingPawn;
                if (pawn.abilities != null)
                {
                    foreach (RimWorld.Ability a in pawn.abilities.abilities)
                    {
                        if (a.def == HVBDefOf.HVB_ToggleEarthshaker)
                        {
                            CompAbilityEffect_Toggle caet = a.CompOfType<CompAbilityEffect_Toggle>();
                            if (caet != null && !caet.enabled)
                            {
                                return;
                            }
                        }
                    }
                }
                float earthshakerCount = 0f;
                float bonusRadius = 0f;
                foreach (Hediff h in pawn.health.hediffSet.hediffs)
                {
                    if (h is HediffWithComps hwc)
                    {
                        HediffComp_Earthshaker es = hwc.TryGetComp<HediffComp_Earthshaker>();
                        if (es != null)
                        {
                            earthshakerCount += es.Props.shockwavePower;
                            bonusRadius += es.Props.bonusRadiusPerSeverity * hwc.Severity;
                        }
                    }
                }
                if (earthshakerCount > 0f)
                {
                    DamageInfo dinfo = new DamageInfo(DamageDefOf.Crush, 35f * pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving), 0f, -1f, pawn);
                    float radius = (3f * pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving))+bonusRadius;
                    foreach (Thing thing in GenRadial.RadialDistinctThingsAround(pawnFlyer.Position, pawnFlyer.Map, radius, true))
                    {
                        if (thing is Pawn p)
                        {
                            p.stances.stunner.StunFor((int)(250 * earthshakerCount), pawn);
                        }
                        else if (thing.def.category == ThingCategory.Building || thing.def.category == ThingCategory.Plant)
                        {
                            thing.TakeDamage(dinfo);
                        }
                    }
                    GenExplosion.DoExplosion(pawnFlyer.Position, pawnFlyer.Map, radius, DamageDefOf.Smoke, null, -1, -1f, null, null, null, null, null, 0f, 1, null, null, 255, false, null, 0f, 1, 0f, false, null, null, null, true, 1f, 0f, true, null, 1f);
                }
            }
        }
        public static void HVB_EvaluateFins(Pawn pawn)
        {
        }
        public static float HVB_RecalculateFinPower(Pawn pawn)
        {
            return 0f;
        }
    }
    public class HVB_Settings : ModSettings
    {
        public float bionicsForSaleMultiplier = 2;
        public override void ExposeData()
        {
            Scribe_Values.Look(ref bionicsForSaleMultiplier, "bionicsForSaleMultiplier", 2f);
            base.ExposeData();
        }
    }
    public class HVB_Mod : Mod
    {
        public HVB_Mod(ModContentPack content) : base(content)
        {
            HVB_Mod.settings = GetSettings<HVB_Settings>();
        }
        public override void DoSettingsWindowContents(Rect inRect)
        {
            //number of bionics for sale multiplier
            float x = inRect.xMin, y = inRect.yMin + 25, halfWidth = inRect.width * 0.5f;
            displayBionicSaleMultiplier = ((int)settings.bionicsForSaleMultiplier).ToString();
            float origR = settings.bionicsForSaleMultiplier;
            Rect bionicSaleRect = new Rect(x + 10, y, halfWidth - 15, 32);
            settings.bionicsForSaleMultiplier = Widgets.HorizontalSlider(bionicSaleRect, settings.bionicsForSaleMultiplier, 1f, 4f, true, "HVB_SettingBSM".Translate(), "1x", "4x", 1f);
            TooltipHandler.TipRegion(bionicSaleRect.LeftPart(1f), "HVB_TooltipBionicSaleMulti".Translate());
            if (origR != settings.bionicsForSaleMultiplier)
            {
                displayBionicSaleMultiplier = ((int)settings.bionicsForSaleMultiplier).ToString() + "x";
            }
            y += 32;
            string origStringR = displayBionicSaleMultiplier;
            displayBionicSaleMultiplier = Widgets.TextField(new Rect(x + 10, y, 50, 32), displayBionicSaleMultiplier);
            if (!displayBionicSaleMultiplier.Equals(origStringR))
            {
                this.ParseInput(displayBionicSaleMultiplier, settings.bionicsForSaleMultiplier, out settings.bionicsForSaleMultiplier);
            }
            base.DoSettingsWindowContents(inRect);
        }
        private void ParseInput(string buffer, float origValue, out float newValue)
        {
            if (!float.TryParse(buffer, out newValue))
                newValue = origValue;
            if (newValue < 0)
                newValue = origValue;
        }
        public override string SettingsCategory()
        {
            return "Hauts' Utility Bionics";
        }
        public static HVB_Settings settings;
        public string displayBionicSaleMultiplier;
    }
}
