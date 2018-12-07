using Org.WebRtc;
using System;
using System.Threading.Tasks;

namespace PeerConnectionClient.Stats
{
    /// <summary>
    /// Tracks peer connection states change to get data needed for Callstats client.
    /// </summary>
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

        private static long gateheringTimeStart;
        private static long gateheringTimeStop;

        private static long connectingTimeStart;
        private static long connectingTimeStop;

        private static string prevIceGatheringState;
        private static string newIceGatheringState;

        private static string newIceConnectionState;
        private static string prevIceConnectionState;

        #region Stats OnIceConnectionStateChange
        public static async Task StatsOnIceConnectionStateChange(RTCPeerConnection pc)
        {
            if (pc.IceConnectionState == RTCIceConnectionState.Checking)
            {
                connectingTimeStart = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

                if (newIceConnectionState != checking)
                {
                    await SetIceConnectionStates(checking);
                }

                SC.statsObjects.Clear();

                await WebRtcStats.GetAllStats(pc);

                if (prevIceConnectionState == connected 
                    || prevIceConnectionState == completed
                    || prevIceConnectionState == failed 
                    || prevIceConnectionState == disconnected
                    || prevIceConnectionState == closed)
                {
                    await SC.callStatsClient.SendIceRestart(
                        SC.prevIceCandidatePairObj, newConnection, prevIceConnectionState);
                }

                if (prevIceConnectionState == disconnected)
                {
                    await SC.callStatsClient.SendIceDisruptionEnd(
                        SC.currIceCandidatePairObj, SC.prevIceCandidatePairObj,
                        newIceConnectionState, prevIceConnectionState);

                    await SC.callStatsClient.SendIceConnectionDisruptionEnd(
                        newIceConnectionState, prevIceConnectionState, 0);
                }
            }

            if (pc.IceConnectionState == RTCIceConnectionState.Connected)
            {
                connectingTimeStop = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                SC.totalSetupTimeStop = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

                if (newIceConnectionState != connected)
                {
                    await SetIceConnectionStates(connected);
                }

                SC.statsObjects.Clear();

                await WebRtcStats.GetAllStats(pc);

                long gatheringDelayMiliseconds;
                long connectivityDelayMiliseconds;
                long totalSetupDelay;

                gatheringDelayMiliseconds = gateheringTimeStop - gateheringTimeStart;

                if (connectingTimeStart != 0)
                    connectivityDelayMiliseconds = connectingTimeStop - connectingTimeStart;
                else
                    connectivityDelayMiliseconds = 0;

                totalSetupDelay = SC.totalSetupTimeStop - SC.totalSetupTimeStart;

                //fabricSetup must be sent whenever iceConnectionState changes from "checking" to "connected" state.
                await SC.FabricSetup("sendrecv", "peer", gatheringDelayMiliseconds, 
                    connectivityDelayMiliseconds, totalSetupDelay);

                if (prevIceConnectionState == disconnected)
                {
                    await SC.callStatsClient.SendIceDisruptionEnd(
                        SC.currIceCandidatePairObj, SC.prevIceCandidatePairObj,
                        newIceConnectionState, prevIceConnectionState);
                }

                _getAllStatsTimer.Elapsed += async (sender, e) =>
                {
                    SC.statsObjects.Clear();

                    SC.prevSelectedCandidateId = SC.currSelectedCandidateId;

                    await WebRtcStats.GetAllStats(pc);

                    await SC.ConferenceStatsSubmission();

                    // TODO: add values
                    SC.callStatsClient.SendSystemStatusStatsSubmission(1, 1, 1, 1, 1);

                    if (prevIceConnectionState == connected 
                    || prevIceConnectionState == completed)
                    {
                        if (SC.prevSelectedCandidateId != null 
                        && SC.prevSelectedCandidateId != null 
                        && SC.prevSelectedCandidateId != SC.currSelectedCandidateId)
                        {
                            // TODO: Set delay and relayType
                            await SC.FabricTransportChange(0, "", newIceConnectionState, prevIceConnectionState);
                        }
                    }
                };
                _getAllStatsTimer.Start();
            }

            if (pc.IceConnectionState == RTCIceConnectionState.Completed)
            {
                if (newIceConnectionState != completed)
                {
                    await SetIceConnectionStates(completed);
                }

                SC.statsObjects.Clear();

                await WebRtcStats.GetAllStats(pc);

                if (prevIceConnectionState == disconnected)
                {
                    await SC.callStatsClient.SendIceDisruptionEnd(
                        SC.currIceCandidatePairObj, SC.prevIceCandidatePairObj,
                        newIceConnectionState, prevIceConnectionState);
                }
            }

            if (pc.IceConnectionState == RTCIceConnectionState.Failed)
            {
                if (newIceConnectionState != failed)
                {
                    await SetIceConnectionStates(failed);
                }

                SC.statsObjects.Clear();

                await WebRtcStats.GetAllStats(pc);

                if (prevIceConnectionState == checking 
                    || prevIceConnectionState == disconnected)
                {
                    await SC.callStatsClient.SendIceFailed(SC.localIceCandidates, SC.remoteIceCandidates,
                        SC.iceCandidatePairList, newIceConnectionState, prevIceConnectionState, 0);
                }

                if (prevIceConnectionState == disconnected 
                    || prevIceConnectionState == completed)
                {
                    // TODO: Set delay
                    SC.callStatsClient.SendFabricDropped(0, SC.GetIceCandidatePairData(),
                        newIceConnectionState, prevIceConnectionState);
                }

                SC.callStatsClient.SendFabricSetupFailed(
                    "sendrecv", "peer", "IceConnectionFailure", string.Empty, string.Empty, string.Empty);
            }

            if (pc.IceConnectionState == RTCIceConnectionState.Disconnected)
            {
                if (newIceConnectionState != disconnected)
                {
                    await SetIceConnectionStates(disconnected);
                }

                SC.statsObjects.Clear();

                await WebRtcStats.GetAllStats(pc);

                if (prevIceConnectionState == connected 
                    || prevIceConnectionState == completed)
                {
                    await SC.callStatsClient.SendIceDisruptionStart(SC.currIceCandidatePairObj,
                        newIceConnectionState, prevIceConnectionState);
                }

                if (prevIceConnectionState == checking)
                {
                    await SC.callStatsClient.SendIceConnectionDisruptionStart(
                        newIceConnectionState, prevIceConnectionState);
                }
            }

            if (pc.IceConnectionState == RTCIceConnectionState.Closed)
            {
                if (newIceConnectionState != closed)
                {
                    await SetIceConnectionStates(closed);
                }

                SC.statsObjects.Clear();

                await WebRtcStats.GetAllStats(pc);

                SC.callStatsClient.SendFabricSetupTerminated();

                if (prevIceConnectionState == checking 
                    || prevIceConnectionState == newConnection)
                {
                    await SC.callStatsClient.SendIceAborted(SC.localIceCandidates, SC.remoteIceCandidates,
                        SC.iceCandidatePairList, newIceConnectionState, prevIceConnectionState, 0);
                }

                if (prevIceConnectionState == connected 
                    || prevIceConnectionState == completed
                    || prevIceConnectionState == failed 
                    || prevIceConnectionState == disconnected)
                {
                    await SC.callStatsClient.SendIceTerminated(
                        SC.prevIceCandidatePairObj, newIceConnectionState, prevIceConnectionState);
                }
            }
        }

        public static async Task PeerConnectionClosedStateChange()
        {
            _getAllStatsTimer.Stop();

            if (newIceConnectionState != closed)
            {
                await SetIceConnectionStates(closed);
            }

            SC.callStatsClient.SendFabricSetupTerminated();

            if (prevIceConnectionState == checking 
                || prevIceConnectionState == newConnection)
            {
                await SC.callStatsClient.SendIceAborted(SC.localIceCandidates, SC.remoteIceCandidates,
                        SC.iceCandidatePairList, newIceConnectionState, prevIceConnectionState, 0);
            }

            if (prevIceConnectionState == connected 
                || prevIceConnectionState == completed
                || prevIceConnectionState == failed 
                || prevIceConnectionState == disconnected)
            {
                await SC.callStatsClient.SendIceTerminated(
                    SC.prevIceCandidatePairObj, newIceConnectionState, prevIceConnectionState);
            }
        }

        private static async Task SetIceConnectionStates(string newState)
        {
            if (prevIceConnectionState == null 
                || newIceConnectionState == null)
            {
                prevIceConnectionState = newConnection;
                newIceConnectionState = newState;
            }
            else
            {
                prevIceConnectionState = newIceConnectionState;
                newIceConnectionState = newState;
            }

            await SC.callStatsClient.SendFabricStateChange(
                prevIceConnectionState, newIceConnectionState, "iceConnectionState");
        }
        #endregion

        #region Stats OnIceGatheringStateChange
        public static async Task StatsOnIceGatheringStateChange(RTCPeerConnection pc)
        {
            if (pc.IceGatheringState == RTCIceGatheringState.Gathering)
            {
                gateheringTimeStart = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

                if (newIceGatheringState != gathering)
                {
                    await SetIceGatheringStates(gathering);
                }
            }

            if (pc.IceGatheringState == RTCIceGatheringState.Complete)
            {
                gateheringTimeStop = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

                if (newIceGatheringState != complete)
                {
                    await SetIceGatheringStates(complete);
                }
            }
        }

        private static async Task SetIceGatheringStates(string newState)
        {
            if (prevIceGatheringState == null 
                || newIceGatheringState == null)
            {
                prevIceGatheringState = newGathering;
                newIceGatheringState = newState;
            }
            else
            {
                prevIceGatheringState = newIceGatheringState;
                newIceGatheringState = newState;
            }

            await SC.callStatsClient.SendFabricStateChange(
                prevIceGatheringState, newIceGatheringState, "iceGatheringState");
        }
        #endregion
    }
}
