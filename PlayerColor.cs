using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using HutongGames.PlayMaker.Actions;
using System.Collections;
using InControl;
using UnityEngine.Rendering;
using System.IO;
using UnityEngine.UIElements;
using Mono.Unix.Native;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;

namespace silksong_PlayerColor {
    [BepInPlugin("com.PDE26jjk.PlayerColor", "PlayerColor", "0.0.2")]
    public class PlayerColor : BaseUnityPlugin {
        private Harmony? _harmony;
        private static Shader PlayerColorShader;
        private static Dictionary<int, string> Tex2atlas = new();
        private static Dictionary<string, Texture> atlas2Mask = new();
        public static GameManager gm => _gm != null ? _gm : (_gm = GameManager.instance);
        public static HeroController hero => gm != null ? gm.hero_ctrl : null;
        private static GameManager _gm;
        private bool hasInited = false;
        private static bool MaskTexureInited;
        //private bool found = false;
        private void Awake() {
#if DEBUG
            Logger.LogInfo("PlayerColor Initialized!--------------------");
#endif
            var modPath = Path.Combine(BepInEx.Paths.PluginPath, "PlayerColor");

            var assetBundle = AssetBundle.LoadFromFile(Path.Combine(modPath, "ssmod"));
            var shader = assetBundle.LoadAsset<Shader>("playercolor.shader");
            PlayerColorShader = shader;
            Logger.LogInfo(shader is null);
            //string[] assetNames = assetBundle.GetAllAssetNames();
            //foreach (string name in assetNames) {
            //    Debug.Log($"AssetBundle 中的资源: {name}");
            //}
            assetBundle.Unload(false);
            _harmony = new Harmony("com.PDE26jjk.PlayerColor");
            _harmony.PatchAll(typeof(HeroSprite_Pather));
            Invoke(nameof(WaitForGameManager), 0.5f);
            SceneManager.sceneLoaded += this.OnSceneLoaded;
            //SpriteAtlasManager.atlasRegistered += (SpriteAtlas t) => {
            //    Sprite[] sprites = { };
            //    t.GetSprites(sprites);
            //    foreach (var item in sprites) {
            //        if (item.texture.name.StartsWith("sactx-2-1024x2048-BC7-Beast_Slas")) {
            //            Debug.LogError("sactx-2-1024x2048-BC7-Beast_Slas: " + item.texture.name + ": " + item.name);
            //        }
            //    }
            //};

        }
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
            if (!hasInited) {
                string[] menuStr = { "menu", "mainmenu", "title" };
                string sceneName = scene.name.ToLower();
                //Debug.LogError("OnSceneLoaded-------------" + sceneName);
                foreach (var item in menuStr) {
                    if (sceneName.Contains(item)) { return; }
                }
                hasInited = true;
                MaskTexureInited = false;
                Invoke(nameof(InitMaskTexture), 0.1f);
            }

        }
        private void OnDisable() {
            _harmony?.UnpatchSelf();
            SceneManager.sceneLoaded -= this.OnSceneLoaded;
        }
        private void WaitForGameManager() {
            if (GameManager.SilentInstance) {
                GetSettings();
            }
            else {
                Invoke(nameof(WaitForGameManager), 0.5f);
            }
        }
        private void GetSettings() {
            string lang; // DE, EN, ES, FR, IT, JA, KO, PT, RU, ZH
            TeamCherry.Localization.LocalizationProjectSettings.TryGetSavedLanguageCode(out lang);
            Settings.Initialize(this.Config, lang);
        }
        void Update() {
#if DEBUG
            if (Input.GetKeyDown(KeyCode.Alpha2)) {
                //InitMaskTexture();
            }
            if (Input.GetKeyDown(KeyCode.Alpha1)) {
               }

#endif
        }
        private static void cacheSprite(tk2dSprite sprite) {
            var tex = sprite.CurrentSprite?.material?.mainTexture;
            if (!tex) return;
            var texId = tex.GetInstanceID();
            if (Tex2atlas.ContainsKey(texId)) return;
            string collName = "Texture2D";
            if (sprite.Collection) {
                collName = sprite.Collection.name;
            }
            Tex2atlas[texId] = collName + "/" + tex.name;
        }

        private void InitMaskTexture() {
            Tex2atlas.Clear();
            atlas2Mask.Clear();
            var maskPath = Path.Combine(Application.dataPath, "Mods", "Mask");
            if (maskPath != null) {
                foreach (string dir in Directory.GetDirectories(maskPath)) {
                    var dirname = Path.GetFileName(dir);
                    //if (!collectionNames.Contains(dirname)) {
                    //    //Debug.Log("collectionNames.Contains" + dirname);
                    //    continue;
                    //}
                    foreach (string atlasPath in Directory.EnumerateFiles(dir, "*.png", SearchOption.TopDirectoryOnly)) {

                        if (!File.Exists(atlasPath)) continue;

                        var pngData = File.ReadAllBytes(atlasPath);
                        var texture = new Texture2D(2, 2);
                        if (texture.LoadImage(pngData)) {
                            string fileName = Path.GetFileNameWithoutExtension(atlasPath);
                            atlas2Mask[dirname + "/" + fileName] = texture;
                            //Debug.Log(dirname + "/" + fileName);
                        }
                    }
                }
            MaskTexureInited = true;
            }
        }

        private static Texture2D CreateSinglePixelBlackTexture() {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.black);
            texture.Apply();
            return texture;
        }
        private static Texture2D DummyMask;
        private static bool UseOverrideMaterial => Settings.UseOverrideMaterial.Value;
        private static readonly int _mainTexProp = Shader.PropertyToID("_MainTex");
        private static readonly int _MaskTexProp = Shader.PropertyToID("_MaskTex");
        private static readonly int _FlashColorProp = Shader.PropertyToID("_FlashColor");
        private static readonly int _FlashAmountProp = Shader.PropertyToID("_FlashAmount");

        private static Dictionary<GameObject, Material> OverrideMaterials = new();
        private static HashSet<GameObject> otherObjs = new();
        //private static Dictionary<GameObject, Material> OriginalMaterials = new();
        class HeroSprite_Pather {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(tk2dSprite), "UpdateMaterial")]
            public static void ChangeMaterial(tk2dSprite __instance) {
                if (!MaskTexureInited || !UseOverrideMaterial) return;
                //Debug.Log("UpdateMaterial--------------------:" + __instance.gameObject.name + ": " + hero is null);
                if (!hero) return;
                GameObject obj = __instance.gameObject;
                if (otherObjs.Contains(obj)) return;
                bool flag = OverrideMaterials.ContainsKey(obj);
                var sprite = __instance.CurrentSprite;
                string collName = "Texture2D";
                if (__instance.Collection) {
                    collName = __instance.Collection.name;
                }
                string texName = sprite.material.mainTexture.name;
                string atlas = collName + "/" + texName;
                //if (texName.StartsWith("sactx-2-1024x2048-BC7-Beast_Slas")) {
                //    Debug.LogError("sactx-2-1024x2048-BC7-Beast_Slas: " + obj.name + ": " + atlas);
                //}

                if (!flag) {

                    if (atlas2Mask.ContainsKey(atlas)) {
                        flag = true;
                        //Debug.Log("has key--------: " + obj.name + ": " + atlas);
                    }
                    else {
                        //Debug.LogError("has not key!!!!!!!!: " + obj.name + ": " + atlas);
                    }
                }
                if (!flag) {
                    if (otherObjs.Count > 1000) {
                        otherObjs.RemoveWhere(obj => obj == null); // 清理无效对象
                        if (otherObjs.Count > 1000) otherObjs.Clear();
                    }
                    otherObjs.Add(obj);
                    return;
                }

                if (!PlayerColorShader) return;
                cacheSprite(__instance);
                Renderer renderer = __instance.GetComponent<Renderer>();
                Material ori_mat = renderer.material;
                if (OverrideMaterials.TryGetValue(obj, out Material mat)) {
                }
                else {
                    //OriginalMaterials.Add(obj, ori_mat);
                    mat = new Material(PlayerColorShader);
                    OverrideMaterials.Add(obj, mat);
                    //Debug.Log("new dict item-------------:" + obj.name + ": " + OverrideMaterials.Count);
                }
                if (OverrideMaterials.Count > 1000) {
                    var keysToRemove = OverrideMaterials.Keys.Where(key => key == null).ToList();
                    foreach (var key in keysToRemove) {
                        OverrideMaterials.Remove(key);
                    }

                    if (OverrideMaterials.Count > 1000) {
                        OverrideMaterials.Clear();
                    }
                }
                //Texture mainTex = ori_mat.GetTexture(_mainTexProp);
                Texture mainTex = sprite.material.mainTexture;
                Texture maskTex;
                int texId = mainTex.GetInstanceID();
                bool hasTexture = Tex2atlas.ContainsKey(texId);
                if (hasTexture) {
                    //Debug.Log(obj.name +" : "+ Tex2atlas[texId]);
                    atlas2Mask.TryGetValue(Tex2atlas[texId], out maskTex);
                }
                else {
                    return;
                }
                if (mainTex) {
                    //Debug.Log("mainTex.name: " + mainTex.name);
                    mat.SetTexture(_mainTexProp, mainTex);
                }
                if (maskTex != null) {
                    mat.SetTexture(_MaskTexProp, maskTex);
                }
                else {
                    //Debug.Log("No mask texture found: " + obj.name + ":" + Tex2atlas[texId]);
                    DummyMask ??= CreateSinglePixelBlackTexture();
                    mat.SetTexture(_MaskTexProp, DummyMask);
                }
                mat.SetColor(_FlashColorProp, ori_mat.GetColor(_FlashColorProp));
                //mat.SetColor(_FlashColorProp, ori_mat.GetColor(_FlashColorProp));
                mat.SetFloat(_FlashAmountProp, ori_mat.GetFloat(_FlashAmountProp));
                mat.SetFloat("t1", Settings.Brightness.Value);
                mat.SetFloat("t2", Settings.Saturation.Value);
                mat.SetColor("_MaskFromColor1", Settings.Color1.Value);
                mat.SetColor("_MaskColor1", Settings.Color2.Value);
                //component.SetMaterials()
                if (renderer.sharedMaterial != mat) {
                    renderer.sharedMaterial = mat;
                }
            }
        }
    }
}
