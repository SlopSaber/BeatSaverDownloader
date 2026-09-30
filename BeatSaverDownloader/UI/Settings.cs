using System.Collections.Generic;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Settings;
using BeatSaberMarkupLanguage.Components;
using BeatSaverDownloader.Bookmarks;
using BeatSaverDownloader.Misc;

namespace BeatSaverDownloader.UI
{
    public class Settings : NotifiableBase
    {
        [UIValue("syncOnLoad")]
        public bool SyncOnLoad
        {
            get => PluginConfig.SyncOnLoad;
            set
            {
                PluginConfig.SyncOnLoad = value;
                PluginConfig.SaveInBackground();
                NotifyPropertyChanged();
            }
        }

        [UIValue("logoutInteractable")]
        public bool LogoutInteractable
        {
            get => PluginConfig.UserTokens?.CouldBeValid == true;
            set => NotifyPropertyChanged();
        }

        [UIAction("logout")]
        private void Logout()
        {
            PluginConfig.UserTokens = null;
            PluginConfig.SaveInBackground();

            LogoutInteractable = true;
        }

        [UIValue("envChoice")]
        public string EnvChoice
        {
            get => PluginConfig.OauthEnvironment;
            set
            {
                PluginConfig.OauthEnvironment = value;
                PluginConfig.SaveInBackground();
                NotifyPropertyChanged();
            }
        }

        [UIValue("envOptions")]
        public List<object> EnvOptions => OauthConfig.Options;

        public static void SetupSettings()
        {
            BSMLSettings.Instance.AddSettingsMenu("BeatSaverDL", "BeatSaverDownloader.UI.BSML.settings.bsml", Plugin.SettingsView);
        }
    }
}
