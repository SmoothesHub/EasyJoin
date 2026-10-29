using System;
using System.Collections.Generic;
using GorillaNetworking;
using GorillaTagScripts;
using PlayFab;
using PlayFab.CloudScriptModels;

namespace EasyJoin
{
    internal sealed class JoinController
    {
        internal static JoinController Current;
        private readonly RoomConfiguration configuration;
        private readonly Func<double> now;
        private readonly Action<string> warn;
        private readonly JoinTracker tracker = new JoinTracker();
        private readonly List<NameRequest> nameChecks = new List<NameRequest>(1);
        private GorillaComputer preparedOwner;
        private string preparedCode;
        private double nextPress;
        private bool reportedError;
        internal bool Enabled = true;

        internal JoinController(RoomConfiguration configuration, Func<double> now, Action<string> warn)
        {
            this.configuration = configuration;
            this.now = now;
            this.warn = warn;
        }

        internal JoinTracker Tracker { get { return tracker; } }

        internal bool BeforeRoomInput(GorillaComputer computer, ref GorillaKeyboardBindings button)
        {
            // This prefix is attached to ProcessRoomState, not the global keyboard dispatcher.
            if (!Enabled || button != GorillaKeyboardBindings.option3 || computer == null) return true;
            try
            {
                if (!computer.initialized || computer.currentState != GorillaComputer.ComputerState.Room)
                    return true;
                if (computer.limitOnlineScreens) return true;
                if (!KIDManager.HasPermissionToUseFeature(EKIDFeatures.Groups) ||
                    !KIDManager.HasPermissionToUseFeature(EKIDFeatures.Multiplayer)) return true;

                var network = NetworkSystem.Instance;
                var controller = computer.networkController;
                if (network == null || controller == null || controller.privateTrigger == null ||
                    GorillaServer.Instance == null || FriendshipGroupDetection.Instance == null)
                    return false;

                // Unity's null comparison also detects destroyed/recreated computer objects.
                // attemptingToConnect is a legacy latch: this build sets it during
                // DisconnectCleanup and never resets it. Only current state and live
                // requests determine whether another join can start.
                if (HasPendingNameCheck() || tracker.IsBusy ||
                    network.groupJoinInProgress ||
                    (network.netState != NetSystemState.Idle && network.netState != NetSystemState.InGame))
                    return false;

                double time = now();
                if (time < nextPress) return false;
                nextPress = time + 1.0;
                if (!configuration.Reload()) return false;

                string code = configuration.CachedCode;
                if (computer.IsPlayerInVirtualStump())
                {
                    string prefix = computer.VStumpRoomFullPrepend;
                    if (String.IsNullOrEmpty(prefix) || prefix.Length + code.Length > 10) return false;
                    code = prefix + code;
                }
                if (network.InRoom && String.Equals(network.RoomName, code, StringComparison.OrdinalIgnoreCase))
                    return false;

                // Do not call Photon directly: the untouched native Enter branch performs the
                // server name check, permissions, party handling, loading screen and joining.
                computer.roomToJoin = code;
                preparedOwner = computer;
                preparedCode = code;
                button = GorillaKeyboardBindings.enter;
                return true;
            }
            catch (Exception ex)
            {
                Report(ex);
                return false;
            }
        }

        internal NameRequest BeginNameCheck(string code, Action<ExecuteFunctionResult> success, Action<PlayFabError> error)
        {
            HasPendingNameCheck();
            var owner = success?.Target as GorillaComputer;
            bool pinCode = preparedOwner != null && code == preparedCode &&
                (owner == null || Object.ReferenceEquals(owner, preparedOwner));
            if (pinCode) owner = preparedOwner;
            var request = new NameRequest(this, owner, code, pinCode, success, error);
            nameChecks.Add(request);
            return request;
        }

        internal void ClearPreparation(GorillaComputer computer)
        {
            if (!Object.ReferenceEquals(preparedOwner, computer)) return;
            preparedOwner = null;
            preparedCode = null;
        }

        private bool HasPendingNameCheck()
        {
            for (int i = nameChecks.Count - 1; i >= 0; --i)
                if (nameChecks[i].IsObsolete) nameChecks.RemoveAt(i);
            return nameChecks.Count != 0;
        }

        internal void FinishNameCheck(NameRequest request) { nameChecks.Remove(request); }

        internal void Report(Exception ex)
        {
            if (reportedError) return;
            reportedError = true;
            warn("EasyJoin could not handle an input (" + ex.GetType().Name + "). See the supported-build notes.");
        }
    }
}
