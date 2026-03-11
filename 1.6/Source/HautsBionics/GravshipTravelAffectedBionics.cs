using HautsFramework;
using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace HautsBionics
{
    //could've also been a DME, but it not being one slightly improves the performance of effects that iterate thru a hediff def's extensions. See the Harmony patch HVBLandingEndedPrefix for what these two classes do
    public class Hediff_ImplantGravNausea : Hediff_Implant
    {

    }
    public class GravitonPart : DefModExtension
    {
        public GravitonPart()
        {
        }
        public HediffDef hediff;
    }
    /*at max severity (which happens as a gravship passenger when it lands, due to GravitonPart), lowers all the pawn's ability cooldowns by lowerAllCooldownsBy.
     * Its Anomaly voidshard equivalent provides a stronger cooldown offset as anomalous activity level rises, which you can see with the other three fields*/
    public class HediffCompProperties_AbilityRefresher : HediffCompProperties
    {
        public HediffCompProperties_AbilityRefresher()
        {
            this.compClass = typeof(HediffComp_AbilityRefresher);
        }
        public int lowerAllCooldownsBy;
        public bool multiplyByAnomalyActivityLevel;
        public float levelForAmbientHorror = 2;
        public Dictionary<int, float> levelAtEachLevel;
    }
    public class HediffComp_AbilityRefresher : HediffComp
    {
        public HediffCompProperties_AbilityRefresher Props
        {
            get
            {
                return (HediffCompProperties_AbilityRefresher)this.props;
            }
        }
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (this.parent.Severity == this.parent.def.maxSeverity)
            {
                int lowerCDsBy = this.Props.lowerAllCooldownsBy;
                if (ModsConfig.AnomalyActive && this.Props.multiplyByAnomalyActivityLevel)
                {
                    if (Find.Storyteller.difficulty.AnomalyPlaystyleDef == DefDatabase<AnomalyPlaystyleDef>.GetNamedSilentFail("AmbientHorror"))
                    {
                        lowerCDsBy = (int)(lowerCDsBy * this.Props.levelForAmbientHorror);
                    }
                    else if (this.Props.levelAtEachLevel != null)
                    {
                        lowerCDsBy = (int)(lowerCDsBy * Math.Max(1, this.Props.levelAtEachLevel.TryGetValue(Find.Anomaly.Level, 1)));
                    }
                }
                if (this.Pawn.abilities != null)
                {
                    List<AbilityGroupDef> agd = new List<AbilityGroupDef>();
                    foreach (RimWorld.Ability a in this.Pawn.abilities.AllAbilitiesForReading)
                    {
                        if (a.OnCooldown)
                        {
                            if (a.def.groupDef != null)
                            {
                                if (!agd.Contains(a.def.groupDef))
                                {
                                    agd.Add(a.def.groupDef);
                                    AbilityCooldownModifierUtility.SetNewCooldown(a, a.CooldownTicksRemaining - lowerCDsBy);
                                }
                            }
                            else
                            {
                                AbilityCooldownModifierUtility.SetNewCooldown(a, a.CooldownTicksRemaining - lowerCDsBy);
                            }
                        }
                    }
                }
                VEF.Abilities.CompAbilities comp = this.Pawn.GetComp<VEF.Abilities.CompAbilities>();
                if (comp != null && !comp.LearnedAbilities.NullOrEmpty())
                {
                    foreach (VEF.Abilities.Ability ab in comp.LearnedAbilities)
                    {
                        if (ab.cooldown > Find.TickManager.TicksGame)
                        {
                            ab.cooldown -= lowerCDsBy;
                        }
                    }
                }
                this.parent.Severity = this.parent.def.minSeverity;
            }
        }
    }
}
