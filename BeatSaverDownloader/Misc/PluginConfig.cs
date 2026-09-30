using System.IO;
using BeatSaverDownloader.Bookmarks;
using System;
using System.Threading.Tasks;
using IPA.Utilities;

namespace BeatSaverDownloader.Misc
{
    public static class PluginConfig
    {
        private static readonly object FileQueueLock = new object();
        private static Task pendingFiles = Task.CompletedTask;
        // This instance and its mutable parser belong exclusively to the file queue.
        private static BS_Utils.Utilities.Config config;
        private static int loadVersion;
        private static int saveVersion;
        public static int MaxSimultaneousDownloads = 3;
        public static bool SyncOnLoad = true;
        public static OauthResponse UserTokens = null;
        public static string OauthEnvironment = "STAGE";
        internal static bool IsLoaded { get; private set; }
        internal static bool IsStopping { get; private set; }

        public static void LoadConfig()
        {
            if (IsStopping) return;
            ++loadVersion;
            Publish(QueueLoad(UnityGame.UserDataPath).GetAwaiter().GetResult());
        }

        public static async Task LoadConfigAsync()
        {
            await UnityGame.SwitchToMainThreadAsync();
            if (IsStopping) return;
            var request = ++loadVersion;
            var saves = saveVersion;
            var snapshot = await QueueLoad(UnityGame.UserDataPath);
            await UnityGame.SwitchToMainThreadAsync();
            if (!IsStopping && request == loadVersion && saves == saveVersion)
                Publish(snapshot);
        }

        private static Task<Snapshot> QueueLoad(string userDataPath)
        {
            return QueueFiles(() =>
            {
                Directory.CreateDirectory(userDataPath);
                var ini = GetConfig();
                return new Snapshot(
                    ini.GetInt("BeatSaverDownloader", "maxSimultaneousDownloads", 3, true),
                    ini.GetBool("BeatSaverDownloader", "syncOnLoad", true),
                    ini.GetString("BeatSaverDownloader", "oauthEnv", "STAGE"),
                    ini.GetString("OAuth", "AccessToken"),
                    ini.GetString("OAuth", "TokenType"),
                    ini.GetInt("OAuth", "ExpiresIn"),
                    ini.GetString("OAuth", "RefreshToken"));
            });
        }

        private static void Publish(Snapshot snapshot)
        {
            MaxSimultaneousDownloads = snapshot.MaxDownloads;
            SyncOnLoad = snapshot.Sync;
            OauthEnvironment = snapshot.Environment;
            UserTokens = new OauthResponse(snapshot.AccessToken, snapshot.TokenType,
                snapshot.ExpiresIn, snapshot.RefreshToken);
            IsLoaded = true;
        }

        public static void SaveConfig()
        {
            if (IsStopping) return;
            QueueSave().GetAwaiter().GetResult();
            Plugin.SettingsView.LogoutInteractable = true;
        }

        public static async Task SaveConfigAsync()
        {
            await UnityGame.SwitchToMainThreadAsync();
            if (IsStopping) return;
            await QueueSave();
            await UnityGame.SwitchToMainThreadAsync();
            if (!IsStopping)
                Plugin.SettingsView.LogoutInteractable = true;
        }

        internal static async void SaveInBackground()
        {
            try
            {
                await SaveConfigAsync();
            }
            catch (Exception e)
            {
                Plugin.LOG.Error("Unable to save downloader settings");
                Plugin.LOG.Error(e);
            }
        }

        internal static void SaveAndFlush()
        {
            if (IsLoaded && !IsStopping)
                QueueSave();
            IsStopping = true;
            Task files;
            lock (FileQueueLock)
                files = pendingFiles;
            try
            {
                files.GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                Plugin.LOG.Error("Unable to finish downloader settings storage");
                Plugin.LOG.Error(e);
            }
        }

        private static Task QueueSave()
        {
            ++saveVersion;
            var tokens = UserTokens;
            var snapshot = new Snapshot(MaxSimultaneousDownloads, SyncOnLoad, OauthEnvironment,
                tokens?.AccessToken ?? "", tokens?.TokenType ?? "", tokens?.ExpiresIn ?? 0,
                tokens?.RefreshToken ?? "");
            return QueueFiles(() =>
            {
                var ini = GetConfig();
                ini.SetInt("BeatSaverDownloader", "maxSimultaneousDownloads", snapshot.MaxDownloads);
                ini.SetBool("BeatSaverDownloader", "syncOnLoad", snapshot.Sync);
                ini.SetString("BeatSaverDownloader", "oauthEnv", snapshot.Environment);
                ini.SetString("OAuth", "AccessToken", snapshot.AccessToken);
                ini.SetString("OAuth", "TokenType", snapshot.TokenType);
                ini.SetInt("OAuth", "ExpiresIn", snapshot.ExpiresIn);
                ini.SetString("OAuth", "RefreshToken", snapshot.RefreshToken);
                return true;
            });
        }

        private static BS_Utils.Utilities.Config GetConfig()
        {
            return config ?? (config = new BS_Utils.Utilities.Config("BeatSaverDownloader"));
        }

        private static Task<T> QueueFiles<T>(Func<T> work)
        {
            lock (FileQueueLock)
            {
                var previous = pendingFiles;
                var next = Task.Run(async () =>
                {
                    try { await previous.ConfigureAwait(false); }
                    catch { }
                    return work();
                });
                // Never include main-thread publication in a shutdown drain.
                pendingFiles = next;
                return next;
            }
        }

        private sealed class Snapshot
        {
            internal readonly int MaxDownloads;
            internal readonly bool Sync;
            internal readonly string Environment;
            internal readonly string AccessToken;
            internal readonly string TokenType;
            internal readonly int ExpiresIn;
            internal readonly string RefreshToken;

            internal Snapshot(int maxDownloads, bool sync, string environment, string accessToken,
                string tokenType, int expiresIn, string refreshToken)
            {
                MaxDownloads = maxDownloads;
                Sync = sync;
                Environment = environment;
                AccessToken = accessToken;
                TokenType = tokenType;
                ExpiresIn = expiresIn;
                RefreshToken = refreshToken;
            }
        }
    }
}
