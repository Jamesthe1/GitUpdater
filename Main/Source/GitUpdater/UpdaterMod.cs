using System;
using System.Linq;
using System.IO;

using Verse;
using LibGit2Sharp;

namespace GitUpdater {
    [StaticConstructorOnStartup]
    public static class UpdaterMod {
        private const string prefix = "Jamesthe1.RimWorld";
        public const string modName = "GitUpdater";

        private static void LogEvent (string message) {
            Log.Message ($"[{modName}] {message}");
        }

        private static void LogError (string message) {
            Log.Error (message);
        }

        private static void CheckForRepoUpdates (Repository repo) {
            var remote = repo.Network.Remotes["origin"];

            if (remote == null) {
                LogEvent ($"Repository has no origin, skipping");
                return;
            }

            var refSpecs = remote.FetchRefSpecs.Select (r => r.Specification);
            var mOptions = new MergeOptions ();
            mOptions.CommitOnSuccess = true;
            var sig = new Signature (new Identity (modName, "jamesthe1sky@gmail.com"), DateTimeOffset.Now);

            try {
                string logHolder = "";
                Commands.Fetch (repo, remote.Name, refSpecs, null, logHolder);

                if (logHolder.Length > 0)
                    LogEvent (logHolder);
            }
            catch (Exception e) {
                LogError ($"Could not fetch data from remote repo: {e.Message}");
                return;
            }

            try {
                repo.MergeFetchedRefs(sig, mOptions);
            }
            catch (Exception e) {
                LogError ($"Could not merge updates from repo: {e.Message}");
                return;
            }

            LogEvent ("Repository updated.");
        }

        private static void UpdateRepos () {
            LogEvent ($"Searching for repos out of {ModLister.AllInstalledMods.Count ()} mods");
            foreach (ModMetaData mod in ModLister.AllInstalledMods) {
                string modPath = mod.RootDir.FullName;
                
                if (!Repository.IsValid (modPath))
                    continue;

                LogEvent ($"{mod.Name} has a git repo");
                using (var repo = new Repository (modPath)) {
                    CheckForRepoUpdates (repo);
                }
            }
            LogEvent ("All mods with repos have been updated.");
        }

        static UpdaterMod () {
            ModMetaData thisMod =  ModLister.GetActiveModWithIdentifier ($"{prefix}.{modName}");
            string gameDir = Directory.GetCurrentDirectory ();

            Directory.SetCurrentDirectory (thisMod.RootDir.FullName);
            LogEvent ($"Natives are now at {Path.GetFullPath ("./Main/Natives")}");
            UpdateRepos ();
            Directory.SetCurrentDirectory (gameDir);
        }
    }
}