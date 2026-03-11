using HautsFramework;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace HautsBionics
{
    /*The archotech earthshaker causes a disruptive shockwave on landing after any jump, and the archotech breathtaker emits an aura that inflicts a rapidly escalating debuff on organic pawns in its radius.
     * You might not always want these passive properties on, especially out of combat.
     * Abilities with this comp do nothing other than switch their 'enabled' value whenever you press them.
     * Those archotech bionics grant an ability with this comp (via HediffCompProperties_GiveAbility, so that even if you have multiple copies of the bionic you'll still only have one toggle),
     * and their togglable properties will not work if they find that that ability's Toggle comp has enable = false.*/
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
    //in addition to respecting its corresponding Toggle as mentioned above, breathtaker auras inflict their debuff slower against vacuum resistant pawns, and they also periodically delete gas around themselves.
    public class HediffCompProperties_BreathtakerAura : HediffCompProperties_AuraHediff
    {
        public HediffCompProperties_BreathtakerAura()
        {
            this.compClass = typeof(HediffComp_BreathtakerAura);
        }
    }
    public class HediffComp_BreathtakerAura : HediffComp_AuraHediff
    {
        public override bool ShouldBeActive
        {
            get
            {
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
        public override float HediffSeverity(Pawn p, HediffDef h)
        {
            float result = base.HediffSeverity(p, h);
            return ModsConfig.OdysseyActive ? result * Mathf.Max(1f - p.GetStatValue(StatDefOf.VacuumResistance, true, -1), 0f) : result;
        }
        protected override void AffectPawns(Pawn p, List<Pawn> pawns, bool inCaravan = false)
        {
            base.AffectPawns(p, pawns, inCaravan);
            if (this.Pawn.Spawned && this.Pawn.Map.gasGrid != null)
            {
                int num = GenRadial.NumCellsInRadius(this.FunctionalRange / 2f);
                for (int i = 0; i < num; i++)
                {
                    this.Pawn.Map.gasGrid.SetDirect(this.Pawn.Position + GenRadial.RadialPattern[i], 0, 0, 0, 0);
                }
            }
        }
    }
    /*Earthshaker shockwaves are handled via a Harmony patch. Since a pawn can have multiple earthshakers (and since, with AEP or Anomaly, there are multiple variants of Earthshakers with different power levels),
     * the patch has to sum the shockwave-relevant properties of all a pawn's Earthshakers. Radius = [shockwavePower + (current severity * bonusRadiusPerSeverity)]. Shockwave stun duration just = shockwavePower.
     * bonusRadiusPerSeverity is specifically for the Anomaly voidshard variant; as the Anomalous activity level increases, its severity increases, so its radius increases.*/
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
}
