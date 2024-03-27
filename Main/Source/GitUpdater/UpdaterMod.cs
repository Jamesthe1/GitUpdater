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
            public bool onlyLocal = true;
            public bool pruneOld = true;
            public CheckoutFileConflictStrategy onFileConflict = CheckoutFileConflictStrategy.Normal;
            public List<string> repoList = new List<string> () { GitUpdateCore.GetID () };
            public ListMode listHandling = ListMode.Blacklist;

            public override void ExposeData () {
                Scribe_Values.Look (ref requireManual, nameof (requireManual));
                Scribe_Values.Look (ref onlyLocal, nameof (onlyLocal));
                Scribe_Values.Look (ref pruneOld, nameof (pruneOld));
                Scribe_Values.Look (ref onFileConflict, nameof (onFileConflict));
                Scribe_Collections.Look (ref repoList, nameof (repoList));
                Scribe_Values.Look (ref listHandling, nameof (listHandling));
                base.ExposeData ();
            }
        }

        public static Settings settings;

        public static bool IsSavedRepo (ModMetaData mod) {
            return settings.repoList.Contains (mod.PackageId);
        }

        public static bool IsUnsavedRepo (ModMetaData mod) {
            return Repository.IsValid (mod.RootDir.FullName) && !IsSavedRepo (mod);
        }

        public void AddModRepo (ModMetaData mod) {
            settings.repoList.Add (mod.PackageId);
        }

        public void RemoveModRepo (ModMetaData mod) {
            settings.repoList.Remove (mod.PackageId);
            UpdaterMenus.reposCached = false;
        }

        /// <summary>
        /// Used for checking if a mod can be updated, according to onlyLocal.
        /// </summary>
        /// <param name="mod">The mod metadata to inspect</param>
        /// <returns>Whether or not the mod can be updated</returns>
        public static bool CanBeUpdated (ModMetaData mod) {
            return !mod.OnSteamWorkshop || !settings.onlyLocal;   // Same thing as "onlyLocal ? mod.SteamAppId == 0 : true", just with fewer steps
        }

        public UpdaterMod (ModContentPack content) : base(content) {
            settings = GetSettings<Settings> ();
            UpdaterMenus.lastLocalRuleState = settings.onlyLocal;    // Require local update
            string isManualStr = settings.requireManual ? "not" : "automatically";
            GitUpdateCore.LogMsg ($"Settings loaded. Will {isManualStr} update repositories right now.", GitUpdateCore.LogMode.Event);

            if (!settings.requireManual)
                GitUpdateCore.UpdateRepos (settings);

            // Cleanse any mods in our list that were removed
            // Using ToList to create a copy; this will avoid the mysterious error "Collection was modified; enumeration operation may not execute."
            foreach (string id in settings.repoList.ToList ())
                if (ModLister.GetModWithIdentifier (id) == null)
                    settings.repoList.Remove (id);
        }

        public override void DoSettingsWindowContents (Rect rect) {
            UpdaterMenus.RenderSettingsWindow (rect, this);
            base.DoSettingsWindowContents (rect);
        }

        public override string SettingsCategory () {
            return GitUpdateCore.modName;
        }
    }
}
