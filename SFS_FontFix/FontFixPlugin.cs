using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TextCore.LowLevel;

namespace SFS_FontFix
{
    [BepInPlugin("com.sfs.fontfix", "SFS Font Fix", "4.1.0")]
    public class FontFixPlugin : BaseUnityPlugin
    {
        public static FontFixPlugin Instance;
        public static ManualLogSource Log => Instance?.Logger;

        private Font chineseUnityFont;
        private string chineseFontFilePath;
        private TMP_FontAsset chineseTMPFont;
        private bool isInitialized;
        private Font originalNormalFont;

        private void Awake()
        {
            Instance = this;
            Logger.LogInfo("SFS Font Fix v4.1.0 loaded");
            new Harmony("com.sfs.fontfix").PatchAll();
        }

        private void LateUpdate()
        {
            if (!isInitialized && SFS.Translations.TranslationManager.main != null)
                LoadAndReplaceFont();

            if (isInitialized && chineseTMPFont != null)
                ApplyTMPFontIfNeeded();
        }

        private void LoadAndReplaceFont()
        {
            if (isInitialized) return;

            var manager = SFS.Translations.TranslationManager.main;
            if (manager?.fonts == null || manager.fonts.Count == 0) return;

            string fontPath = FindFontFile();
            if (fontPath == null)
            {
                Logger.LogWarning("Font not found. Place .ttf/.otf in BepInEx/plugins/");
                return;
            }

            chineseUnityFont = new Font(fontPath);
            chineseFontFilePath = fontPath;
            Logger.LogInfo("Font loaded: " + chineseUnityFont.name);

            CreateTMPFont();
            ReplaceNormalFont(manager);

            isInitialized = true;
            Logger.LogInfo("Font replacement complete");
        }

        private string FindFontFile()
        {
            string pluginPath = Path.GetDirectoryName(typeof(FontFixPlugin).Assembly.Location);
            foreach (var name in new[] { "NotoSansSC-Bold.ttf", "NotoSansSC-Bold.otf", "NotoSansSC.ttf", "NotoSansSC.otf", "Font.ttf", "Font.otf" })
            {
                string path = Path.Combine(pluginPath, name);
                if (File.Exists(path)) return path;
            }
            return null;
        }

        private void CreateTMPFont()
        {
            if (chineseUnityFont == null) return;

            try
            {
                chineseTMPFont = TMP_FontAsset.CreateFontAsset(
                    chineseUnityFont, 90, 9, GlyphRenderMode.SDFAA, 4096, 4096,
                    AtlasPopulationMode.Dynamic, true);

                if (chineseTMPFont == null)
                {
                    Logger.LogError("CreateFontAsset returned null");
                    return;
                }

                chineseTMPFont.name = "NotoSansSC SDF";
                chineseTMPFont.fallbackFontAssetTable = new List<TMP_FontAsset>();
                Logger.LogInfo("TMP font asset created");
            }
            catch (Exception e)
            {
                Logger.LogError("Failed to create TMP font: " + e);
            }
        }

        private void ReplaceNormalFont(SFS.Translations.TranslationManager manager)
        {
            var fonts = manager.fonts;
            for (int i = 0; i < fonts.Count; i++)
            {
                if (fonts[i] != null && fonts[i].name.ToLower() == "normal")
                {
                    originalNormalFont = fonts[i];
                    fonts[i] = chineseUnityFont;
                    if (manager.currentFont == originalNormalFont)
                        manager.currentFont = chineseUnityFont;
                    Logger.LogInfo("Replaced normal font at index " + i);
                    return;
                }
            }

            if (fonts.Count > 0 && fonts[0] != null)
            {
                originalNormalFont = fonts[0];
                fonts[0] = chineseUnityFont;
                Logger.LogInfo("Replaced first font as fallback");
            }
        }

        private void ApplyTMPFontIfNeeded()
        {
            foreach (var tmp in FindObjectsOfType<TMP_Text>())
            {
                if (tmp.font != chineseTMPFont)
                {
                    tmp.font = chineseTMPFont;
                    tmp.enableAutoSizing = true;
                }
            }
        }

        public void RefreshFonts()
        {
            if (!isInitialized) return;
            var manager = SFS.Translations.TranslationManager.main;
            if (manager != null) ReplaceNormalFont(manager);
            ApplyTMPFontIfNeeded();
        }

        public static FontFixPlugin GetInstance() => Instance;
        public static bool IsReady() => Instance != null && Instance.isInitialized;
        public static TMP_FontAsset GetChineseTMPFont() => Instance?.chineseTMPFont;
        public static Font GetChineseUnityFont() => Instance?.chineseUnityFont;
    }

    [HarmonyPatch(typeof(SFS.Translations.TranslationManager), "SetLanguage")]
    public class SetLanguagePatch
    {
        [HarmonyPostfix]
        static void Postfix(SFS.Translations.TranslationManager __instance)
        {
            if (FontFixPlugin.GetChineseUnityFont() != null && __instance.currentFont != FontFixPlugin.GetChineseUnityFont())
                __instance.currentFont = FontFixPlugin.GetChineseUnityFont();
        }
    }

    [HarmonyPatch(typeof(SFS.Translations.FontSetter), "SetFont")]
    public class FontSetter_TMP_Patch
    {
        [HarmonyPostfix]
        static void Postfix(Font font, SFS.Translations.FontSetter __instance)
        {
            if (!FontFixPlugin.IsReady()) return;

            var tmp = __instance.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.font = FontFixPlugin.GetChineseTMPFont();
                tmp.enableAutoSizing = true;
            }
        }
    }
}
