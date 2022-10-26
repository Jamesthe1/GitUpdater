using System;
using System.Linq;
using System.Collections.Generic;

using UnityEngine;
using Verse;
using LibGit2Sharp;

namespace GitUpdater {
    using IDActions = Dictionary<string, Action>;

    public class UpdaterMod : Mod {
        public class Settings : ModSettings {
            public enum ListMode {
                Whitelist,
                Blacklist
            }

            public bool requireManual = false;
            public bool pruneOld = true;
            public CheckoutFileConflictStrategy onFileConflict = CheckoutFileConflictStrategy.Normal;
            public List<string> repoList = new List<string> () { GitUpdateCore.GetID () };
            public ListMode listHandling = ListMode.Blacklist;

            public override void ExposeData () {
                Scribe_Values.Look (ref requireManual, nameof (requireManual));
                Scribe_Values.Look (ref pruneOld, nameof (pruneOld));
                Scribe_Values.Look (ref onFileConflict, nameof (onFileConflict));
                Scribe_Collections.Look (ref repoList, nameof (repoList));
                Scribe_Values.Look (ref listHandling, nameof (listHandling));
                base.ExposeData ();
            }
        }

        private const int ITEM_HEIGHT = 30;

        public static Settings settings;
        public static bool needsRestart = false;

        private Vector2 scrollPos = Vector2.zero;
        private List<ModMetaData> cachedRepos = new List<ModMetaData> ();
        private bool reposCached = false;
        private bool savedReposCached = true;

        public enum MenuMode {
            Main,
            AddMod
        }

        public MenuMode menu = MenuMode.Main;

        public static bool IsSavedRepo (ModMetaData mod) {
            return settings.repoList.Contains (mod.PackageId);
        }

        public static bool IsUnsavedRepo (ModMetaData mod) {
            return Repository.IsValid (mod.RootDir.FullName) && !IsSavedRepo (mod);
        }

        public UpdaterMod (ModContentPack content) : base(content) {
            settings = GetSettings<Settings> ();
            string isManualStr = settings.requireManual ? "not" : "automatically";
            GitUpdateCore.LogMsg ($"Settings loaded. Will {isManualStr} update repositories right now.", GitUpdateCore.LogMode.Event);

            if (!settings.requireManual)
                GitUpdateCore.UpdateRepos (settings.listHandling);
        }

        private void ListMods (Listing_Standard listingStd, Func<ModMetaData, bool> condition, Action<ModMetaData> buttonAction, Rect area, ref List<ModMetaData> cache, ref bool isClean) {
            area.y += listingStd.CurHeight;

            Rect inner = new Rect (area);
            inner.width -= 20;

            if (!isClean) {
                cache = new List<ModMetaData> (GitUpdateCore.GetModsOfCondition (condition));
                isClean = true;
            }

            inner.height = cache.Count () * ITEM_HEIGHT;

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

        public static IDActions GenerateActions<TEnum> (Action<TEnum> action, string extraPrefix = "") where TEnum : Enum {
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

        private List<FloatMenuOption> GenerateFloatOptions<TEnum> (Action<TEnum> action, string extraPrefix = "") where TEnum : Enum {
            var enumerable = GenerateActions (action, extraPrefix).Select (kv => new FloatMenuOption (kv.Key, kv.Value));
            return new List<FloatMenuOption> (enumerable);
        }

        private void PresentFloatOptions<TEnum> (Action<TEnum> action, string extraPrefix = "") where TEnum : Enum {
            var options = GenerateFloatOptions (action, extraPrefix);
            Find.WindowStack.Add (new FloatMenu (options));
        }
        public override void DoSettingsWindowContents (Rect rect) {
            Rect TopHalf = rect.TopHalf ();
            TopHalf.LeftHalf ();
            TopHalf.RightHalf ();
            Rect BottomHalf = rect.BottomHalf ();
            BottomHalf.LeftHalf ();
            BottomHalf.RightHalf ();
            var listingStd = new Listing_Standard ();
            var modsArea = new Rect (0f, 0f, rect.width, ITEM_HEIGHT * 10);

            #region Menus

            listingStd.Begin (rect);

            switch (menu) {
                case MenuMode.Main:
                    listingStd.CheckboxLabeled ("GU.RequireManual".Translate (), ref settings.requireManual);
                    listingStd.CheckboxLabeled ("GU.PruneOld".Translate (), ref settings.pruneOld);
                    bool chooseConflict = listingStd.LabeledButton ("GU.FCStrat".Translate (), GitUpdateCore.PrefixTranslateItem (settings.onFileConflict, "FC"), rect.width, 0.25f, 5f);
                    listingStd.Label ("GU.Diff3Implement".Translate ().Colorize (Color.red));

                    if (chooseConflict)
                        PresentFloatOptions<CheckoutFileConflictStrategy> (cfs => settings.onFileConflict = cfs, "FC");

                    listingStd.Label ("GU.Repos".Translate ());
                    // Temp cache since we don't want to do much
                    List<ModMetaData> savedMods = settings.repoList.ConvertAll (id => ModLister.GetModWithIdentifier (id));
                    ListMods ( listingStd,
                               m => IsSavedRepo (m),
                               m => { settings.repoList.Remove (m.PackageId);
                                      reposCached = false; // Bugfix for list not updating on removal of an item
                                    },
                               modsArea,
                               ref savedMods,
                               ref savedReposCached
                             );

                    bool[] listMgrRow = listingStd.ButtonTextRow ( new TaggedString[] { "GU.Add".Translate (), GitUpdateCore.PrefixTranslateItem (settings.listHandling) },
                                                                   rect.width, 5f, ITEM_HEIGHT
                                                                 );
                    if (listMgrRow[0])
                        menu = MenuMode.AddMod;
                    if (listMgrRow[1])
                        PresentFloatOptions<Settings.ListMode> (lm => settings.listHandling = lm);

                    if (settings.requireManual) {
                        listingStd.GapLine ();

                        bool doManual = listingStd.ButtonText ("GU.ManualUpdate".Translate ());
                        if (doManual) {
                            GitUpdateCore.UpdateRepos (settings.listHandling);
                            needsRestart = true;
                        }
                    }

                    if (needsRestart)
                        listingStd.Label ("GU.RestartRequired".Translate ());

                    break;
                case MenuMode.AddMod:
                    // Only allow repositories not in our list
                    ListMods ( listingStd,
                               m => IsUnsavedRepo (m),
                               m => settings.repoList.Add (m.PackageId),
                               modsArea,
                               ref cachedRepos,
                               ref reposCached
                             );

                    listingStd.GapLine ();

                    bool goBack = listingStd.ButtonText ("GU.Back".Translate ());
                    if (goBack)
                        menu = MenuMode.Main;
                    break;
            }

            listingStd.End ();
            #endregion

            base.DoSettingsWindowContents (rect);
        }

        public override string SettingsCategory () {
            return GitUpdateCore.modName;
        }
    }
}
