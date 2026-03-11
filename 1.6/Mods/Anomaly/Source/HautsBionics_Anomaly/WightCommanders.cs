using HautsFramework;
using Verse;

namespace HautsBionics_Anomaly
{
    //a derivative of HediffComp_AuraHediff that only affects ghouls. If this hediff is on a ghoul, it will uninstall itself.
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
}
