using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using IPA.Utilities;
using UnityEngine;

namespace BeatSaverDownloader.Misc
{
    public class SongDownloader : MonoBehaviour
    {
        public event Action<BeatSaverSharp.Models.Beatmap> SongDownloaded;

        private static SongDownloader _instance = null;

        public static SongDownloader Instance
        {
            get
            {
                if (!_instance)
                    _instance = new GameObject("SongDownloader").AddComponent<SongDownloader>();
                return _instance;
            }
        }

        private HashSet<string> _alreadyDownloadedSongs = new HashSet<string>();
        private Dictionary<string, bool> _preparationChanges;
        private int _songLoadRequest;
        private Task _hashPreparation = Task.CompletedTask;

        public void Awake()
        {
            DontDestroyOnLoad(gameObject);

            if (!SongCore.Loader.AreSongsLoaded)
            {
                SongCore.Loader.SongsLoadedEvent += SongLoader_SongsLoadedEvent;
            }
            else
            {
                SongLoader_SongsLoadedEvent(null, SongCore.Loader.CustomLevels);
            }
        }

        public void OnDestroy()
        {
            SongCore.Loader.SongsLoadedEvent -= SongLoader_SongsLoadedEvent;
            ++_songLoadRequest;
            _preparationChanges = null;
        }

        private void SongLoader_SongsLoadedEvent(SongCore.Loader sender, ConcurrentDictionary<string, BeatmapLevel> levels)
        {
            _hashPreparation = PrepareSongHashesAsync(levels);
            ObserveHashPreparation(_hashPreparation);
        }

        private static async void ObserveHashPreparation(Task preparation)
        {
            try { await preparation; }
            catch (Exception e) { Plugin.LOG.Critical(e); }
        }

        internal async Task WaitForHashPreparationAsync()
        {
            await UnityGame.SwitchToMainThreadAsync();
            while (this && !PluginConfig.IsStopping)
            {
                var preparation = _hashPreparation;
                await preparation;
                await UnityGame.SwitchToMainThreadAsync();
                if (ReferenceEquals(preparation, _hashPreparation)) return;
            }
        }

        private async Task PrepareSongHashesAsync(ConcurrentDictionary<string, BeatmapLevel> levels)
        {
            await UnityGame.SwitchToMainThreadAsync();
            if (!this || PluginConfig.IsStopping) return;
            var request = ++_songLoadRequest;
            var changes = new Dictionary<string, bool>();
            _preparationChanges = changes;
            try
            {
                Plugin.LOG.Debug("Establishing Already Downloaded Songs");
                var hashes = levels.Values.Select(x => SongCore.Collections.GetCustomLevelHash(x.levelID)).ToArray();
                var prepared = await Task.Run(() => new HashSet<string>(hashes));
                await UnityGame.SwitchToMainThreadAsync();
                if (!this || PluginConfig.IsStopping || request != _songLoadRequest) return;
                foreach (var change in changes)
                    if (change.Value) prepared.Add(change.Key);
                    else prepared.Remove(change.Key);
                _alreadyDownloadedSongs = prepared;
            }
            catch (Exception e)
            {
                Plugin.LOG.Critical(e);
            }
            finally
            {
                await UnityGame.SwitchToMainThreadAsync();
                if (request == _songLoadRequest)
                    _preparationChanges = null;
            }
        }

        public async Task DownloadSong(BeatSaverSharp.Models.Beatmap song, System.Threading.CancellationToken token, IProgress<double> progress = null, bool direct = false)
        {
            await UnityGame.SwitchToMainThreadAsync();
            var version = song.LatestVersion;
            var hash = version.Hash.ToUpper();
            var id = song.ID;
            var songName = song.Metadata.SongName;
            var levelAuthor = song.Metadata.LevelAuthorName;
            try
            {
                string customSongsPath = CustomLevelPathHelper.customLevelsDirectoryPath;
                await Task.Run(() =>
                {
                    if (!Directory.Exists(customSongsPath))
                        Directory.CreateDirectory(customSongsPath);
                });
                await UnityGame.SwitchToMainThreadAsync();
                if (!this || PluginConfig.IsStopping) return;
                var zip = await version.DownloadZIP(token, progress);
                Plugin.LOG.Info("Downloaded zip!");
                await Task.Run(() => ExtractZip(id, songName, levelAuthor, zip, customSongsPath));
                await UnityGame.SwitchToMainThreadAsync();
                if (!this || PluginConfig.IsStopping) return;
                SongDownloaded?.Invoke(song);

            }
            catch (Exception e)
            {
                await UnityGame.SwitchToMainThreadAsync();
                Plugin.LOG.Critical(e);
                if (e is TaskCanceledException)
                    Plugin.LOG.Warn("Song Download Aborted.");
                else
                    Plugin.LOG.Critical("Failed to download Song!");
                if (this && !PluginConfig.IsStopping && _alreadyDownloadedSongs.Remove(hash))
                {
                    if (_preparationChanges != null)
                        _preparationChanges[hash] = false;
                }
            }
        }

        private static void ExtractZip(string id, string songName, string levelAuthor, byte[] zip, string customSongsPath, bool overwrite = false)
        {
            using (Stream zipStream = new MemoryStream(zip))
            {
                try
                {
                    Plugin.LOG.Info("Extracting...");
                    using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
                    {
                        var basePath = id + " (" + songName + " - " + levelAuthor + ")";
                        basePath = string.Join("", basePath.Split(Path.GetInvalidFileNameChars().Concat(Path.GetInvalidPathChars()).ToArray()));
                        var path = customSongsPath + "/" + basePath;
                        if (!overwrite && Directory.Exists(path))
                        {
                            var pathNum = 1;
                            while (Directory.Exists(path + $" ({pathNum})")) ++pathNum;
                            path += $" ({pathNum})";
                        }

                        if (!Directory.Exists(path))
                            Directory.CreateDirectory(path);
                        Plugin.LOG.Info(path);
                        ExtractFiles(archive, path, overwrite);
                    }
                }
                catch (Exception e)
                {
                    Plugin.LOG.Critical($"Unable to extract ZIP! Exception: {e}");
                }
            }
        }

        private static void ExtractFiles(ZipArchive archive, string path, bool overwrite)
        {
            foreach (var entry in archive.Entries)
            {
                var entryPath = Path.Combine(path, entry.Name); // Name instead of FullName for better security and because song zips don't have nested directories anyway
                if (overwrite || !File.Exists(entryPath)) // Either we're overwriting or there's no existing file
                    entry.ExtractToFile(entryPath, overwrite);
            }
        }

        public void QueuedDownload(string hash)
        {
            if (!Instance._alreadyDownloadedSongs.Contains(hash))
                Instance._alreadyDownloadedSongs.Add(hash);
            if (Instance._preparationChanges != null)
                Instance._preparationChanges[hash] = true;
        }

        public static bool IsSongDownloaded(string hash)
        {
            return Instance._alreadyDownloadedSongs.Contains(hash.ToUpper());
        }
    }
}
