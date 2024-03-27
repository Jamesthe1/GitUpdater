using System;
using System.Linq;
using System.Collections.Generic;

using UnityEngine;
using Verse;
using LibGit2Sharp;

namespace GitUpdater {
    using IDActions = Dictionary<string, Action>;
    using FCStrat = CheckoutFileConflictStrategy;

    internal static class UpdaterMenus {
        private const int ITEM_HEIGHT = 30;

        public enum MenuMode {
            Main,
            AddMod
        }

        public static MenuMode menu = MenuMode.Main;
        private static Vector2 scrollPos = Vector2.zero;
        private static List<ModMetaData> cachedRepos = new List<ModMetaData> ();
        public static bool reposCached = false;
        public static bool savedReposCached = true;
        public static bool lastLocalRuleState = true;     // Value "settings.onlyLocal" is inaccessible before constructing

        public static bool needsRestart = false;

        private static void ListMods (Listing_Standard listingStd, Func<ModMetaData, bool> condition, Action<ModMetaData> buttonAction, Rect area, ref List<ModMetaData> cache, ref bool isClean) {
            area.y += listingStd.CurHeight;

            Rect inner = new Rect (area);
            inner.width -= 20;

            if (!isClean) {
                cache = new List<ModMetaData> (GitUpdateCore.GetUpdateableModsOfCondition (condition));
                isClean = true;
            }

            inner.height = cache.Count * ITEM_HEIGHT;

            Rect itemRect = new Rect (0f, area.y, inner.width, ITEM_HEIGHT);

            Widgets.BeginScrollView (area, ref scrollPos, inner);
            foreach (ModMetaData mod in cache) {
                bool isPressed = Widgets.ButtonText (itemRect, mod.Name);
                itemRect.y += ITEM_HEIGHT;
                if (isPressed) {
                    buttonAction (mod);
                    isClean = false;
                }
            }
            Widgets.EndScrollView ();

            listingStd.Gap (area.height);
        }

        private static IDActions GenerateActions<TEnum> (Action<TEnum> action, string extraPrefix = "") where TEnum : Enum {
            TEnum[] vals = (TEnum[])Enum.GetValues (typeof (TEnum));
            IEnumerable<string> names = vals.Select (v => GitUpdateCore.PrefixTranslateItem (v, extraPrefix));

            IDActions idActions = new IDActions ();
            for (int i = 0; i < vals.Length; i++) {
                TEnum val = vals[i];
                string name = names.ElementAt (i);

                idActions.Add (name, () => action (val));
            }
            return idActions;
        }

        private static List<FloatMenuOption> GenerateFloatOptions<TEnum> (Action<TEnum> action, string extraPrefix = "") where TEnum : Enum {
            var enumerable = GenerateActions (action, extraPrefix).Select (kv => new FloatMenuOption (kv.Key, kv.Value));
            return new List<FloatMenuOption> (enumerable);
        }

        private static void PresentFloatOptions<TEnum> (Action<TEnum> action, string extraPrefix = "") where TEnum : Enum {
            var options = GenerateFloatOptions (action, extraPrefix);
            Find.WindowStack.Add (new FloatMenu (options));
        }

        private static void RenderMainMenu (UpdaterMod updaterMod, Rect rect, Listing_Standard listingStd, Rect modsArea) {
            ref UpdaterMod.Settings settings = ref UpdaterMod.settings;

            listingStd.CheckboxLabeled ("GU.RequireManual".Translate (), ref settings.requireManual);
            listingStd.CheckboxLabeled ("GU.OnlyLocal".Translate (), ref settings.onlyLocal);
            listingStd.CheckboxLabeled ("GU.PruneOld".Translate (), ref settings.pruneOld);
            bool chooseConflict = listingStd.LabeledButton ("GU.FCStrat".Translate (), GitUpdateCore.PrefixTranslateItem (settings.onFileConflict, "FC"), rect.width, 0.25f, 5f);
            listingStd.Label ("GU.Diff3Implement".Translate ().Colorize (Color.red));

            if (chooseConflict)
                PresentFloatOptions<FCStrat> (fcs => UpdaterMod.settings.onFileConflict = fcs, "FC");

            listingStd.Label ("GU.Repos".Translate ());
            // Temp cache since we don't want to do much
            List<ModMetaData> savedMods = settings.repoList.ConvertAll (id => ModLister.GetModWithIdentifier (id));
            ListMods (
                listingStd,
                UpdaterMod.IsSavedRepo,
                updaterMod.RemoveModRepo,
                modsArea,
                ref savedMods,
                ref savedReposCached
            );

            bool[] listMgrRow = listingStd.ButtonTextRow (
                new TaggedString[] { "GU.Add".Translate (), GitUpdateCore.PrefixTranslateItem (settings.listHandling) },
                rect.width, 5f, ITEM_HEIGHT
            );
            
            if (listMgrRow[0])
                menu = MenuMode.AddMod;
            if (listMgrRow[1])
                PresentFloatOptions<UpdaterMod.Settings.ListMode> (lm => UpdaterMod.settings.listHandling = lm);

            if (settings.requireManual) {
                listingStd.GapLine ();

                bool doManual = listingStd.ButtonText ("GU.ManualUpdate".Translate ());
                if (doManual) {
                    GitUpdateCore.UpdateRepos (settings);
                    needsRestart = true;
                }
            }

            if (needsRestart)
                listingStd.Label ("GU.RestartRequired".Translate ());
        }

        private static void RenderAddMenu (UpdaterMod updaterMod, Rect rect, Listing_Standard listingStd, Rect modsArea) {
            // Only allow repositories not in our list
            ListMods (
                listingStd,
                UpdaterMod.IsUnsavedRepo,
                updaterMod.AddModRepo,
                modsArea,
                ref cachedRepos,
                ref reposCached
            );

            listingStd.GapLine ();

            bool goBack = listingStd.ButtonText ("GU.Back".Translate ());
            if (goBack)
                menu = MenuMode.Main;
        }

        public static void RenderSettingsWindow (Rect rect, UpdaterMod updaterMod) {
            // Cache needs to be refreshed if the option is changed
            if (lastLocalRuleState != UpdaterMod.settings.onlyLocal) {
                lastLocalRuleState = UpdaterMod.settings.onlyLocal;
                savedReposCached = false;
                reposCached = false;
            }
            
            var listingStd = new Listing_Standard ();
            var modsArea = new Rect (0f, 0f, rect.width, ITEM_HEIGHT * 10);

            listingStd.Begin (rect);

            switch (menu) {
                case MenuMode.Main:
                    RenderMainMenu (updaterMod, rect, listingStd, modsArea);
                    break;
                case MenuMode.AddMod:
                    RenderAddMenu (updaterMod, rect, listingStd, modsArea);
                    break;
            }

            listingStd.End ();
        }
    }
}