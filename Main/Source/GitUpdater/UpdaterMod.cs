using System;
using System.Linq;
using System.Collections.Generic;

using UnityEngine;
using Verse;
using LibGit2Sharp;

namespace GitUpdater {
    public class UpdaterMod : Mod {
        public class Settings : ModSettings {
            public enum ListMode {
                Whitelist,
                Blacklist
            }

            public bool requireManual = false;
            public List<string> repoList = new List<string>() { GitUpdateCore.GetID () };
            public ListMode listHandling = ListMode.Blacklist;
            public bool pruneOld = true;

            public override void ExposeData () {
                Scribe_Values.Look (ref requireManual, nameof (requireManual));
                Scribe_Collections.Look (ref repoList, nameof (repoList));
                Scribe_Values.Look (ref listHandling, nameof (listHandling));
                Scribe_Values.Look (ref pruneOld, nameof (pruneOld));
                base.ExposeData ();
            }

            public static string LMString (ListMode mode) {
                return "GU." + mode.ToString ();
            }

            public static TaggedString LMStringTranslated (ListMode mode) {
                return LMString (mode).Translate ();
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

            IEnumerable<ModMetaData> mods = ModLister.AllInstalledMods;
            IEnumerable<ModMetaData> satisfiers;
            if (!isClean) {
                cache = new List<ModMetaData> (mods.Where (condition));
                isClean = true;
            }
            satisfiers = cache;

            inner.height = satisfiers.Count () * ITEM_HEIGHT;

            Rect itemRect = new Rect (0f, area.y, inner.width, ITEM_HEIGHT);

            Widgets.BeginScrollView (area, ref scrollPos, inner);
            foreach (ModMetaData mod in satisfiers) {
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

                    listingStd.Label ("GU.Repos".Translate ());
                    // Temp cache since we don't want to do much
                    List<ModMetaData> savedMods = settings.repoList.ConvertAll (id => ModLister.GetModWithIdentifier (id));
                    ListMods ( listingStd,
                               m => IsSavedRepo (m),
                               m => settings.repoList.Remove (m.PackageId),
                               modsArea,
                               ref savedMods,
                               ref savedReposCached
                             );

                    float padding = 5;
                    float halfWidth = rect.width / 2;
                    bool add = Widgets.ButtonText (new Rect (padding, listingStd.CurHeight, halfWidth - padding, ITEM_HEIGHT),
                                                   "GU.Add".Translate ()
                                                  );
                    if (add)
                        menu = MenuMode.AddMod;
                    bool switchList = Widgets.ButtonText (new Rect (halfWidth + padding, listingStd.CurHeight, halfWidth - padding, ITEM_HEIGHT),
                                                          Settings.LMStringTranslated (settings.listHandling)
                                                         );
                    if (switchList) {
                        var floatOptions = new List<FloatMenuOption> () { new FloatMenuOption (Settings.LMStringTranslated (Settings.ListMode.Blacklist), () => settings.listHandling = Settings.ListMode.Blacklist),
                                                                          new FloatMenuOption (Settings.LMStringTranslated (Settings.ListMode.Whitelist), () => settings.listHandling = Settings.ListMode.Whitelist)
                                                                        };
                        Find.WindowStack.Add (new FloatMenu (floatOptions));
                    }
                    listingStd.Gap (ITEM_HEIGHT);

                    if (settings.requireManual) {
                        listingStd.GapLine ();

                        bool doManual = listingStd.ButtonText ("GU.ManualUpdate".Translate ());
                        if (doManual) {
                            GitUpdateCore.UpdateRepos (settings.listHandling);
                            needsRestart = true;
                        }

                        if (needsRestart)
                            listingStd.Label ("GU.RestartRequired".Translate ());
                    }

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
