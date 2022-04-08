using System;
using System.Linq;
using System.IO;

using Verse;
using LibGit2Sharp;

namespace GitUpdater {
    using ListMode = UpdaterMod.Settings.ListMode;
    public static class GitUpdateCore {
        // IF YOU ARE FORKING THIS, PLEASE EDIT THIS DATA
        private const string email = "james.inness.work@gmail.com";
        public const string modName = "GitUpdater";

        public enum LogMode {
            Event,
            Warn,
            Error
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

            var refSpecs = remote.FetchRefSpecs.Select (r => r.Specification);
            var fOptions = new FetchOptions ();
            if (UpdaterMod.settings.pruneOld)
                fOptions.Prune = true;

            var mOptions = new MergeOptions ();
            mOptions.CommitOnSuccess = true;

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
                repo.MergeFetchedRefs (sig, mOptions);
            }
            catch (Exception e) {
                LogMsg ($"Could not merge updates from repo: {e.Message}", LogMode.Error);
                return;
            }

            LogMsg ("Repository updated.", LogMode.Event);
        }

        public static void UpdateRepos (ListMode listMode) {
            LogMsg ($"Searching for repos out of {ModLister.AllInstalledMods.Count ()} mods (Mode: {listMode})", LogMode.Event);
            foreach (ModMetaData mod in ModLister.AllInstalledMods) {
                string modPath = mod.RootDir.FullName;

                bool isRepo;
                switch (listMode) {
                    case ListMode.Whitelist:
                        isRepo = UpdaterMod.IsSavedRepo (mod);
                        break;
                    case ListMode.Blacklist:
                        isRepo = UpdaterMod.IsUnsavedRepo (mod);
                        break;
                    default:
                        throw new NotImplementedException ("Unknown list mode");
                }

                if (!isRepo)
                    continue;

                LogMsg ($"{mod.Name} has a git repo", LogMode.Event);
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

            // Change directory temporarily to our mod so that our DLL can fetch natives inside Natives.
            Directory.SetCurrentDirectory (thisMod.RootDir.FullName);
            // Test run, doesn't matter what we do so long as it initializes.
            Repository.IsValid (".");
            Directory.SetCurrentDirectory (gameDir);
        }
    }
}