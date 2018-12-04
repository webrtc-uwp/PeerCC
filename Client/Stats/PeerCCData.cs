using Org.WebRtc;
using System;
using System.Threading.Tasks;

namespace PeerConnectionClient.Stats
{
    public class PeerCCData
    {
        static System.Timers.Timer _getAllStatsTimer = new System.Timers.Timer(10000);

        #region Stats OnIceConnectionStateChange
        public static async Task StatsOnIceConnectionStateChange(RTCPeerConnection pc)
        {
            if (pc.IceConnectionState == RTCIceConnectionState.Checking)
            {
                SharedProperties.connectingTimeStart = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

                if (SharedProperties.newIceConnectionState != SharedProperties.checking)
                {
                    await SetIceConnectionStates(SharedProperties.checking);
                }

                SharedProperties.statsObjects.Clear();

                await WebRtcData.GetAllStats(pc);

                if (SharedProperties.prevIceConnectionState == SharedProperties.connected 
                    || SharedProperties.prevIceConnectionState == SharedProperties.completed
                    || SharedProperties.prevIceConnectionState == SharedProperties.failed 
                    || SharedProperties.prevIceConnectionState == SharedProperties.disconnected
                    || SharedProperties.prevIceConnectionState == SharedProperties.closed)
                {
                    await SharedProperties.callStatsClient.SendIceRestart();
                }

                if (SharedProperties.prevIceConnectionState == SharedProperties.disconnected)
                {
                    await SharedProperties.callStatsClient.SendIceDisruptionEnd();

                    await SharedProperties.callStatsClient.SendIceConnectionDisruptionEnd();
                }
            }

            if (pc.IceConnectionState == RTCIceConnectionState.Connected)
            {
                SharedProperties.connectingTimeStop = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                SharedProperties.totalSetupTimeStop = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

                if (SharedProperties.newIceConnectionState != SharedProperties.connected)
                {
                    await SetIceConnectionStates(SharedProperties.connected);
                }

                SharedProperties.statsObjects.Clear();

                await WebRtcData.GetAllStats(pc);

                long gatheringDelayMiliseconds;
                long connectivityDelayMiliseconds;
                long totalSetupDelay;

                gatheringDelayMiliseconds = SharedProperties.gateheringTimeStop - SharedProperties.gateheringTimeStart;

                if (SharedProperties.connectingTimeStart != 0)
                    connectivityDelayMiliseconds = SharedProperties.connectingTimeStop - SharedProperties.connectingTimeStart;
                else
                    connectivityDelayMiliseconds = 0;

                totalSetupDelay = SharedProperties.totalSetupTimeStop - SharedProperties.totalSetupTimeStart;

                //fabricSetup must be sent whenever iceConnectionState changes from "checking" to "connected" state.
                await SharedProperties.callStatsClient.SendFabricSetup(gatheringDelayMiliseconds, connectivityDelayMiliseconds, totalSetupDelay);

                if (SharedProperties.prevIceConnectionState == SharedProperties.disconnected)
                {
                    await SharedProperties.callStatsClient.SendIceDisruptionEnd();
                }

                _getAllStatsTimer.Elapsed += async (sender, e) =>
                {
                    SharedProperties.statsObjects.Clear();

                    SharedProperties.prevSelectedCandidateId = SharedProperties.currSelectedCandidateId;

                    await WebRtcData.GetAllStats(pc);

                    await SharedProperties.callStatsClient.SendConferenceStatsSubmission();

                    SharedProperties.callStatsClient.SendSystemStatusStatsSubmission();

                    if (SharedProperties.prevIceConnectionState == SharedProperties.connected 
                    || SharedProperties.prevIceConnectionState == SharedProperties.completed)
                    {
                        if (SharedProperties.prevSelectedCandidateId != null 
                        && SharedProperties.prevSelectedCandidateId != null 
                        && SharedProperties.prevSelectedCandidateId != SharedProperties.currSelectedCandidateId)
                        {
                            await SharedProperties.callStatsClient.SendFabricTransportChange();
                        }
                    }
                };
                _getAllStatsTimer.Start();
            }

            if (pc.IceConnectionState == RTCIceConnectionState.Completed)
            {
                if (SharedProperties.newIceConnectionState != SharedProperties.completed)
                {
                    await SetIceConnectionStates(SharedProperties.completed);
                }

                SharedProperties.statsObjects.Clear();

                await WebRtcData.GetAllStats(pc);

                if (SharedProperties.prevIceConnectionState == SharedProperties.disconnected)
                {
                    await SharedProperties.callStatsClient.SendIceDisruptionEnd();
                }
            }

            if (pc.IceConnectionState == RTCIceConnectionState.Failed)
            {
                if (SharedProperties.newIceConnectionState != SharedProperties.failed)
                {
                    await SetIceConnectionStates(SharedProperties.failed);
                }

                SharedProperties.statsObjects.Clear();

                await WebRtcData.GetAllStats(pc);

                if (SharedProperties.prevIceConnectionState == SharedProperties.checking 
                    || SharedProperties.prevIceConnectionState == SharedProperties.disconnected)
                {
                    await SharedProperties.callStatsClient.SendIceFailed();
                }

                if (SharedProperties.prevIceConnectionState == SharedProperties.disconnected 
                    || SharedProperties.prevIceConnectionState == SharedProperties.completed)
                {
                    SharedProperties.callStatsClient.SendFabricDropped();
                }

                SharedProperties.callStatsClient.SendFabricSetupFailed("IceConnectionFailure", string.Empty, string.Empty, string.Empty);
            }

            if (pc.IceConnectionState == RTCIceConnectionState.Disconnected)
            {
                if (SharedProperties.newIceConnectionState != SharedProperties.disconnected)
                {
                    await SetIceConnectionStates(SharedProperties.disconnected);
                }

                SharedProperties.statsObjects.Clear();

                await WebRtcData.GetAllStats(pc);

                if (SharedProperties.prevIceConnectionState == SharedProperties.connected 
                    || SharedProperties.prevIceConnectionState == SharedProperties.completed)
                {
                    await SharedProperties.callStatsClient.SendIceDisruptionStart();
                }

                if (SharedProperties.prevIceConnectionState == SharedProperties.checking)
                {
                    await SharedProperties.callStatsClient.SendIceConnectionDisruptionStart();
                }
            }

            if (pc.IceConnectionState == RTCIceConnectionState.Closed)
            {
                if (SharedProperties.newIceConnectionState != SharedProperties.closed)
                {
                    await SetIceConnectionStates(SharedProperties.closed);
                }

                SharedProperties.statsObjects.Clear();

                await WebRtcData.GetAllStats(pc);

                SharedProperties.callStatsClient.SendFabricSetupTerminated();

                if (SharedProperties.prevIceConnectionState == SharedProperties.checking 
                    || SharedProperties.prevIceConnectionState == SharedProperties.newConnection)
                {
                    await SharedProperties.callStatsClient.SendIceAborted();
                }

                if (SharedProperties.prevIceConnectionState == SharedProperties.connected 
                    || SharedProperties.prevIceConnectionState == SharedProperties.completed
                    || SharedProperties.prevIceConnectionState == SharedProperties.failed 
                    || SharedProperties.prevIceConnectionState == SharedProperties.disconnected)
                {
                    await SharedProperties.callStatsClient.SendIceTerminated();
                }
            }
        }

        public static async Task PeerConnectionClosedStateChange()
        {
            _getAllStatsTimer.Stop();

            if (SharedProperties.newIceConnectionState != SharedProperties.closed)
            {
                await SetIceConnectionStates(SharedProperties.closed);
            }

            SharedProperties.callStatsClient.SendFabricSetupTerminated();

            if (SharedProperties.prevIceConnectionState == SharedProperties.checking 
                || SharedProperties.prevIceConnectionState == SharedProperties.newConnection)
            {
                await SharedProperties.callStatsClient.SendIceAborted();
            }

            if (SharedProperties.prevIceConnectionState == SharedProperties.connected 
                || SharedProperties.prevIceConnectionState == SharedProperties.completed
                || SharedProperties.prevIceConnectionState == SharedProperties.failed 
                || SharedProperties.prevIceConnectionState == SharedProperties.disconnected)
            {
                await SharedProperties.callStatsClient.SendIceTerminated();
            }
        }

        private static async Task SetIceConnectionStates(string newState)
        {
            if (SharedProperties.prevIceConnectionState == null || SharedProperties.newIceConnectionState == null)
            {
                SharedProperties.prevIceConnectionState = SharedProperties.newConnection;
                SharedProperties.newIceConnectionState = newState;
            }
            else
            {
                SharedProperties.prevIceConnectionState = SharedProperties.newIceConnectionState;
                SharedProperties.newIceConnectionState = newState;
            }

            await SharedProperties.callStatsClient.SendFabricStateChange(
                SharedProperties.prevIceConnectionState, SharedProperties.newIceConnectionState, "iceConnectionState");
        }
        #endregion

        #region Stats OnIceGatheringStateChange
        public static async Task StatsOnIceGatheringStateChange(RTCPeerConnection pc)
        {
            if (pc.IceGatheringState == RTCIceGatheringState.Gathering)
            {
                SharedProperties.gateheringTimeStart = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

                if (SharedProperties.newIceGatheringState != SharedProperties.gathering)
                {
                    await SetIceGatheringStates(SharedProperties.gathering);
                }
            }

            if (pc.IceGatheringState == RTCIceGatheringState.Complete)
            {
                SharedProperties.gateheringTimeStop = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

                if (SharedProperties.newIceGatheringState != SharedProperties.complete)
                {
                    await SetIceGatheringStates(SharedProperties.complete);
                }
            }
        }

        private static async Task SetIceGatheringStates(string newState)
        {
            if (SharedProperties.prevIceGatheringState == null || SharedProperties.newIceGatheringState == null)
            {
                SharedProperties.prevIceGatheringState = SharedProperties.newGathering;
                SharedProperties.newIceGatheringState = newState;
            }
            else
            {
                SharedProperties.prevIceGatheringState = SharedProperties.newIceGatheringState;
                SharedProperties.newIceGatheringState = newState;
            }

            await SharedProperties.callStatsClient.SendFabricStateChange(SharedProperties.prevIceGatheringState, SharedProperties.newIceGatheringState, "iceGatheringState");
        }
        #endregion
    }
}
