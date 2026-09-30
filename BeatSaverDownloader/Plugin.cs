using BeatSaverDownloader.Misc;
using BeatSaverDownloader.UI;
using IPA;
using System;
using System.Collections.Concurrent;
using BeatSaberMarkupLanguage.MenuButtons;
using BeatSaverDownloader.Bookmarks;
using BeatSaverDownloader.UI.ViewControllers.DownloadQueue;
using BeatSaverSharp.Http;
using BS_Utils.Utilities;
using IPA.Loader;
using IPA.Utilities;
using System.Threading.Tasks;

namespace BeatSaverDownloader
{
    public enum SongQueueState { Queued, Downloading, Downloaded, Error };

    [Plugin(RuntimeOptions.SingleStartInit)]
    public class Plugin
    {
        private static readonly PluginUI PluginView = new PluginUI();
        internal static readonly Settings SettingsView = new Settings();
        private PluginMetadata _metadata;
        public static IPA.Logging.Logger LOG;
        public static BeatSaverSharp.BeatSaver BeatSaver;
        private CallbackListener _listener;
        private BookmarksApi _bookmarksApi;
        private QueueManager _queueManager;
        private Task _initialization = Task.CompletedTask;
        private bool _enabled;
        private bool _exiting;
        private int _menuRequest;
        public static string UserAgent = "";

        [Init]
        public void Init(object nullObject, IPA.Logging.Logger logger, PluginMetadata metadata)
        {
            UserAgent = $"BeatSaverDownloader/{metadata.HVersion}";
            _metadata = metadata;
            LOG = logger;
        }

        public void OnApplicationQuit()
        {
            PluginConfig.SaveAndFlush();
        }

        [OnStart]
        public void OnApplicationStart()
        {
            BSEvents.lateMenuSceneLoadedFresh += OnMenuSceneLoadedFresh;
            _initialization = InitializeAsync();
            ObserveInitialization();
        }

        private async void ObserveInitialization()
        {
            try { await _initialization; }
            catch (Exception e) { LOG.Critical(e); }
        }

        private async Task InitializeAsync()
        {
            BeatSaver = new BeatSaverSharp.BeatSaver(
                new BeatSaverSharp.BeatSaverOptions(
                    "BeatSaverDownloader",
                    new Version((int) _metadata.HVersion.Major, (int) _metadata.HVersion.Minor, (int) _metadata.HVersion.Patch)
                )
            );

            await PluginConfig.LoadConfigAsync();
            await UnityGame.SwitchToMainThreadAsync();
            if (_exiting) return;
            Sprites.ConvertToSprites();

            if (OauthConfig.Current.AppAuth != null)
            {
                var httpService = (UnityWebRequestService) BeatSaver.GetField<IHttpService, BeatSaverSharp.BeatSaver>("_httpService");
                httpService.Headers.Add("X-App-Auth", OauthConfig.Current.AppAuth);
            }

            var tokenApi = new TokenApi(_metadata);
            _listener = new CallbackListener(tokenApi);
            _queueManager = new QueueManager();
            _bookmarksApi = new BookmarksApi(tokenApi, _queueManager);

            PluginView.Setup(_bookmarksApi, _queueManager);

            if (PluginManager.GetPlugin("BetterSongList") != null)
                RegisterBookmarksFilter();

            if (_enabled)
                _listener.Start();
        }

        private void RegisterBookmarksFilter()
        {
            var _ = new BookmarksFilter(_bookmarksApi);
        }

        [OnEnable]
        public void OnEnable()
        {
            _enabled = true;
            _listener?.Start();
        }

        [OnDisable]
        public void OnDisable()
        {
            _enabled = false;
            _listener?.Stop();
        }

        [OnExit]
        public void OnExit()
        {
            _exiting = true;
            _listener?.Stop();
            BSEvents.lateMenuSceneLoadedFresh -= OnMenuSceneLoadedFresh;
            SongCore.Loader.SongsLoadedEvent -= Loader_SongsLoadedEvent;
            PluginConfig.SaveAndFlush();
            _bookmarksApi?.Store();
        }

        private async void OnMenuSceneLoadedFresh(ScenesTransitionSetupData data)
        {
            var request = ++_menuRequest;
            try
            {
                await _initialization;
                await UnityGame.SwitchToMainThreadAsync();
                if (_exiting || request != _menuRequest) return;
                PluginUI.SetupLevelDetailClone();
                Settings.SetupSettings();

                MenuButtons.Instance.RegisterButton(PluginView.MoreSongsButton);

                SongCore.Loader.SongsLoadedEvent -= Loader_SongsLoadedEvent;
                SongCore.Loader.SongsLoadedEvent += Loader_SongsLoadedEvent;
                if (SongCore.Loader.AreSongsLoaded)
                    Loader_SongsLoadedEvent(null, SongCore.Loader.CustomLevels);
            }
            catch (Exception e)
            {
                LOG.Critical("Exception on fresh menu scene change: " + e);
            }
        }

        private async void Loader_SongsLoadedEvent(SongCore.Loader arg1, ConcurrentDictionary<string, BeatmapLevel> arg2)
        {
            await UnityGame.SwitchToMainThreadAsync();
            if (_exiting) return;
            if (PluginView.MoreSongsButton.Interactable) return;

            PluginView.MoreSongsButton.Interactable = true;
            MenuButtons.Instance.InvokeMethod<object, MenuButtons>("Refresh");

            if (PluginConfig.UserTokens?.CouldBeValid == true && PluginConfig.SyncOnLoad)
            {
                try
                {
                    await _bookmarksApi.Sync(false);
                }
                catch (TokenApi.InvalidOauthCredentialsException)
                {
                    // Do nothing
                }
            }
        }
    }
}
