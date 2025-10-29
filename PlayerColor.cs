using BepInEx;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.IO;
using UnityEngine.SceneManagement;
using System;
using System.Reflection;

namespace silksong_PlayerColor {
    [BepInPlugin("com.PDE26jjk.PlayerColor", "PlayerColor", "0.0.3")]
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
            atlas2MaskPath.Clear();
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
                        string fileName = Path.GetFileNameWithoutExtension(atlasPath);
                        atlas2MaskPath[dirname + "/" + fileName] = atlasPath;
                        if (Settings.LoadAllTextureAtStart.Value) {
                            var pngData = File.ReadAllBytes(atlasPath);
                            var texture = new Texture2D(2, 2);
                            if (texture.LoadImage(pngData)) {
                                atlas2Mask[dirname + "/" + fileName] = texture;
                                //Debug.Log(dirname + "/" + fileName);
                            }
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
        private static Dictionary<string, string> atlas2MaskPath = new();

        //private static Dictionary<GameObject, Material> OriginalMaterials = new();

        private static readonly Color _OriginalColor = Settings.ParseColor("#79404B");
        public class PlayerColorStruct {
            public Color Color;
            public float Saturation;
            public float Brightness;
        }

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

                    if (atlas2MaskPath.ContainsKey(atlas)) {
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
                    atlas = Tex2atlas[texId];
                    if (!atlas2Mask.ContainsKey(atlas) && atlas2MaskPath.ContainsKey(atlas)) {
                        var pngData = File.ReadAllBytes(atlas2MaskPath[atlas]);
                        var texture = new Texture2D(2, 2);
                        if (texture.LoadImage(pngData)) {
                            atlas2Mask[atlas] = texture;
                        }
                    }

                    //Debug.Log(obj.name +" : "+ Tex2atlas[texId]);
                    atlas2Mask.TryGetValue(atlas, out maskTex);
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
                if (ori_mat.HasColor(_FlashColorProp)) {
                    mat.SetColor(_FlashColorProp, ori_mat.GetColor(_FlashColorProp));
                    //mat.SetColor(_FlashColorProp, ori_mat.GetColor(_FlashColorProp));
                    mat.SetFloat(_FlashAmountProp, ori_mat.GetFloat(_FlashAmountProp));
                }
                PlayerColorStruct playerColor = GetPlayerColor(obj);
                mat.SetFloat("t1", playerColor.Brightness);
                mat.SetFloat("t2", playerColor.Saturation);
                mat.SetColor("_MaskFromColor1", _OriginalColor);
                mat.SetColor("_MaskColor1", playerColor.Color);
                //component.SetMaterials()
                if (renderer.sharedMaterial != mat) {
                    renderer.sharedMaterial = mat;
                }
            }
            static Type? _HeroProxyType;
            static MethodInfo? _GetClientPlayerColorStructInfo;
            static bool hasOnlineModule = true;
            static PlayerColorStruct _DefaultPlayerColorStruct = new();
            private static PlayerColorStruct GetPlayerColor(GameObject obj) {
                if (obj.name.StartsWith("HeroProxySprite_") && hasOnlineModule) {
                    ulong clientID = ulong.Parse(obj.name.Split('_')[1]);
                    if (_HeroProxyType == null) {
                        try {
                            _HeroProxyType = Type.GetType("silksong_OnlineFighting.HeroProxy, silksong_OnlineFighting");
                            _GetClientPlayerColorStructInfo = _HeroProxyType.GetMethod("GetClientPlayerColorStruct", BindingFlags.Public | BindingFlags.Static);
                        }
                        catch (Exception) {
                            hasOnlineModule = false;
                            Debug.Log("Online module not installed.");
                        }
                    }
                    if (_GetClientPlayerColorStructInfo != null) {
                        var playerColor = _GetClientPlayerColorStructInfo.Invoke(null, new object[] { clientID });
                        unsafe {
                            return *(PlayerColorStruct*)&playerColor;
                        }
                    }


                }
                _DefaultPlayerColorStruct.Color = Settings.Color2.Value;
                _DefaultPlayerColorStruct.Brightness = Settings.Brightness.Value;
                _DefaultPlayerColorStruct.Saturation = Settings.Saturation.Value;
                return _DefaultPlayerColorStruct;

            }
        }
    }
}
