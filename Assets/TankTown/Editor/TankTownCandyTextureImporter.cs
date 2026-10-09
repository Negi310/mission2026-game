using UnityEditor;
using UnityEngine;

namespace TankTown.Editor
{
    // Keep the ten shared candy maps compact and stable at distance.
    public sealed class TankTownCandyTextureImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/TankTown/Resources/CandyTextures/") || !assetPath.EndsWith(".png")) return;
            var texture = (TextureImporter)assetImporter;
            texture.textureType = TextureImporterType.Default;
            texture.textureShape = TextureImporterShape.Texture2D;
            texture.sRGBTexture = true;
            texture.alphaSource = TextureImporterAlphaSource.None;
            texture.mipmapEnabled = true;
            texture.isReadable = false;
            texture.maxTextureSize = 1024;
            texture.filterMode = FilterMode.Trilinear;
            texture.wrapMode = assetPath.EndsWith("CandySwirl.png") ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            texture.anisoLevel = 4;
            texture.textureCompression = TextureImporterCompression.CompressedHQ;
            var platform = texture.GetPlatformTextureSettings("Standalone");
            platform.overridden = true;
            platform.maxTextureSize = 1024;
            platform.format = TextureImporterFormat.BC7;
            platform.compressionQuality = 80;
            texture.SetPlatformTextureSettings(platform);
        }
    }
}
