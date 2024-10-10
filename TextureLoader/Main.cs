global using static TextureLoader.Logger;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ModLoader.Framework;
using ModLoader.Framework.Attributes;
using UnityEngine;
using UnityEngine.Networking;
using VTOLAPI;

namespace TextureLoader;

[ItemId("NotPolar.LegacyTextureLoader")]
public class TextureManager : VtolMod
{
    private static string ModFolder;
    private static string TexFolder;
    private static string GameFolder;

    private void Awake()
    {
        ModFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        GameFolder = Directory.GetCurrentDirectory();
        TexFolder = Path.Combine(GameFolder, "@Mod Loader", "LegacyTextureLoader");

        Log($"ModFolder: {ModFolder}");
        Log($"GameFolder: {GameFolder}");
        Log($"TexFolder: {TexFolder}");

        if (!Directory.Exists(TexFolder))
        {
            Log("Creating directory: " + TexFolder);
            Directory.CreateDirectory(TexFolder);
        }

        var placeholderFilePath = Path.Combine(TexFolder, "PLACE TEXTURE MODS HERE");
        Log($"Placeholder file path: {placeholderFilePath}");

        if (!File.Exists(placeholderFilePath))
        {
            Log("Creating placeholder file: " + placeholderFilePath);
            File.Create(placeholderFilePath).Dispose();
        }
        else
        {
            Log("Placeholder file already exists: " + placeholderFilePath);
        }

        VTAPI.SceneLoaded += SceneLoaded;
        Log($"Awake at {ModFolder}");
        //FindSkins(TexFolder);
    }

    public override void UnLoad()
    {
        // Destroy any objects
        VTAPI.SceneLoaded -= SceneLoaded;
    }

    /// <summary>
    ///     All the materials in the game
    /// </summary>
    private List<Mat> materials;

    /// <summary>
    ///     The default textures so we can revert back
    /// </summary>
    private Dictionary<string, Texture> defaultTextures;

    private readonly string[] matsNotToTouch =
    {
        "Font Material", "Font Material_0", "Font Material_1", "Font Material_2", "Font Material_3", "Font Material_4",
        "Font Material_5", "Font Material_6"
    };

    private struct Mat
    {
        public readonly string name;
        public readonly Material material;

        public Mat(string name, Material material)
        {
            this.name = name;
            this.material = material;
        }
    }

    private IEnumerator GetDefaultTextures()
    {
        yield return new WaitForSeconds(0.5f);
        Log("Getting Default Textures");
        var materials = Resources.FindObjectsOfTypeAll(typeof(Material)) as Material[];
        defaultTextures = new Dictionary<string, Texture>(materials.Length);

        for (var i = 0; i < materials.Length; i++)
            if (!matsNotToTouch.Contains(materials[i].name) && !defaultTextures.ContainsKey(materials[i].name))
                defaultTextures.Add(materials[i].name, materials[i].GetTexture("_MainTex"));

        Log($"Got {materials.Length} default textures stored");
        FindMaterials(materials);

        //The reason for apply a skin is that, in case we are in the game scene
        //and a material wasn't loaded into the resources in the vehicle config room
        //This will retry to apply it again after finding the list
        Apply();
    }

    private void SceneLoaded(VTScenes scene)
    {
        if (scene == VTScenes.VehicleConfiguration)
        {
            //Vehicle Configuration Room
            Log("Started Skins Vehicle Config room");
            StartCoroutine(GetDefaultTextures());
        }

        switch (scene)
        {
            case VTScenes.MeshTerrain:
            case VTScenes.OpenWater:
            case VTScenes.Akutan:
            case VTScenes.CustomMapBase:
            case VTScenes.CustomMapBase_OverCloud:
                StartCoroutine(GetDefaultTextures());
                break;
        }
    }

    private void FindMaterials(Material[] mats)
    {
        if (mats == null)
            mats = Resources.FindObjectsOfTypeAll<Material>();
        materials = new List<Mat>(mats.Length);

        //We now add every texture into the dictionary which gives more things to change for the skin creators
        for (var i = 0; i < mats.Length; i++) materials.Add(new Mat(mats[i].name, mats[i]));
    }

    public void RevertTextures()
    {
        Log("Reverting Textures");
        for (var i = 0; i < materials.Count; i++)
            if (defaultTextures.ContainsKey(materials[i].name))
                materials[i].material.SetTexture("_MainTex", defaultTextures[materials[i].name]);
            else
                LogError($"Tried to get material {materials[i].name} but it wasn't in the default dictionary");
    }

    private void Apply()
    {
        foreach (var subfolder in Directory.GetDirectories(TexFolder))
            for (var i = 0; i < materials.Count; i++)
            {
                //Log("Checking for matching texture for " + materials[i].name + " in subfolder " + subfolder);
                var texturePath = Path.Combine(subfolder, materials[i].name + ".png");
                if (File.Exists(texturePath))
                {
                    Log("Found matching texture for " + materials[i].name + " in subfolder " + subfolder);
                    StartCoroutine(UpdateTexture(texturePath, materials[i].material));
                    continue;
                }

                if (materials[i].name.Equals("mat_afighterExt2_livery"))
                {
                    texturePath = Path.Combine(subfolder, "mat_aFighterExt2.png");
                    if (File.Exists(texturePath))
                        StartCoroutine(UpdateTexture(texturePath, materials[i].material));
                    else
                        Log("No matching texture for " + materials[i].name + " in subfolder " + subfolder);
                }
                else
                {
                    Log("No matching texture for " + materials[i].name + " in subfolder " + subfolder);
                }
            }
    }

    private IEnumerator UpdateTexture(string path, Material material)
    {
        Log("Updating Texture from path: " + path);
        if (material == null)
            LogError("Material was null, not updating texture");
        else
            using (var uwr = UnityWebRequestTexture.GetTexture("file:///" + path))
            {
                yield return uwr.SendWebRequest();

                if (uwr.result != UnityWebRequest.Result.Success)
                {
                    LogError("Failed to load texture: " + uwr.error);
                }
                else
                {
                    Texture texture = DownloadHandlerTexture.GetContent(uwr);
                    material.SetTexture("_MainTex", texture);
                    Log($"Set Material for {material.name} to texture located at {path}");
                }
            }
    }
}