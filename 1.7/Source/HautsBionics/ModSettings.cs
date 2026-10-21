using UnityEngine;
using Verse;

namespace HautsBionics
{
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
