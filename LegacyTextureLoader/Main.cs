global using static LegacyTextureLoader.Logger;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ModLoader.Framework;
using ModLoader.Framework.Attributes;
using UnityEngine;
using UnityEngine.Networking;
using VTOLAPI;

namespace LegacyTextureLoader;

[ItemId("NotPolar.LegacyTextureLoader")]
public class TextureLoader : VtolMod
{
    private static string _modFolder;
    private static string _workshopFolder;
    private static string _gameFolder;
    private static string _texFolder;

    private void Awake()
    {
        _modFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        _workshopFolder = Directory.GetParent(_modFolder).FullName;
        _gameFolder = Directory.GetCurrentDirectory();
        _texFolder = Path.Combine(_gameFolder, "@Mod Loader", "LegacyTextureLoader");

        Log($"ModFolder: {_modFolder}");
        Log($"WorkshopFolder: {_workshopFolder}");
        Log($"GameFolder: {_gameFolder}");
        Log($"TexFolder: {_texFolder}");

        if (!Directory.Exists(_texFolder))
        {
            Log("Creating directory: " + _texFolder);
            Directory.CreateDirectory(_texFolder);
        }

        var placeholderFilePath = Path.Combine(_texFolder, "PLACE TEXTURE MODS HERE");
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
        Log($"Awake at {_modFolder}");
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
    private List<Mat> _materials;

    /// <summary>
    ///     The default textures so we can revert back
    /// </summary>
    private Dictionary<string, Texture> _defaultTextures;

    private readonly string[] _matsNotToTouch = 
    {
        "Font Material", 
        "Font Material_0", 
        "Font Material_1", 
        "Font Material_2", 
        "Font Material_3", 
        "Font Material_4", 
        "Font Material_5", 
        "Font Material_6"
    };

    private struct Mat
    {
        public readonly string Name;
        public readonly Material Material;

        public Mat(string name, Material material)
        {
            this.Name = name;
            this.Material = material;
        }
    }

    private IEnumerator GetDefaultTextures()
    {
        yield return new WaitForSeconds(0.5f);
        Log("Getting Default Textures");
        var materials = Resources.FindObjectsOfTypeAll(typeof(Material)) as Material[];
        if (materials != null)
        {
            _defaultTextures = new Dictionary<string, Texture>(materials.Length);
            foreach (var material in materials)
            {
                if (!_matsNotToTouch.Contains(material.name) && !_defaultTextures.ContainsKey(material.name))
                {
                    _defaultTextures.Add(material.name, material.GetTexture("_MainTex"));
                }
            }
            
            Log($"Got {materials.Length} default textures stored");
            FindMaterials(materials);
        }
        else
        {
            LogError("Materials is null.");
        }
        
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
        mats ??= Resources.FindObjectsOfTypeAll<Material>();
        _materials = new List<Mat>(mats.Length);

        //We now add every texture into the dictionary which gives more things to change for the skin creators
        foreach (var mat in mats)
        {
            _materials.Add(new Mat(mat.name, mat));
        }
    }

    public void RevertTextures()
    {
        Log("Reverting Textures");
        for (var i = 0; i < _materials.Count; i++)
            if (_defaultTextures.ContainsKey(_materials[i].Name))
                _materials[i].Material.SetTexture("_MainTex", _defaultTextures[_materials[i].Name]);
            else
                LogError($"Tried to get material {_materials[i].Name} but it wasn't in the default dictionary");
    }

    private void Apply()
    {
        var textureFolders = new[] {_texFolder, _workshopFolder};
        
        foreach (var folder in textureFolders)
            foreach (var subfolder in Directory.GetDirectories(folder))
                for (var i = 0; i < _materials.Count; i++)
                {
                    //Log("Checking for matching texture for " + materials[i].name + " in subfolder " + subfolder);
                    var texturePath = Path.Combine(subfolder, _materials[i].Name + ".png");
                    if (File.Exists(texturePath))
                    {
                        Log("Found matching texture for " + _materials[i].Name + " in subfolder " + subfolder);
                        StartCoroutine(UpdateTexture(texturePath, _materials[i].Material));
                        continue;
                    }

                    if (_materials[i].Name.Equals("mat_afighterExt2_livery"))
                    {
                        texturePath = Path.Combine(subfolder, "mat_aFighterExt2.png");
                        if (File.Exists(texturePath))
                            StartCoroutine(UpdateTexture(texturePath, _materials[i].Material));
                        //else
                            //Log("No matching texture for " + _materials[i].Name + " in subfolder " + subfolder);
                    }
                    else
                    {
                        //Log("No matching texture for " + _materials[i].Name + " in subfolder " + subfolder);
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