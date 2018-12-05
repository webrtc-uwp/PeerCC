using Org.WebRtc;
using System;
using System.Threading.Tasks;

namespace PeerConnectionClient.Stats
{
    public class PeerConnectionStateChange
    {
        private static readonly StatsController SC = StatsController.Instance;

        private static readonly System.Timers.Timer _getAllStatsTimer = new System.Timers.Timer(10000);

        private static readonly string newConnection = RTCIceConnectionState.New.ToString().ToLower();
        private static readonly string checking = RTCIceConnectionState.Checking.ToString().ToLower();
        private static readonly string connected = RTCIceConnectionState.Connected.ToString().ToLower();
        private static readonly string completed = RTCIceConnectionState.Completed.ToString().ToLower();
        private static readonly string failed = RTCIceConnectionState.Failed.ToString().ToLower();
        private static readonly string disconnected = RTCIceConnectionState.Disconnected.ToString().ToLower();
        private static readonly string closed = RTCIceConnectionState.Closed.ToString().ToLower();

        private static readonly string newGathering = RTCIceGatheringState.New.ToString().ToLower();
        private static readonly string gathering = RTCIceGatheringState.Gathering.ToString().ToLower();
        private static readonly string complete = RTCIceGatheringState.Complete.ToString().ToLower();

        #region Stats OnIceConnectionStateChange
        public static async Task StatsOnIceConnectionStateChange(RTCPeerConnection pc)
        {
            if (pc.IceConnectionState == RTCIceConnectionState.Checking)
            {
                SC.connectingTimeStart = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

                if (SC.newIceConnectionState != checking)
                {
                    await SetIceConnectionStates(checking);
                }

                SC.statsObjects.Clear();

                await WebRtcStats.GetAllStats(pc);

                if (SC.prevIceConnectionState == connected 
                    || SC.prevIceConnectionState == completed
                    || SC.prevIceConnectionState == failed 
                    || SC.prevIceConnectionState == disconnected
                    || SC.prevIceConnectionState == closed)
                {
                    await SC.callStatsClient.SendIceRestart(newConnection);
                }

                if (SC.prevIceConnectionState == disconnected)
                {
                    await SC.callStatsClient.SendIceDisruptionEnd();

                    await SC.callStatsClient.SendIceConnectionDisruptionEnd();
                }
            }

            if (pc.IceConnectionState == RTCIceConnectionState.Connected)
            {
                SC.connectingTimeStop = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                SC.totalSetupTimeStop = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

                if (SC.newIceConnectionState != connected)
                {
                    await SetIceConnectionStates(connected);
                }

                SC.statsObjects.Clear();

                await WebRtcStats.GetAllStats(pc);

                long gatheringDelayMiliseconds;
                long connectivityDelayMiliseconds;
                long totalSetupDelay;

                gatheringDelayMiliseconds = SC.gateheringTimeStop - SC.gateheringTimeStart;

                if (SC.connectingTimeStart != 0)
                    connectivityDelayMiliseconds = SC.connectingTimeStop - SC.connectingTimeStart;
                else
                    connectivityDelayMiliseconds = 0;

                totalSetupDelay = SC.totalSetupTimeStop - SC.totalSetupTimeStart;

                //fabricSetup must be sent whenever iceConnectionState changes from "checking" to "connected" state.
                await SC.callStatsClient.SendFabricSetup("sendrecv", "peer", gatheringDelayMiliseconds, connectivityDelayMiliseconds, totalSetupDelay);

                if (SC.prevIceConnectionState == disconnected)
                {
                    await SC.callStatsClient.SendIceDisruptionEnd();
                }

                _getAllStatsTimer.Elapsed += async (sender, e) =>
                {
                    SC.statsObjects.Clear();

                    SC.prevSelectedCandidateId = SC.currSelectedCandidateId;

                    await WebRtcStats.GetAllStats(pc);

                    await SC.callStatsClient.SendConferenceStatsSubmission();

                    // TODO: add values
                    SC.callStatsClient.SendSystemStatusStatsSubmission(1, 1, 1, 1, 1);

                    if (SC.prevIceConnectionState == connected 
                    || SC.prevIceConnectionState == completed)
                    {
                        if (SC.prevSelectedCandidateId != null 
                        && SC.prevSelectedCandidateId != null 
                        && SC.prevSelectedCandidateId != SC.currSelectedCandidateId)
                        {
                            // TODO: Set delay and relayType
                            await SC.callStatsClient.SendFabricTransportChange(0, "");
                        }
                    }
                };
                _getAllStatsTimer.Start();
            }

            if (pc.IceConnectionState == RTCIceConnectionState.Completed)
            {
                if (SC.newIceConnectionState != completed)
                {
                    await SetIceConnectionStates(completed);
                }

                SC.statsObjects.Clear();

                await WebRtcStats.GetAllStats(pc);

                if (SC.prevIceConnectionState == disconnected)
                {
                    await SC.callStatsClient.SendIceDisruptionEnd();
                }
            }

            if (pc.IceConnectionState == RTCIceConnectionState.Failed)
            {
                if (SC.newIceConnectionState != failed)
                {
                    await SetIceConnectionStates(failed);
                }

                SC.statsObjects.Clear();

                await WebRtcStats.GetAllStats(pc);

                if (SC.prevIceConnectionState == checking 
                    || SC.prevIceConnectionState == disconnected)
                {
                    await SC.callStatsClient.SendIceFailed();
                }

                if (SC.prevIceConnectionState == disconnected 
                    || SC.prevIceConnectionState == completed)
                {
                    // TODO: Set delay
                    SC.callStatsClient.SendFabricDropped(0);
                }

                SC.callStatsClient.SendFabricSetupFailed("sendrecv", "peer", "IceConnectionFailure", string.Empty, string.Empty, string.Empty);
            }

            if (pc.IceConnectionState == RTCIceConnectionState.Disconnected)
            {
                if (SC.newIceConnectionState != disconnected)
                {
                    await SetIceConnectionStates(disconnected);
                }

                SC.statsObjects.Clear();

                await WebRtcStats.GetAllStats(pc);

                if (SC.prevIceConnectionState == connected 
                    || SC.prevIceConnectionState == completed)
                {
                    await SC.callStatsClient.SendIceDisruptionStart();
                }

                if (SC.prevIceConnectionState == checking)
                {
                    await SC.callStatsClient.SendIceConnectionDisruptionStart();
                }
            }

            if (pc.IceConnectionState == RTCIceConnectionState.Closed)
            {
                if (SC.newIceConnectionState != closed)
                {
                    await SetIceConnectionStates(closed);
                }

                SC.statsObjects.Clear();

                await WebRtcStats.GetAllStats(pc);

                SC.callStatsClient.SendFabricSetupTerminated();

                if (SC.prevIceConnectionState == checking 
                    || SC.prevIceConnectionState == newConnection)
                {
                    await SC.callStatsClient.SendIceAborted();
                }

                if (SC.prevIceConnectionState == connected 
                    || SC.prevIceConnectionState == completed
                    || SC.prevIceConnectionState == failed 
                    || SC.prevIceConnectionState == disconnected)
                {
                    await SC.callStatsClient.SendIceTerminated();
                }
            }
        }

        public static async Task PeerConnectionClosedStateChange()
        {
            _getAllStatsTimer.Stop();

            if (SC.newIceConnectionState != closed)
            {
                await SetIceConnectionStates(closed);
            }

            SC.callStatsClient.SendFabricSetupTerminated();

            if (SC.prevIceConnectionState == checking 
                || SC.prevIceConnectionState == newConnection)
            {
                await SC.callStatsClient.SendIceAborted();
            }

            if (SC.prevIceConnectionState == connected 
                || SC.prevIceConnectionState == completed
                || SC.prevIceConnectionState == failed 
                || SC.prevIceConnectionState == disconnected)
            {
                await SC.callStatsClient.SendIceTerminated();
            }
        }

        private static async Task SetIceConnectionStates(string newState)
        {
            if (SC.prevIceConnectionState == null 
                || SC.newIceConnectionState == null)
            {
                SC.prevIceConnectionState = newConnection;
                SC.newIceConnectionState = newState;
            }
            else
            {
                SC.prevIceConnectionState = SC.newIceConnectionState;
                SC.newIceConnectionState = newState;
            }

            await SC.callStatsClient.SendFabricStateChange(
                SC.prevIceConnectionState, SC.newIceConnectionState, "iceConnectionState");
        }
        #endregion

        #region Stats OnIceGatheringStateChange
        public static async Task StatsOnIceGatheringStateChange(RTCPeerConnection pc)
        {
            if (pc.IceGatheringState == RTCIceGatheringState.Gathering)
            {
                SC.gateheringTimeStart = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

                if (SC.newIceGatheringState != gathering)
                {
                    await SetIceGatheringStates(gathering);
                }
            }

            if (pc.IceGatheringState == RTCIceGatheringState.Complete)
            {
                SC.gateheringTimeStop = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

                if (SC.newIceGatheringState != complete)
                {
                    await SetIceGatheringStates(complete);
                }
            }
        }

        private static async Task SetIceGatheringStates(string newState)
        {
            if (SC.prevIceGatheringState == null 
                || SC.newIceGatheringState == null)
            {
                SC.prevIceGatheringState = newGathering;
                SC.newIceGatheringState = newState;
            }
            else
            {
                SC.prevIceGatheringState = SC.newIceGatheringState;
                SC.newIceGatheringState = newState;
            }

            await SC.callStatsClient.SendFabricStateChange(
                SC.prevIceGatheringState, SC.newIceGatheringState, "iceGatheringState");
        }
        #endregion
    }
}
