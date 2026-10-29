using System;
using System.Diagnostics;
using System.IO;
using BepInEx;
using GorillaNetworking;
using HarmonyLib;

namespace EasyJoin
{
    [BepInPlugin(Id, "EasyJoin", "1.0.1")]
    [BepInProcess("Gorilla Tag.exe")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id = "local.easyjoin.gorillatag";
        internal const string SupportedGameMvid = "b72270f9-3b78-4f33-9a7b-d143ff810ca5";
        private Harmony harmony;
        private JoinController controller;

        private void Awake()
        {
            var configuration = new RoomConfiguration(Path.Combine(Paths.ConfigPath, RoomConfiguration.FileName), Warn);
            if (typeof(GorillaComputer).Module.ModuleVersionId.ToString() != SupportedGameMvid)
            {
                Logger.LogError("EasyJoin 1.0.1 supports Steam build 26061329 only. This game assembly differs; patches were not installed.");
                return;
            }
            controller = new JoinController(configuration, Clock, Warn);
        }

        private void OnEnable()
        {
            if (controller == null) return;
            controller.Enabled = true;
            if (harmony != null) return;
            try
            {
                JoinController.Current = controller;
                harmony = new Harmony(Id);
                harmony.PatchAll(typeof(Plugin).Assembly);
            }
            catch (Exception ex)
            {
                Logger.LogError("EasyJoin could not install its patches: " + ex);
                RemovePatches();
            }
        }

        private void OnDisable()
        {
            // Continue observing existing requests while disabled so re-enabling cannot
            // forget a pending name check or an asynchronous join.
            if (controller != null) controller.Enabled = false;
        }

        private void RemovePatches()
        {
            try { PatchLifecycle.Remove(harmony); }
            catch (Exception ex) { Logger.LogError("EasyJoin could not remove its patches: " + ex); }
            finally
            {
                harmony = null;
                if (Object.ReferenceEquals(JoinController.Current, controller)) JoinController.Current = null;
            }
        }

        private void OnDestroy() { OnDisable(); RemovePatches(); }
        private void Warn(string message) { Logger.LogWarning(message); }
        private static double Clock() { return Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency; }
    }
}
