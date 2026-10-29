using System;
using System.Threading;
using GorillaNetworking;
using PlayFab;
using PlayFab.CloudScriptModels;

namespace EasyJoin
{
    // One lease per actual room-name request. Player/troop-name errors cannot finish it.
    internal sealed class NameRequest
    {
        private readonly JoinController controller;
        private readonly GorillaComputer owner;
        private readonly bool hasOwner;
        private readonly string code;
        private readonly bool pinCode;
        private readonly Action<ExecuteFunctionResult> success;
        private readonly Action<PlayFabError> error;
        // 0 = pending, 1 = executing the original callback, 2 = finished/canceled.
        private int state;

        internal NameRequest(JoinController controller, GorillaComputer owner, string code, bool pinCode,
            Action<ExecuteFunctionResult> success, Action<PlayFabError> error)
        {
            this.controller = controller;
            this.owner = owner;
            hasOwner = !Object.ReferenceEquals(owner, null);
            this.code = code;
            this.pinCode = pinCode;
            this.success = success;
            this.error = error;
        }

        internal bool IsObsolete { get { return Volatile.Read(ref state) == 2 || (hasOwner && owner == null); } }

        internal void Succeed(ExecuteFunctionResult result)
        {
            if (Interlocked.CompareExchange(ref state, 1, 0) != 0) return;
            string editedText = null;
            bool restore = false;
            try
            {
                if (pinCode)
                {
                    if (owner == null) return;
                    editedText = owner.roomToJoin;
                    restore = editedText != code;
                    // The native callback reads this mutable field. Use the very code
                    // the server just checked, even if the player edited it meanwhile.
                    owner.roomToJoin = code;
                }
                success?.Invoke(result);
            }
            finally
            {
                try
                {
                    // Preserve edits, but do not undo a native warning clearing the field.
                    if (restore && owner != null && owner.roomToJoin == code) owner.roomToJoin = editedText;
                }
                finally { Complete(); }
            }
        }

        internal void Fail(PlayFabError result)
        {
            if (Interlocked.CompareExchange(ref state, 1, 0) != 0) return;
            try { if (!pinCode || owner != null) error?.Invoke(result); }
            finally { Complete(); }
        }

        internal void Cancel()
        {
            if (Interlocked.CompareExchange(ref state, 2, 0) != 0) return;
            controller.FinishNameCheck(this);
        }

        private void Complete()
        {
            Volatile.Write(ref state, 2);
            controller.FinishNameCheck(this);
        }
    }
}
