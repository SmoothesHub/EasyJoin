using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace EasyJoin
{
    internal static class PatchLifecycle
    {
        internal static void Remove(Harmony harmony)
        {
            if (harmony == null) return;
            // HarmonyX UnpatchSelf recompiles after removing individual patch methods.
            // Remove this owner's prefix/postfix/finalizer together per target, so
            // a finalizer never temporarily loses its __runOriginal state provider.
            var targets = new List<MethodBase>(harmony.GetPatchedMethods());
            foreach (var target in targets)
                harmony.Unpatch(target, HarmonyPatchType.All, harmony.Id);
        }
    }
}
