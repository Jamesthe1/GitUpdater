using System.Linq;

using Verse;
using UnityEngine;

namespace GitUpdater {
    public static class VerseExtensions {
        private static bool MakeSectionedButton (TaggedString text, float rowWidth, float padding, float height, float top, int sections, int section) {
            float rowPart = rowWidth / sections;

            return Widgets.ButtonText (new Rect (rowPart * section + padding, top, rowPart - padding, height), text);
        }

        public static bool[] ButtonTextRow (this Listing_Standard listingStd, TaggedString[] texts, float rowWidth, float padding, float height) {
            float top = listingStd.CurHeight;
            listingStd.Gap (height);
            return texts.Select ( (s, i) => MakeSectionedButton (s, rowWidth, padding, height, top, texts.Length, i)
                                ).ToArray ();
        }

        public static bool LabeledButton (this Listing_Standard listingStd, TaggedString label, TaggedString button, float rowWidth, float buttonWidthPct, float padding) {
            float top = listingStd.CurHeight;
            listingStd.Label (label);
            float height = listingStd.CurHeight - top;
            int sections = (int)(1f / buttonWidthPct);
            // Cheap way to make a button on the right side
            return MakeSectionedButton (button, rowWidth, padding, height, top, sections, sections-1);
        }
    }
}
