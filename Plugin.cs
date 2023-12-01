using BaboonAPI.Hooks.Initializer;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.IO;
using TootTallyCore.Graphics.Animations;
using TootTallyCore.Utils.TootTallyModules;
using TootTallySettings;
using UnityEngine;

namespace TootTallyWickedCursorMovement
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("TootTallySettings", BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin, ITootTallyModule
    {
        public static Plugin Instance;

        private const string CONFIG_NAME = "WickedCursorMovement.cfg";
        private const string CONFIG_FIELD = "WickedCursorMovement";

        private const float DEFAULT_FREQ = 3.5f;
        private const float DEFAULT_DAMP = 0.12f;
        private const float DEFAULT_INIT = 1.5f;
        private Harmony _harmony;
        public ConfigEntry<bool> ModuleConfigEnabled { get; set; }
        public bool IsConfigInitialized { get; set; }

        //Change this name to whatever you want
        public string Name { get => "WickedCursorMovement"; set => Name = value; }

        public static TootTallySettingPage settingPage;

        public static void LogInfo(string msg) => Instance.Logger.LogInfo(msg);
        public static void LogError(string msg) => Instance.Logger.LogError(msg);

        private void Awake()
        {
            if (Instance != null) return;
            Instance = this;
            _harmony = new Harmony(Info.Metadata.GUID);

            GameInitializationEvent.Register(Info, TryInitialize);
        }

        private void TryInitialize()
        {
            // Bind to the TTModules Config for TootTally
            ModuleConfigEnabled = TootTallyCore.Plugin.Instance.Config.Bind("Modules", "Wicked Cursor Movement", true, "For the people that are getting too good at the game");
            TootTallyModuleManager.AddModule(this);
            TootTallySettings.Plugin.Instance.AddModuleToSettingPage(this);
        }

        public void LoadModule()
        {
            string configPath = Path.Combine(Paths.BepInExRootPath, "config/");
            ConfigFile config = new ConfigFile(configPath + CONFIG_NAME, true) { SaveOnConfigSet = true };
            Frequency = config.Bind(CONFIG_FIELD, "Frequency", DEFAULT_FREQ, "The strenght of the vibration (Higher vibrates more).");
            Damping = config.Bind(CONFIG_FIELD, "Damping", DEFAULT_DAMP, "How fast the cursor settle at the original target.\n(0 will vibrate forever, 100 will not vibrate).");
            InitialResponse = config.Bind(CONFIG_FIELD, "InitialResponse", DEFAULT_INIT, "How much it anticipates the motion.\n(value higher than one will take time to accelerate, value lower than 0 will ancitipate the motion).");

            settingPage = TootTallySettingsManager.AddNewPage("WickedCursor\nMovementV2", "Wicked Cursor", 40f, new Color(0, 0, 0, 0));
            settingPage?.AddSlider("Frequency", .01f, 10f, Frequency, false);
            settingPage?.AddSlider("Damping", 0f, 10f, Damping, false);
            settingPage?.AddSlider("Initial Response", -10f, 10f, InitialResponse, false);
            settingPage.AddLabel($"Some good default values are:\nFreq: {DEFAULT_FREQ}\nDamp: {DEFAULT_DAMP}\nInit: {DEFAULT_INIT}");
            settingPage.AddLabel("To turn off the effect, go to the TootTally Module's page and turn off the module.");

            TootTallySettings.Plugin.TryAddThunderstoreIconToPageButton(Instance.Info.Location, Name, settingPage);

            _harmony.PatchAll(typeof(WickedCursorPatches));
            LogInfo($"Module loaded!");
        }

        public void UnloadModule()
        {
            _harmony.UnpatchSelf();
            settingPage.Remove();
            LogInfo($"Module unloaded!");
        }

        public static class WickedCursorPatches
        {
            public static SecondDegreeDynamicsAnimation cursorDynamics;
            public static Vector2 cursorPosition;
            public static Vector2 cursorDestination;

            [HarmonyPatch(typeof(GameController), nameof(GameController.Start))]
            [HarmonyPostfix]
            public static void GameControllerStartPostfixPatch(GameController __instance)
            {
                cursorDynamics = new SecondDegreeDynamicsAnimation(Instance.Frequency.Value, Instance.Damping.Value, Instance.InitialResponse.Value);

                cursorPosition = __instance.pointer.transform.localPosition;
                cursorDynamics.SetStartVector(cursorPosition);
            }

            [HarmonyPatch(typeof(GameController), nameof(GameController.Update))]
            [HarmonyPrefix]
            public static void GameControllerUpdatePostfixPatch(GameController __instance)
            {
                cursorDestination = Input.mousePosition / 2.42f;
                cursorDestination.y -= 440f / 2f;

                if (cursorDynamics != null && cursorPosition != cursorDestination)
                    cursorPosition.y = cursorDynamics.GetNewVector(cursorDestination, Time.deltaTime).y;
                __instance.pointer.transform.localPosition = cursorPosition;
            }
        }

        public ConfigEntry<float> Frequency { get; set; }
        public ConfigEntry<float> Damping { get; set; }
        public ConfigEntry<float> InitialResponse { get; set; }
    }
}