using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using IPA.Utilities;
using UnityEngine;

namespace BeatSaverDownloader.Misc
{
    internal class Sprites
    {
        public static Sprite AddToFavorites;
        public static Sprite RemoveFromFavorites;
        public static Sprite StarFull;
        public static Sprite StarEmpty;
        public static Sprite DoubleArrow;

        public static Sprite ReviewIcon;

        //https://www.flaticon.com/free-icon/thumbs-up_70420
        public static Sprite ThumbUp;

        //https://www.flaticon.com/free-icon/dislike-thumb_70485
        public static Sprite ThumbDown;

        //https://www.flaticon.com/free-icon/playlist_727239
        public static Sprite PlaylistIcon;

        //https://www.flaticon.com/free-icon/musical-note_727218
        public static Sprite SongIcon;

        //https://www.flaticon.com/free-icon/download_724933
        public static Sprite DownloadIcon;

        //https://www.flaticon.com/free-icon/media-play-symbol_31128
        public static Sprite PlayIcon;

        //https://game-icons.net/1x1/delapouite/perspective-dice-six-faces-three.html
        public static Sprite RandomIcon;

        //https://www.flaticon.com/free-icon/waste-bin_70388
        public static Sprite DeleteIcon;

        public static Sprite BeatSaverIcon;
        public static Sprite ScoreSaberIcon;
        //by elliotttate#9942
        public static Sprite BeastSaberLogo;
        private static readonly string[] SpriteResources =
        {
            "BeatSaverDownloader.Assets.AddToFavorites.png",
            "BeatSaverDownloader.Assets.RemoveFromFavorites.png",
            "BeatSaverDownloader.Assets.StarFull.png",
            "BeatSaverDownloader.Assets.StarEmpty.png",
            "BeatSaverDownloader.Assets.BeastSaberLogo.png",
            "BeatSaverDownloader.Assets.ReviewIcon.png",
            "BeatSaverDownloader.Assets.ThumbUp.png",
            "BeatSaverDownloader.Assets.ThumbDown.png",
            "BeatSaverDownloader.Assets.PlaylistIcon.png",
            "BeatSaverDownloader.Assets.SongIcon.png",
            "BeatSaverDownloader.Assets.DownloadIcon.png",
            "BeatSaverDownloader.Assets.PlayIcon.png",
            "BeatSaverDownloader.Assets.DoubleArrow.png",
            "BeatSaverDownloader.Assets.RandomIcon.png",
            "BeatSaverDownloader.Assets.DeleteIcon.png",
            "BeatSaverDownloader.Assets.BeatSaver.png",
            "BeatSaverDownloader.Assets.ScoreSaber.png",
        };

        public static void ConvertToSprites()
        {
            PublishSprites(ReadSpriteBytes());
        }

        internal static async Task ConvertToSpritesAsync()
        {
            var images = await Task.Run(ReadSpriteBytes);
            await UnityGame.SwitchToMainThreadAsync();
            if (!PluginConfig.IsStopping)
                PublishSprites(images);
        }

        private static byte[][] ReadSpriteBytes()
        {
            var assembly = typeof(Sprites).Assembly;
            var images = new byte[SpriteResources.Length][];
            for (var i = 0; i < images.Length; ++i)
                images[i] = GetResource(assembly, SpriteResources[i]);
            return images;
        }

        private static void PublishSprites(byte[][] images)
        {
            AddToFavorites = LoadSpriteRaw(images[0]);
            RemoveFromFavorites = LoadSpriteRaw(images[1]);
            StarFull = LoadSpriteRaw(images[2]);
            StarEmpty = LoadSpriteRaw(images[3]);
            BeastSaberLogo = LoadSpriteRaw(images[4]);
            ReviewIcon = LoadSpriteRaw(images[5]);
            ThumbUp = LoadSpriteRaw(images[6]);
            ThumbDown = LoadSpriteRaw(images[7]);
            PlaylistIcon = LoadSpriteRaw(images[8]);
            SongIcon = LoadSpriteRaw(images[9]);
            DownloadIcon = LoadSpriteRaw(images[10]);
            PlayIcon = LoadSpriteRaw(images[11]);
            DoubleArrow = LoadSpriteRaw(images[12]);
            RandomIcon = LoadSpriteRaw(images[13]);
            DeleteIcon = LoadSpriteRaw(images[14]);
            BeatSaverIcon = LoadSpriteRaw(images[15]);
            ScoreSaberIcon = LoadSpriteRaw(images[16]);
        }

        public static string SpriteToBase64(Sprite input)
        {
            return Convert.ToBase64String(input.texture.EncodeToPNG());
        }

        public static Sprite Base64ToSprite(string input)
        {
            string base64 = input;
            if (input.Contains(","))
            {
                base64 = input.Substring(input.IndexOf(','));
            }
            Texture2D tex = Base64ToTexture2D(base64);
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), (Vector2.one / 2f));
        }

        private static Texture2D Base64ToTexture2D(string encodedData)
        {
            var imageData = Convert.FromBase64String(encodedData);
            var tex2D = new Texture2D(2, 2);
            return tex2D.LoadImage(imageData) ? tex2D : null;
        }

        // Image helpers

        private static Texture2D LoadTextureRaw(byte[] file)
        {
            if (file == null || file.Length == 0) return null;

            var tex2D = new Texture2D(2, 2);
            if (tex2D.LoadImage(file)) return tex2D;
            UnityEngine.Object.Destroy(tex2D);
            return null;
        }

        private static Texture2D LoadTextureFromFile(string filePath)
        {
            return File.Exists(filePath) ?
                LoadTextureRaw(File.ReadAllBytes(filePath)) :
                null;
        }

        public static Texture2D LoadTextureFromResources(string resourcePath)
        {
            return LoadTextureRaw(GetResource(Assembly.GetCallingAssembly(), resourcePath));
        }

        public static Sprite LoadSpriteRaw(byte[] image, float pixelsPerUnit = 100.0f)
        {
            return LoadSpriteFromTexture(LoadTextureRaw(image), pixelsPerUnit);
        }

        public static Sprite LoadSpriteFromTexture(Texture2D spriteTexture, float pixelsPerUnit = 100.0f)
        {
            return spriteTexture ?
                Sprite.Create(spriteTexture, new Rect(0, 0, spriteTexture.width, spriteTexture.height), new Vector2(0, 0), pixelsPerUnit) :
                null;
        }

        public static Sprite LoadSpriteFromFile(string filePath, float pixelsPerUnit = 100.0f)
        {
            return LoadSpriteFromTexture(LoadTextureFromFile(filePath), pixelsPerUnit);
        }

        private static Sprite LoadSpriteFromResources(string resourcePath, float pixelsPerUnit = 100.0f)
        {
            return LoadSpriteRaw(GetResource(Assembly.GetCallingAssembly(), resourcePath), pixelsPerUnit);
        }

        private static byte[] GetResource(Assembly asm, string resourceName)
        {
            using (var stream = asm.GetManifestResourceStream(resourceName))
            {
                if (stream == null) return null;
                var data = new byte[stream.Length];
                var offset = 0;
                while (offset < data.Length)
                {
                    var read = stream.Read(data, offset, data.Length - offset);
                    if (read == 0) break;
                    offset += read;
                }
                return data;
            }
        }
    }
}
