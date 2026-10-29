using System;
using System.Threading.Tasks;
using GorillaNetworking;
using HarmonyLib;
using PlayFab;
using PlayFab.CloudScriptModels;

namespace EasyJoin
{
    [HarmonyPatch(typeof(GorillaComputer), "ProcessRoomState", typeof(GorillaKeyboardBindings))]
    internal static class RoomInputPatch
    {
        [HarmonyPrefix, HarmonyPriority(Priority.Last)]
        internal static bool Prefix(GorillaComputer __instance, ref GorillaKeyboardBindings __0, bool __runOriginal)
        {
            var current = JoinController.Current;
            return !__runOriginal || current == null || current.BeforeRoomInput(__instance, ref __0);
        }

        [HarmonyFinalizer]
        internal static void Finalizer(GorillaComputer __instance)
        {
            JoinController.Current?.ClearPreparation(__instance);
        }
    }

    [HarmonyPatch(typeof(GorillaServer), "CheckForBadName")]
    internal static class RoomNameRequestPatch
    {
        [HarmonyPrefix, HarmonyPriority(Priority.Last)]
        internal static void Prefix(CheckForBadNameRequest __0,
            ref Action<ExecuteFunctionResult> __1, ref Action<PlayFabError> __2,
            bool __runOriginal, out NameRequest __state)
        {
            __state = null;
            var current = JoinController.Current;
            if (!__runOriginal || current == null || __0 == null || !__0.forRoom) return;
            __state = current.BeginNameCheck(__0.name, __1, __2);
            __1 = __state.Succeed;
            __2 = __state.Fail;
        }

        [HarmonyFinalizer]
        internal static void Finalizer(NameRequest __state, Exception __exception, bool __runOriginal)
        {
            // A later prefix can suppress the request after our prefix wrapped it.
            // No callback will arrive in that case, so release this exact request.
            // PatchLifecycle removes each target's patches atomically for HarmonyX.
            if (!__runOriginal || __exception != null) __state?.Cancel();
        }
    }

    [HarmonyPatch(typeof(PhotonNetworkController), "AttemptToJoinSpecificRoomAsync")]
    internal static class NativeJoinPatch
    {
        [HarmonyPostfix]
        internal static void Postfix(Task __result)
        {
            var current = JoinController.Current;
            if (current == null) return;
            try { current.Tracker.Track(__result); }
            catch (Exception ex) { current.Report(ex); }
        }
    }
}
