// BlossomRallyImport.cs - import settings for the Blender-exported cars and environment kit.
// (Runtime MaterialLibrary also remaps materials, so the game looks right even if this did not run.)
using UnityEditor;
using UnityEngine;

namespace BlossomRally.EditorTools
{
    public class BlossomRallyImport : AssetPostprocessor
    {
        const string Root = "Assets/BlossomRally/Resources/";

        public override uint GetVersion() { return 3; }

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root)) return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importBlendShapes = false;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.isReadable = true;                 // needed for runtime static batching
            mi.importNormals = ModelImporterNormals.Import;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;
            var ti = (TextureImporter)assetImporter;
            ti.mipmapEnabled = true;
            ti.anisoLevel = 4;
            ti.wrapMode = assetPath.Contains("ENV_Road_") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            ti.maxTextureSize = 2048;
        }

        void OnPostprocessMaterial(Material m)
        {
            if (!assetPath.StartsWith(Root)) return;
            string n = m.name;
            Shader s = null;
            if (n == "M_Env_VertexColor") s = Shader.Find("BlossomRally/VertexColorWind");
            else if (n == "M_Env_LanternGlow") s = Shader.Find("BlossomRally/VertexColorGlow");
            else if (n == "M_Glass_Tint") s = Shader.Find("BlossomRally/Glass");
            if (s != null) m.shader = s;
            if (n.StartsWith("M_Light_"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", m.color * 1.4f);
            }
        }
    }
}
