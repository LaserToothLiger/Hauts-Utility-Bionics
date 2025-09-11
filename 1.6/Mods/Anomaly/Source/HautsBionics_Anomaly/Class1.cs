using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using HarmonyLib;
using UnityEngine;
using HautsFramework;
using RimWorld.Planet;
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
            harmony.Patch(AccessTools.Method(typeof(HautsUtility), nameof(HautsUtility.AddTraitGrantedStuff)),
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
    public class Recipe_InstallVoidImplant : Recipe_Surgery
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            List<Hediff> allHediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < allHediffs.Count; i++)
            {
                if (allHediffs[i].Part != null && allHediffs[i].def == recipe.removesHediff && allHediffs[i].Visible)
                {
                    yield return allHediffs[i].Part;
                }
            }
            yield break;
        }
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            /*if (pawn.story != null)
            {
                foreach (Trait t in pawn.story.traits.allTraits)
                {
                    if (t.def.HasModExtension<CannotRemoveBionicsFrom>())
                    {
                        return;
                    }
                }
            }*/
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
                    pawn.health.RemoveHediff(hediff);
                    pawn.health.AddHediff(this.recipe.addsHediff, part, null, null);
                }
            }
            if (this.IsViolationOnPawn(pawn, part, Faction.OfPlayer))
            {
                base.ReportViolation(pawn, billDoer, pawn.HomeFaction, -70, null);
            }
        }
    }
    public class Recipe_InstallVoidArtificialBodyPart : Recipe_Surgery
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            List<Hediff> allHediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < allHediffs.Count; i++)
            {
                if (allHediffs[i].Part != null && allHediffs[i].def == recipe.removesHediff && allHediffs[i].Visible)
                {
                    yield return allHediffs[i].Part;
                }
            }
            yield break;
        }
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            /*if (pawn.story != null)
            {
                foreach (Trait t in pawn.story.traits.allTraits)
                {
                    if (t.def.HasModExtension<CannotRemoveBionicsFrom>())
                    {
                        return;
                    }
                }
            }*/
            Hediff hediff = null;
            if (billDoer != null)
            {
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, new object[]
                {
                    billDoer,
                    pawn
                });
                hediff = pawn.health.hediffSet.GetDirectlyAddedPartFor(part);
                if (part != null)
                {
                    for (int i =0; i<part.parts.Count;i++)
                    {
                        MedicalRecipesUtility.RestorePartAndSpawnAllPreviousParts(pawn, part.parts[i], billDoer.Position, billDoer.Map);
                    }
                    pawn.health.RestorePart(part,null,true);
                }
                if (!PawnGenerator.IsBeingGenerated(pawn) && this.IsViolationOnPawn(pawn, part, Faction.OfPlayer))
                {
                    base.ReportViolation(pawn, billDoer, pawn.HomeFaction, -70, null);
                }
            } else if (part != null) {
                for (int i = 0; i < part.parts.Count; i++)
                {
                    MedicalRecipesUtility.RestorePartAndSpawnAllPreviousParts(pawn, part.parts[i], pawn.Position, pawn.Map);
                }
                pawn.health.RestorePart(part, null, true);
            }
            pawn.health.AddHediff(this.recipe.addsHediff, part, null, null);
            if (hediff != null)
            {
                hediff.Notify_SurgicallyReplaced(billDoer);
            }
        }
    }
    public class Recipe_VivisectHomunculus : Recipe_Surgery
    {
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            Pawn pawn = thing as Pawn;
            return pawn != null && pawn.story != null && pawn.story.traits.HasTrait(HVBAnomalyDefOf.HVB_HomunculusTrait);
        }
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (pawn.story != null && pawn.story.traits.HasTrait(HVBAnomalyDefOf.HVB_HomunculusTrait))
            {
                pawn.story.traits.RemoveTrait(pawn.story.traits.GetTrait(HVBAnomalyDefOf.HVB_HomunculusTrait));
                List<Hediff> hediffsToRemove = new List<Hediff>();
                foreach (Hediff h in pawn.health.hediffSet.hediffs)
                {
                    if (h.def.comps != null && h.def.HasComp(typeof(HediffComp_Voidshard)))
                    {
                        hediffsToRemove.Add(h);
                    }
                }
                List<Hediff> bionicsToExtract = new List<Hediff>();
                int toExtract = Math.Min((int)Rand.RangeInclusive(3,4),hediffsToRemove.Count);
                int counter = 250;
                while (bionicsToExtract.Count < toExtract && counter > 0)
                {
                    Hediff h = hediffsToRemove.RandomElement();
                    if (!bionicsToExtract.Contains(h))
                    {
                        bionicsToExtract.Add(h);
                    }
                    counter--;
                }
                if (pawn.SpawnedOrAnyParentSpawned)
                {
                    foreach (Hediff h in bionicsToExtract)
                    {
                        if (h.def.spawnThingOnRemoved != null)
                        {
                            GenSpawn.Spawn(h.def.spawnThingOnRemoved, pawn.PositionHeld, pawn.MapHeld, WipeMode.Vanish);
                        }
                    }
                    for (int i = pawn.health.hediffSet.hediffs.Count - 1; i >= 0; i--)
                    {
                        if (pawn.health.hediffSet.hediffs[i].def.comps != null && pawn.health.hediffSet.hediffs[i].def.HasComp(typeof(HediffComp_Voidshard)))
                        {
                            pawn.health.RemoveHediff(pawn.health.hediffSet.hediffs[i]);
                            EffecterDefOf.MeatExplosion.Spawn(pawn.PositionHeld, pawn.MapHeld, 1f).Cleanup();
                        }
                    }
                    pawn.Kill(null);
                    if (pawn.Corpse != null)
                    {
                        pawn.Corpse.Destroy(DestroyMode.KillFinalize);
                    } else if (!pawn.Dead) {
                        pawn.Destroy(DestroyMode.KillFinalize);
                    }
                }
            }
        }
    }
    public class IncidentWorker_HomunculusExamined : IncidentWorker
    {
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            return true;
        }
    }
    public class HediffCompProperties_OnlyGhoulsAura : HediffCompProperties_AuraHediff
    {
        public HediffCompProperties_OnlyGhoulsAura()
        {
            this.compClass = typeof(HediffComp_OnlyGhoulsAura);
        }
    }
    public class HediffComp_OnlyGhoulsAura : HediffComp_AuraHediff
    {
        public override bool ValidatePawn(Pawn originator, Pawn p, bool inCaravan)
        {
            if (p.IsGhoul)
            {
                return base.ValidatePawn(originator, p, inCaravan);
            }
            return false;
        }
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (this.Pawn.IsGhoul)
            {
                if (this.parent.def.spawnThingOnRemoved != null)
                {
                    GenSpawn.Spawn(this.parent.def.spawnThingOnRemoved, this.Pawn.Position, this.Pawn.Map, WipeMode.Vanish);
                }
                this.Pawn.health.RemoveHediff(this.parent);
            }
        }
    }
    public class HediffCompProperties_Truelife : HediffCompProperties
    {
        public HediffCompProperties_Truelife()
        {
            this.compClass = typeof(HediffComp_Truelife);
        }
        public List<MutantDef> revertedMutantDefs;
        public int periodicity;
        public string reversionText;
    }
    public class HediffComp_Truelife : HediffComp
    {
        public HediffCompProperties_Truelife Props
        {
            get
            {
                return (HediffCompProperties_Truelife)this.props;
            }
        }
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            this.factionAtTimeOfDeath = this.Pawn.Faction;
        }
        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
            this.factionAtTimeOfDeath = this.Pawn.Faction;
        }
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (this.parent.Severity == this.parent.def.maxSeverity && this.Pawn.IsHashIntervalTick(this.Props.periodicity, delta) && this.Pawn.mutant != null && this.Pawn.mutant.Def != null && !this.Pawn.health.hediffSet.HasHediff(HediffDefOf.Rising) && (this.Props.revertedMutantDefs == null || this.Props.revertedMutantDefs.Contains(this.Pawn.mutant.Def)))
            {
                this.Pawn.mutant.Revert();
                this.Pawn.health.Notify_Resurrected();
                Hediff hediff = HediffMaker.MakeHediff(HediffDefOf.ResurrectionSickness, this.Pawn, null);
                if (!this.Pawn.health.WouldDieAfterAddingHediff(hediff))
                {
                    this.Pawn.health.AddHediff(hediff, null, null, null);
                }
                if (this.factionAtTimeOfDeath != null)
                {
                    this.Pawn.SetFaction(this.factionAtTimeOfDeath);
                }
                this.parent.Severity = this.parent.def.minSeverity;
                if (PawnUtility.ShouldSendNotificationAbout(this.Pawn))
                {
                    Messages.Message(this.Props.reversionText.Translate().CapitalizeFirst().Formatted(this.Pawn.Named("PAWN")).AdjustedFor(this.Pawn, "PAWN", true).Resolve(), this.Pawn, MessageTypeDefOf.NeutralEvent, true);
                }
            }
        }
        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_References.Look<Faction>(ref this.factionAtTimeOfDeath, "factionAtTimeOfDeath", false);
        }
        public Faction factionAtTimeOfDeath;
    }
    public class Hediff_Hellborne : Hediff_Implant
    {
        public override void PostTickInterval(int delta)
        {
            base.PostTickInterval(delta);
            if (this.pawn.mutant != null && this.pawn.mutant.Def != null && this.pawn.mutant.Def == MutantDefOf.Shambler)
            {
                this.Severity = 1f;
            } else {
                this.Severity = 0.01f;
            }
        }
    }
    public class CompProperties_AbilityAbsorbSentience : CompProperties_AbilityEffectWithDuration
    {
        public HediffDef hediffToSelf;
        public float baseSeverityToAdd;
        public float bonusSeverityIfIntermediate;
        public float bonusSeverityIfAdvanced;
        public float bonusSeverityIfHadSentienceCatalyst;
    }
    public class CompAbilityEffect_AbsorbSentience : CompAbilityEffect_WithDuration
    {
        public new CompProperties_AbilityAbsorbSentience Props
        {
            get
            {
                return (CompProperties_AbilityAbsorbSentience)this.props;
            }
        }
        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return base.CanApplyOn(target, dest) && target.Pawn != null && (target.Pawn.Downed || target.Pawn.Faction == this.parent.pawn.Faction);
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            float severityToGain = this.Props.baseSeverityToAdd;
            Pawn occupant = target.Pawn;
            if (occupant != null)
            {
                TrainabilityDef td = TrainableUtility.GetTrainability(occupant);
                if (td != null)
                {
                    if (td == TrainabilityDefOf.Advanced)
                    {
                        severityToGain += this.Props.bonusSeverityIfAdvanced;
                    } else if (td == TrainabilityDefOf.Intermediate) {
                        severityToGain += this.Props.bonusSeverityIfIntermediate;
                    }
                    if (ModsConfig.OdysseyActive && occupant.health.hediffSet.HasHediff(HediffDefOf.SentienceCatalyst))
                    {
                        severityToGain += this.Props.bonusSeverityIfHadSentienceCatalyst;
                    }
                }
                DamageInfo damageInfo = new DamageInfo(DamageDefOf.ExecutionCut, 9999f, 999f, -1f, null, occupant.health.hediffSet.GetBrain(), null, DamageInfo.SourceCategory.ThingOrUnknown, null, true, true, QualityCategory.Normal, true, false);
                damageInfo.SetIgnoreInstantKillProtection(true);
                damageInfo.SetAllowDamagePropagation(false);
                occupant.forceNoDeathNotification = true;
                occupant.TakeDamage(damageInfo);
                occupant.forceNoDeathNotification = false;
            }
            if (occupant.Dead)
            {
                Hediff alreadyExisting = this.parent.pawn.health.hediffSet.GetFirstHediffOfDef(this.Props.hediffToSelf);
                if (alreadyExisting != null)
                {
                    alreadyExisting.Severity += severityToGain;
                } else {
                    BodyPartRecord bpr = this.parent.pawn.health.hediffSet.GetBrain();
                    Hediff hediff = HediffMaker.MakeHediff(this.Props.hediffToSelf, this.parent.pawn, bpr);
                    this.parent.pawn.health.AddHediff(hediff, bpr);
                    hediff.Severity = severityToGain;
                }
            }
        }
    }
    public class HediffCompProperties_Voidshard : HediffCompProperties
    {
        public HediffCompProperties_Voidshard()
        {
            this.compClass = typeof(HediffComp_Voidshard);
        }
        [MustTranslate]
        public string letterLabel;
        [MustTranslate]
        public string letterText;
        public List<HediffDef> downsides;
        public float chanceForNothing = 0.5f;
        public IntRange timeToRerollDownside = new IntRange(720000, 1680000);
    }
    public class HediffComp_Voidshard : HediffComp
    {
        public HediffCompProperties_Voidshard Props
        {
            get
            {
                return (HediffCompProperties_Voidshard)this.props;
            }
        }
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            if (Find.Anomaly.Level > 0 && Find.Anomaly.Level < 6)
            {
                this.GenerateDownside();
            }
            this.timeToNextDownsideReroll = this.Props.timeToRerollDownside.RandomInRange;
        }
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (Find.Anomaly.Level > 0 && Find.Anomaly.Level < 6)
            {
                if (this.timeToNextDownsideReroll > 0)
                {
                    this.timeToNextDownsideReroll -= delta;
                } else {
                    this.RerollDownside();
                }
            } else {
                this.timeToNextDownsideReroll = 0;
            }
        }
        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            this.RemoveCurrentDownside();
            this.HorrorAttack();
        }
        private void HorrorAttack()
        {
            if (this.parent.pawn.SpawnedOrAnyParentSpawned)
            {
                Pawn pawn = this.parent.pawn;
                Pawn pawn2 = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Metalhorror, Faction.OfEntities, PawnGenerationContext.NonPlayer, -1, false, false, false, true, false, 1f, false, true, false, true, true, false, false, false, false, 0f, 0f, null, 1f, null, null, null, null, null, new float?(0f), new float?(0f), null, null, null, null, null, false, false, false, false, null, null, null, null, null, 0f, DevelopmentalStage.Adult, null, null, null, false, false, false, -1, 0, false));
                GenSpawn.Spawn(pawn2, CellFinder.StandableCellNear(pawn.PositionHeld, pawn.MapHeld, 2f, null), pawn.MapHeld, WipeMode.Vanish);
                pawn2.stances.stunner.StunFor(new IntRange(120, 240).RandomInRange, null, true, true, false);
                CompInspectStringEmergence compInspectStringEmergence = pawn2.TryGetComp<CompInspectStringEmergence>();
                if (compInspectStringEmergence != null)
                {
                    compInspectStringEmergence.sourcePawn = pawn;
                }
                TaggedString label = this.Props.letterLabel.Formatted(pawn.Named("PAWN"));
                TaggedString text = this.Props.letterText.Formatted(pawn.Named("PAWN"));
                Find.LetterStack.ReceiveLetter(label, text, LetterDefOf.ThreatBig, pawn2, null, null, null, null, 0, true);
            }
        }
        public void RerollDownside()
        {
            this.RemoveCurrentDownside();
            this.GenerateDownside();
            this.timeToNextDownsideReroll = this.Props.timeToRerollDownside.RandomInRange;
        }
        public void RemoveCurrentDownside()
        {
            List<Hediff> hediffs = this.Pawn.health.hediffSet.hediffs;
            for (int i = hediffs.Count - 1; i >= 0; i--)
            {
                if (hediffs[i] is HediffWithComps hwc)
                {
                    HediffComp_VoidshardDownside vsd = hwc.TryGetComp<HediffComp_VoidshardDownside>();
                    if (vsd != null && vsd.causativeHediffs.Contains(this.parent))
                    {
                        vsd.causativeHediffs.Remove(this.parent);
                        if (vsd.causativeHediffs.Count == 0)
                        {
                            this.Pawn.health.RemoveHediff(hediffs[i]);
                        }
                    }
                }
            }
        }
        public HediffDef PickDownside()
        {
            return (Rand.Chance(this.Props.chanceForNothing) ? HVBAnomalyDefOf.HVB_DownsideNothing : this.Props.downsides.RandomElement());
        }
        public void GenerateDownside()
        {
            HediffDef hd = this.PickDownside();
            Hediff hvsd = this.Pawn.health.hediffSet.GetFirstHediffOfDef(hd);
            if (hvsd == null)
            {
                Hediff hediff = HediffMaker.MakeHediff(hd, this.Pawn);
                if (hediff is HediffWithComps hwc)
                {
                    HediffComp_VoidshardDownside vsd = hwc.TryGetComp<HediffComp_VoidshardDownside>();
                    if (vsd != null)
                    {
                        if (vsd.causativeHediffs == null)
                        {
                            vsd.causativeHediffs = new List<Hediff>();
                        }
                        vsd.causativeHediffs.Add(this.parent);
                    }
                }
                this.Pawn.health.AddHediff(hediff, null);
            } else {
                if (hvsd is HediffWithComps hwc)
                {
                    HediffComp_VoidshardDownside vsd = hwc.TryGetComp<HediffComp_VoidshardDownside>();
                    if (vsd != null)
                    {
                        if (vsd.causativeHediffs == null)
                        {
                            vsd.causativeHediffs = new List<Hediff>();
                        }
                        vsd.causativeHediffs.Add(this.parent);
                    }
                }
            }
        }
        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look<int>(ref this.timeToNextDownsideReroll, "timeToNextDownsideReroll", this.Props.timeToRerollDownside.RandomInRange, false);
        }
        public int timeToNextDownsideReroll;
    }
    public class HediffCompProperties_InhumanRambler : HediffCompProperties
    {
        public HediffCompProperties_InhumanRambler()
        {
            this.compClass = typeof(HediffComp_InhumanRambler);
        }
        public float chancePerSeverity;
        public bool noChanceIfVoidDisrupted = true;
    }
    public class HediffComp_InhumanRambler : HediffComp
    {
        public HediffCompProperties_InhumanRambler Props
        {
            get
            {
                return (HediffCompProperties_InhumanRambler)this.props;
            }
        }
    }
    public class HediffComp_VoidshardDownside : HediffComp
    {
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (this.causativeHediffs == null || this.causativeHediffs.Count == 0)
            {
                this.Pawn.health.RemoveHediff(this.parent);
            } else {
                this.parent.Severity = this.causativeHediffs.Count - 1;
                if (this.Pawn.IsHashIntervalTick(250, delta) && ((Find.Anomaly.Level <= 0 || Find.Anomaly.Level > 5) || (this.Pawn.Faction != null && (this.Pawn.Faction == Faction.OfEntities || this.Pawn.Faction == Faction.OfHoraxCult))))
                {
                    this.Pawn.health.RemoveHediff(this.parent);
                }
            }
        }
        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Collections.Look<Hediff>(ref this.causativeHediffs, "causativeHediffs", LookMode.Deep, Array.Empty<object>());
        }
        public List<Hediff> causativeHediffs;
    }
    public class HediffCompProperties_Corruptor : HediffCompProperties
    {
        public HediffCompProperties_Corruptor()
        {
            this.compClass = typeof(HediffComp_Corruptor);
        }
        public float corruptionMtbDays = 50f;
        public float mtbLossPerExtraSeverity;
        public HediffDef hediff;
    }
    public class HediffComp_Corruptor : HediffComp
    {
        public HediffCompProperties_Corruptor Props
        {
            get
            {
                return (HediffCompProperties_Corruptor)this.props;
            }
        }
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (this.Pawn.IsHashIntervalTick(150, delta) && (this.Pawn.story == null || !this.Pawn.story.traits.HasTrait(HVBAnomalyDefOf.HVB_HomunculusTrait)) && this.Pawn.health.immunity.DiseaseContractChanceFactor(this.Props.hediff, this.parent.Part) > 0.001f && Rand.MTBEventOccurs(Math.Max(this.Props.corruptionMtbDays - (this.Props.mtbLossPerExtraSeverity * this.parent.Severity), 0.001f), 60000f, 150f) && !this.Pawn.health.hediffSet.HasHediff(this.Props.hediff))
            {
                this.Pawn.health.AddHediff(this.Props.hediff, this.parent.Part, null, null);
            }
        }
    }
    public class HediffCompProperties_Incubator : HediffCompProperties
    {
        public HediffCompProperties_Incubator()
        {
            this.compClass = typeof(HediffComp_Incubator);
        }
        [MustTranslate]
        public string letterLabel;
        [MustTranslate]
        public string letterText;
        public Dictionary<PawnKindDef,int> possiblePawnKinds;
    }
    public class HediffComp_Incubator : HediffComp
    {
        public HediffCompProperties_Incubator Props
        {
            get
            {
                return (HediffCompProperties_Incubator)this.props;
            }
        }
        public override void Notify_PawnKilled()
        {
            base.Notify_PawnKilled();
            if (this.Pawn.story == null || !this.Pawn.story.traits.HasTrait(HVBAnomalyDefOf.HVB_HomunculusTrait))
            {
                List<PawnKindDef> couldSpawn = new List<PawnKindDef>();
                foreach (PawnKindDef pkd in this.Props.possiblePawnKinds.Keys)
                {
                    if (this.Props.possiblePawnKinds.TryGetValue(pkd) <= this.parent.Severity + 1f)
                    {
                        couldSpawn.Add(pkd);
                    }
                }
                if (couldSpawn.Count > 0)
                {
                    PawnKindDef toSpawn = couldSpawn.RandomElement();
                    Pawn pawn = this.parent.pawn;
                    Pawn pawn2 = PawnGenerator.GeneratePawn(new PawnGenerationRequest(toSpawn, Faction.OfEntities, PawnGenerationContext.NonPlayer, -1, false, false, false, true, false, 1f, false, true, false, true, true, false, false, false, false, 0f, 0f, null, 1f, null, null, null, null, null, new float?(0f), new float?(0f), null, null, null, null, null, false, false, false, false, null, null, null, null, null, 0f, DevelopmentalStage.Adult, null, null, null, false, false, false, -1, 0, false));
                    GenSpawn.Spawn(pawn2, CellFinder.StandableCellNear(pawn.Position, pawn.Map, 2f, null), pawn.Map, WipeMode.Vanish);
                    pawn2.stances.stunner.StunFor(new IntRange(120, 240).RandomInRange, null, true, true, false);
                    CompInspectStringEmergence compInspectStringEmergence = pawn2.TryGetComp<CompInspectStringEmergence>();
                    if (compInspectStringEmergence != null)
                    {
                        compInspectStringEmergence.sourcePawn = pawn;
                    }
                    TaggedString label = this.Props.letterLabel.Formatted(pawn.Named("PAWN"));
                    TaggedString text = this.Props.letterText.Formatted(pawn.Named("PAWN"));
                    Find.LetterStack.ReceiveLetter(label, text, LetterDefOf.ThreatBig, pawn2, null, null, null, null, 0, true);
                }
            }
        }
    }
    public class Thought_Subversion : Thought_Memory
    {
        protected override float BaseMoodOffset
        {
            get
            {
                return this.CurStage.baseMoodEffect* Math.Min((Find.Anomaly.Level > 5f ? 1f: Find.Anomaly.Level), 3f);
            }
        }
    }
    public class Thought_DarkHarmony : Thought_Memory
    {
        protected override float BaseMoodOffset
        {
            get
            {
                float num = Find.Anomaly.Level;
                if (num > 3f)
                {
                    if (num > 5f)
                    {
                        num = 2f;
                    } else {
                        num -= 1f;
                    }
                }
                return this.CurStage.baseMoodEffect * num;
            }
        }
    }
}
