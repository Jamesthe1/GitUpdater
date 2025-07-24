using System;
using System.Linq;
using System.IO;

using Verse;
using LibGit2Sharp;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace GitUpdater {
    using ListMode = UpdaterMod.Settings.ListMode;
    public static class GitUpdateCore {
        // personal info not relevant
        private const string email = "na@na.na";
        public const string modName = "GitUpdater";
        private const string prefix = "GU";

        public enum LogMode {
            Event,
            Warn,
            Error
        }

        public static string PrefixItem<T> (T item, string extraPrefix = "") {
            // Inline automatically does ToString for us
            string itemName = extraPrefix == "" ? item.ToString () : $"{extraPrefix}.{item}";
            return $"{prefix}.{itemName}";
        }

        public static string PrefixTranslateItem<T> (T item, string extraPrefix = "") {
            return PrefixItem (item, extraPrefix).Translate ();
        }

        public static string GetID () {
            return ModLister.AllInstalledMods.First (m => m.Active && m.Name == modName).PackageId;
        }

        public static void LogMsg (string message, LogMode mode) {
            Action<string> log;
            switch (mode) {
                case LogMode.Event:
                    log = Log.Message;
                    break;
                case LogMode.Warn:
                    log = Log.Warning;
                    break;
                case LogMode.Error:
                    log = Log.Error;
                    break;
                default:
                    throw new NotImplementedException ("This log mode hasn't been implemented yet.");
            }

            log ($"[{modName}] {message}");
        }

        private static void CheckForRepoUpdates (Repository repo) {
            var remote = repo.Network.Remotes["origin"];

            if (remote == null) {
                LogMsg ("Repository has no origin, skipping", LogMode.Warn);
                return;
            }

            var regex = new Regex (@"^git@");
            if (regex.IsMatch (remote.Url)) {
                LogMsg ("Can't get SSH urls at the moment, sorry :(", LogMode.Warn);
                return;
            }

            var refSpecs = remote.FetchRefSpecs.Select (r => r.Specification);
            var fOptions = new FetchOptions ();
            fOptions.Prune = UpdaterMod.settings.pruneOld;

            var mOptions = new MergeOptions ();
            mOptions.CommitOnSuccess = true;
            mOptions.FileConflictStrategy = UpdaterMod.settings.onFileConflict;

            var sig = new Signature (new Identity (modName, email), DateTimeOffset.Now);

            try {
                string logHolder = "";
                Commands.Fetch (repo, remote.Name, refSpecs, fOptions, logHolder);

                if (logHolder.Length > 0)
                    LogMsg (logHolder, LogMode.Warn);
            }
            catch (Exception e) {
                LogMsg ($"Could not fetch data from remote repo: {e.Message}", LogMode.Error);
                return;
            }

            try {
                MergeResult rslt = repo.MergeFetchedRefs (sig, mOptions);
                switch (rslt.Status) {
                    case MergeStatus.UpToDate:
                        LogMsg ("Repo already up-to-date.", LogMode.Event);
                        return;
                    case MergeStatus.FastForward:
                        LogMsg ($"Fast-forwarded to new head {repo.Head.FriendlyName}.", LogMode.Event);
                        return;
                    case MergeStatus.NonFastForward:
                        LogMsg ($"Non-fast-forward to new head {repo.Head.FriendlyName}.", LogMode.Event);
                        return;
                    case MergeStatus.Conflicts:
                        LogMsg ($"Conflicts appeared during merge. You may have to implement these manually, or do it quick and dirty by using the \"{"GU.FC.Theirs".Translate ()}\" option.", LogMode.Warn);
                        return;
                }
            }
            catch (Exception e) {
                LogMsg ($"Could not merge updates from repo: {e.Message}", LogMode.Error);
            }
        }

        public static List<ModMetaData> GetModsOfCondition (Func<ModMetaData, bool> condition) {
            return ModLister.AllInstalledMods
                    .Where (condition)
                    .ToList ();
        }

        public static List<ModMetaData> GetUpdateableModsOfCondition (Func<ModMetaData, bool> condition) {
            return GetModsOfCondition (m => condition (m) && UpdaterMod.CanBeUpdated (m));
        }

        public static void UpdateRepos (UpdaterMod.Settings settings) {
            // Not sure if I can trust "isSteamWorkshop"
            Func<ModMetaData, bool> condition = null;
            switch (settings.listHandling) {
                case ListMode.Whitelist:
                    condition = UpdaterMod.IsSavedRepo;
                    break;
                case ListMode.Blacklist:
                    condition = UpdaterMod.IsUnsavedRepo;
                    break;
                default:
                    throw new NotImplementedException ("Unknown list mode");
            }
            List<ModMetaData> mods = GetUpdateableModsOfCondition (condition);
            LogMsg ($"Looking for updates in {mods.Count ()} mod(s) (Mode: {settings.listHandling}, Only local: {settings.onlyLocal})", LogMode.Event);

            foreach (ModMetaData mod in mods) {
                string modPath = mod.RootDir.FullName;
                LogMsg ($"Analyzing {mod.Name}...", LogMode.Event);
                using (var repo = new Repository (modPath)) {
                    CheckForRepoUpdates (repo);
                }
            }
            LogMsg ("All mods with repos have been updated.", LogMode.Event);
        }

        // Just so that it can load the DLL properly, we need this
        static GitUpdateCore () {
            ModMetaData thisMod = ModLister.GetActiveModWithIdentifier (GetID ());
            string gameDir = Directory.GetCurrentDirectory ();
            string modDir = thisMod.RootDir.FullName + "/Main/Natives";

            // Change directory temporarily to our mod so that our DLL can fetch natives inside Natives.
            Directory.SetCurrentDirectory (modDir);

            // Test run, doesn't matter what we do so long as it initializes.
            Repository.IsValid (".");
            Directory.SetCurrentDirectory (gameDir);
        }
    }
}