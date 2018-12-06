using CallStatsLib.Request;
using Org.WebRtc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PeerConnectionClient.Stats
{
    public static class WebRtcStats
    {
        private static readonly StatsController SC = StatsController.Instance;

        private static double _prevBytesReceivedVideo = 0.0;
        private static double _prevBytesReceivedAudio = 0.0;
        private static double _prevBytesSentVideo = 0.0;
        private static double _prevBytesSentAudio = 0.0;

        #region WebRtc Stats
        public static Dictionary<RTCStatsType, object> MakeDictionaryOfAllStats()
        {
            return new Dictionary<RTCStatsType, object>
            {
                { RTCStatsType.Codec, null },
                { RTCStatsType.InboundRtp, null },
                { RTCStatsType.OutboundRtp, null },
                { RTCStatsType.RemoteInboundRtp, null },
                { RTCStatsType.RemoteOutboundRtp, null },
                { RTCStatsType.Csrc, null },
                { RTCStatsType.PeerConnection, null },
                { RTCStatsType.DataChannel, null },
                { RTCStatsType.Stream, null },
                { RTCStatsType.Track, null },
                { RTCStatsType.Sender, null },
                { RTCStatsType.Receiver, null },
                { RTCStatsType.Transport, null },
                { RTCStatsType.CandidatePair, null },
                { RTCStatsType.LocalCandidate, null },
                { RTCStatsType.RemoteCandidate, null },
                { RTCStatsType.Certificate, null }
            };
        }

        private static RTCStatsTypeSet statsType = new RTCStatsTypeSet(MakeDictionaryOfAllStats());

        public static async Task GetAllStats(RTCPeerConnection pc)
        {
            IRTCStatsReport statsReport = await Task.Run(async () => await pc.GetStats(statsType));

            GetAllStatsData(statsReport);
        }

        public static void GetAllStatsData(IRTCStatsReport statsReport)
        {
            Dictionary<string, string> candidatePairsDict = new Dictionary<string, string>();

            for (int i = 0; i < statsReport.StatsIds.Count; i++)
            {
                IRTCStats rtcStats = statsReport.GetStats(statsReport.StatsIds[i]);

                RTCStatsType? statsType = rtcStats.StatsType;

                string statsTypeOther = rtcStats.StatsTypeOther;

                if (statsType == null)
                {
                    if (statsTypeOther == "ice-candidate")
                    {
                        RTCIceCandidateStats iceCandidateStats = RTCIceCandidateStats.Cast(rtcStats);

                        IceCandidateStats ics = new IceCandidateStats();
                        ics.candidateType = iceCandidateStats.CandidateType.ToString().ToLower();
                        ics.deleted = iceCandidateStats.Deleted;
                        ics.id = iceCandidateStats.Id;
                        ics.ip = iceCandidateStats.Ip;
                        ics.networkType = iceCandidateStats.NetworkType.ToString();
                        ics.port = iceCandidateStats.Port;
                        ics.priority = iceCandidateStats.Priority;
                        ics.protocol = iceCandidateStats.Protocol.ToLower();
                        ics.relayProtocol = iceCandidateStats.RelayProtocol;
                        ics.type = candidatePairsDict[ics.id];
                        ics.statsTypeOther = iceCandidateStats.StatsTypeOther;
                        ics.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                        ics.transportId = iceCandidateStats.TransportId;
                        ics.url = iceCandidateStats.Url;

                        SC.iceCandidateStatsList.Add(ics);

                        SC.statsObjects.Add(ics);
                    }
                }

                else if (statsType == RTCStatsType.Codec)
                {
                    RTCCodecStats codecStats = RTCCodecStats.Cast(rtcStats);

                    CodecStats cs = new CodecStats();
                    cs.channels = codecStats.Channels;
                    cs.clockRate = codecStats.ClockRate;
                    cs.codecType = codecStats.CodecType.ToString();
                    cs.id = codecStats.Id;
                    cs.implementation = codecStats.Implementation;
                    cs.mimeType = codecStats.MimeType;
                    cs.payloadType = codecStats.PayloadType;
                    cs.sdpFmtpLine = codecStats.SdpFmtpLine;
                    cs.type = codecStats.StatsType.ToString().ToLower();
                    cs.statsTypeOther = codecStats.StatsTypeOther;
                    cs.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                    cs.transportId = codecStats.TransportId;

                    SC.statsObjects.Add(cs);
                }

                else if (statsType == RTCStatsType.InboundRtp)
                {
                    RTCInboundRtpStreamStats inboundRtpStats = RTCInboundRtpStreamStats.Cast(rtcStats);

                    InboundRtpStreamStats irss = new InboundRtpStreamStats();
                    irss.averageRtcpInterval = inboundRtpStats.AverageRtcpInterval;
                    irss.burstDiscardCount = inboundRtpStats.BurstDiscardCount;
                    irss.burstDiscardRate = inboundRtpStats.BurstDiscardRate;
                    irss.burstLossCount = inboundRtpStats.BurstLossCount;
                    irss.burstLossRate = inboundRtpStats.BurstLossRate;
                    irss.burstPacketsDiscarded = inboundRtpStats.BurstPacketsLost;
                    irss.burstPacketsLost = inboundRtpStats.BurstPacketsLost;
                    irss.bytesReceived = inboundRtpStats.BytesReceived;
                    irss.codecId = inboundRtpStats.CodecId;
                    irss.fecPacketsReceived = inboundRtpStats.FecPacketsReceived;
                    irss.firCount = inboundRtpStats.FirCount;
                    irss.framesDecoded = inboundRtpStats.FramesDecoded;
                    irss.gapDiscardRate = inboundRtpStats.GapDiscardRate;
                    irss.gapLossRate = inboundRtpStats.GapLossRate;
                    irss.id = inboundRtpStats.Id;
                    irss.jitter = inboundRtpStats.Jitter;
                    irss.kind = inboundRtpStats.Kind;
                    //irss.lastPacketReceivedTimestamp = inboundRtpStats.LastPacketReceivedTimestamp;
                    irss.nackCount = inboundRtpStats.NackCount;
                    irss.packetsDiscarded = inboundRtpStats.PacketsDiscarded;
                    irss.packetsDuplicated = inboundRtpStats.PacketsDuplicated;
                    irss.packetsFailedDecryption = inboundRtpStats.PacketsFailedDecryption;
                    irss.packetsLost = inboundRtpStats.PacketsLost;
                    irss.packetsReceived = inboundRtpStats.PacketsReceived;
                    irss.packetsRepaired = inboundRtpStats.PacketsRepaired;
                    //irss.perDscpPacketsReceived = (Dictionary<string, ulong>)inboundRtpStats.PerDscpPacketsReceived;
                    irss.pliCount = inboundRtpStats.PliCount;
                    irss.qpSum = inboundRtpStats.QpSum;
                    irss.receiverId = inboundRtpStats.ReceiverId;
                    irss.remoteId = inboundRtpStats.RemoteId;
                    irss.sliCount = inboundRtpStats.SliCount;
                    irss.ssrc = inboundRtpStats.Ssrc;
                    irss.type = "inbound-rtp";
                    irss.statsTypeOther = inboundRtpStats.StatsTypeOther;
                    irss.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                    irss.trackId = inboundRtpStats.TrackId;
                    irss.transportId = inboundRtpStats.TransportId;

                    long csioIntMs = DateTime.UtcNow.ToUnixTimeStampMiliseconds() - SC.milisec;

                    if (irss.id.ToLower().Contains("audio"))
                    {
                        if (SC.milisec != 0)
                            irss.csioIntBRKbps = (irss.bytesReceived - _prevBytesReceivedAudio) * 8 / csioIntMs;

                        _prevBytesReceivedAudio = irss.bytesReceived;
                    }

                    if (irss.id.ToLower().Contains("video"))
                    {
                        if (SC.milisec != 0)
                            irss.csioIntBRKbps = (irss.bytesReceived - _prevBytesReceivedVideo) * 8 / csioIntMs;

                        _prevBytesReceivedVideo = irss.bytesReceived;
                    }

                    SC.statsObjects.Add(irss);
                }

                else if (statsType == RTCStatsType.OutboundRtp)
                {
                    RTCOutboundRtpStreamStats outboundRtpStats = RTCOutboundRtpStreamStats.Cast(rtcStats);

                    OutboundRtpStreamStat orss = new OutboundRtpStreamStat();
                    orss.averageRtcpInterval = outboundRtpStats.AverageRtcpInterval;
                    orss.bytesDiscardedOnSend = outboundRtpStats.BytesDiscardedOnSend;
                    orss.bytesSent = outboundRtpStats.BytesSent;
                    orss.codecId = outboundRtpStats.CodecId;
                    orss.fecPacketsSent = outboundRtpStats.FecPacketsSent;
                    orss.firCount = outboundRtpStats.FirCount;
                    orss.framesEncoded = outboundRtpStats.FramesEncoded;
                    orss.id = outboundRtpStats.Id;
                    orss.kind = outboundRtpStats.Kind;
                    orss.nackCount = outboundRtpStats.NackCount;
                    orss.packetsDiscardedOnSend = outboundRtpStats.PacketsDiscardedOnSend;
                    orss.packetsSent = outboundRtpStats.PacketsSent;
                    //orss.perDscpPacketsSent = (Dictionary<string, ulong>)outboundRtpStats.PerDscpPacketsSent;
                    orss.pliCount = outboundRtpStats.PliCount;
                    orss.qpSum = outboundRtpStats.QpSum;
                    //orss.qualityLimitationReason = outboundRtpStats.QualityLimitationDurations.ToString();
                    orss.remoteId = outboundRtpStats.RemoteId;
                    orss.senderId = outboundRtpStats.SenderId;
                    orss.sliCount = outboundRtpStats.SliCount;
                    orss.ssrc = outboundRtpStats.Ssrc;
                    orss.type = "outbound-rtp";
                    orss.statsTypeOther = outboundRtpStats.StatsTypeOther;
                    orss.targetBitrate = outboundRtpStats.TargetBitrate;
                    orss.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                    orss.trackId = outboundRtpStats.TrackId;
                    orss.transportId = outboundRtpStats.TransportId;

                    long csioIntMs = DateTime.UtcNow.ToUnixTimeStampMiliseconds() - SC.milisec;

                    if (orss.id.ToLower().Contains("audio"))
                    {
                        if (SC.milisec != 0)
                            orss.csioIntBRKbps = (orss.bytesSent - _prevBytesSentAudio) * 8 / csioIntMs;

                        _prevBytesSentAudio = orss.bytesSent;
                    }

                    if (orss.id.ToLower().Contains("video"))
                    {
                        if (SC.milisec != 0)
                            orss.csioIntBRKbps = (orss.bytesSent - _prevBytesSentVideo) * 8 / csioIntMs;

                        _prevBytesSentVideo = orss.bytesSent;
                    }

                    SC.statsObjects.Add(orss);
                }

                else if (statsType == RTCStatsType.RemoteInboundRtp)
                {
                    RTCRemoteInboundRtpStreamStats remoteInboundRtpStats = RTCRemoteInboundRtpStreamStats.Cast(rtcStats);

                    RemoteInboundRtpStreamStats rirss = new RemoteInboundRtpStreamStats();
                    rirss.burstDiscardCount = remoteInboundRtpStats.BurstDiscardCount;
                    rirss.burstDiscardRate = remoteInboundRtpStats.BurstDiscardRate;
                    rirss.burstLossCount = remoteInboundRtpStats.BurstLossCount;
                    rirss.burstLossRate = remoteInboundRtpStats.BurstLossRate;
                    rirss.burstPacketsDiscarded = remoteInboundRtpStats.BurstPacketsDiscarded;
                    rirss.burstPacketsLost = remoteInboundRtpStats.BurstPacketsLost;
                    rirss.codecId = remoteInboundRtpStats.CodecId;
                    rirss.firCount = remoteInboundRtpStats.FirCount;
                    rirss.fractionLost = remoteInboundRtpStats.FractionLost;
                    rirss.gapDiscardRate = remoteInboundRtpStats.GapDiscardRate;
                    rirss.gapLossRate = remoteInboundRtpStats.GapLossRate;
                    rirss.id = remoteInboundRtpStats.Id;
                    rirss.jitter = remoteInboundRtpStats.Jitter;
                    rirss.kind = remoteInboundRtpStats.Kind;
                    rirss.localId = remoteInboundRtpStats.LocalId;
                    rirss.nackCount = remoteInboundRtpStats.NackCount;
                    rirss.packetsDiscarded = remoteInboundRtpStats.PacketsDiscarded;
                    rirss.packetsLost = remoteInboundRtpStats.PacketsLost;
                    rirss.packetsReceived = remoteInboundRtpStats.PacketsReceived;
                    rirss.packetsRepaired = remoteInboundRtpStats.PacketsRepaired;
                    rirss.pliCount = remoteInboundRtpStats.PliCount;
                    rirss.qpSum = remoteInboundRtpStats.QpSum;
                    rirss.roundTripTime = remoteInboundRtpStats.RoundTripTime;
                    rirss.sliCount = remoteInboundRtpStats.SliCount;
                    rirss.ssrc = remoteInboundRtpStats.Ssrc;
                    rirss.type = remoteInboundRtpStats.StatsType.ToString().ToLower();
                    rirss.statsTypeOther = remoteInboundRtpStats.StatsTypeOther;
                    rirss.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                    rirss.transportId = remoteInboundRtpStats.TransportId;

                    SC.statsObjects.Add(rirss);
                }

                else if (statsType == RTCStatsType.RemoteOutboundRtp)
                {
                    RTCRemoteOutboundRtpStreamStats remoteOutboundRtpStats = RTCRemoteOutboundRtpStreamStats.Cast(rtcStats);

                    RemoteOutboundRtpStreamStats rorss = new RemoteOutboundRtpStreamStats();
                    rorss.bytesDiscardedOnSend = remoteOutboundRtpStats.BytesDiscardedOnSend;
                    rorss.bytesSent = remoteOutboundRtpStats.BytesSent;
                    rorss.codecId = remoteOutboundRtpStats.CodecId;
                    rorss.fecPacketsSent = remoteOutboundRtpStats.FecPacketsSent;
                    rorss.firCount = remoteOutboundRtpStats.FirCount;
                    rorss.id = remoteOutboundRtpStats.Id;
                    rorss.kind = remoteOutboundRtpStats.Kind;
                    rorss.localId = remoteOutboundRtpStats.LocalId;
                    rorss.nackCount = remoteOutboundRtpStats.NackCount;
                    rorss.packetsDiscardedOnSend = remoteOutboundRtpStats.PacketsDiscardedOnSend;
                    rorss.packetsSent = remoteOutboundRtpStats.PacketsSent;
                    rorss.pliCount = remoteOutboundRtpStats.PliCount;
                    rorss.qpSum = remoteOutboundRtpStats.QpSum;
                    rorss.sliCount = remoteOutboundRtpStats.SliCount;
                    rorss.ssrc = remoteOutboundRtpStats.Ssrc;
                    rorss.type = remoteOutboundRtpStats.StatsType.ToString().ToLower();
                    rorss.StatsTypeOther = remoteOutboundRtpStats.StatsTypeOther;
                    rorss.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                    rorss.TransportId = remoteOutboundRtpStats.TransportId;

                    SC.statsObjects.Add(rorss);
                }

                else if (statsType == RTCStatsType.Csrc)
                {
                    RTCRtpContributingSourceStats csrcStats = RTCRtpContributingSourceStats.Cast(rtcStats);

                    RtpContributingSourceStats rcss = new RtpContributingSourceStats();
                    rcss.audioLevel = csrcStats.AudioLevel;
                    rcss.contributorSsrc = csrcStats.ContributorSsrc;
                    rcss.id = csrcStats.Id;
                    rcss.inboundRtpStreamId = csrcStats.InboundRtpStreamId;
                    rcss.packetsContributedTo = csrcStats.PacketsContributedTo;
                    rcss.type = csrcStats.StatsType.ToString().ToLower();
                    rcss.statsTypeOther = csrcStats.StatsTypeOther;
                    rcss.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

                    SC.statsObjects.Add(rcss);
                }

                else if (statsType == RTCStatsType.PeerConnection)
                {
                    RTCPeerConnectionStats peerConnectionStats = RTCPeerConnectionStats.Cast(rtcStats);

                    PeerConnectionStats pcs = new PeerConnectionStats();
                    pcs.dataChannelsAccepted = peerConnectionStats.DataChannelsAccepted;
                    pcs.dataChannelsClosed = peerConnectionStats.DataChannelsClosed;
                    pcs.dataChannelsOpened = peerConnectionStats.DataChannelsOpened;
                    pcs.dataChannelsRequested = peerConnectionStats.DataChannelsRequested;
                    pcs.id = peerConnectionStats.Id;
                    pcs.type = "peer-connection";
                    pcs.statsTypeOther = peerConnectionStats.StatsTypeOther;
                    pcs.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

                    SC.statsObjects.Add(pcs);
                }

                else if (statsType == RTCStatsType.DataChannel)
                {
                    RTCDataChannelStats dataChannelStats = RTCDataChannelStats.Cast(rtcStats);

                    DataChannelStats dc = new DataChannelStats();
                    dc.bytesReceived = dataChannelStats.BytesReceived;
                    dc.bytesSent = dataChannelStats.BytesSent;
                    dc.dataChannelIdentifier = dataChannelStats.DataChannelIdentifier;
                    dc.id = dataChannelStats.Id;
                    dc.label = dataChannelStats.Label;
                    dc.messagesReceived = dataChannelStats.MessagesReceived;
                    dc.messagesSent = dataChannelStats.MessagesSent;
                    dc.protocol = dataChannelStats.Protocol;
                    dc.state = dataChannelStats.State.ToString().ToLower();
                    dc.type = "data-channel";
                    dc.statsTypeOther = dataChannelStats.StatsTypeOther;
                    dc.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                    dc.transportId = dataChannelStats.TransportId;

                    SC.statsObjects.Add(dc);
                }

                else if (statsType == RTCStatsType.Stream)
                {
                    RTCMediaStreamStats mediaStreamStats = RTCMediaStreamStats.Cast(rtcStats);

                    MediaStreamStats mss = new MediaStreamStats();
                    mss.id = mediaStreamStats.Id;
                    mss.type = mediaStreamStats.StatsType.ToString().ToLower();
                    mss.statsTypeOther = mediaStreamStats.StatsTypeOther;
                    mss.streamIdentifier = mediaStreamStats.StreamIdentifier;
                    mss.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                    mss.trackIds = mediaStreamStats.TrackIds.ToList();

                    SC.statsObjects.Add(mss);
                }

                else if (statsType == RTCStatsType.Track)
                {
                    RTCMediaHandlerStats mediaHandlerStats = RTCMediaHandlerStats.Cast(rtcStats);

                    if (mediaHandlerStats.Kind == "audio")
                    {
                        RTCSenderAudioTrackAttachmentStats audioTrackStats = RTCSenderAudioTrackAttachmentStats.Cast(rtcStats);

                        SenderAudioTrackAttachmentStats satas = new SenderAudioTrackAttachmentStats();
                        satas.audioLevel = audioTrackStats.AudioLevel;
                        satas.echoReturnLoss = audioTrackStats.EchoReturnLoss;
                        satas.echoReturnLossEnhancement = audioTrackStats.EchoReturnLossEnhancement;
                        satas.ended = audioTrackStats.Ended;
                        satas.id = audioTrackStats.Id;
                        satas.kind = audioTrackStats.Kind;
                        satas.priority = audioTrackStats.Priority.ToString();
                        satas.remoteSource = audioTrackStats.RemoteSource;
                        satas.type = audioTrackStats.StatsType.ToString().ToLower();
                        satas.statsTypeOther = audioTrackStats.StatsTypeOther;
                        satas.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                        satas.totalAudioEnergy = audioTrackStats.TotalAudioEnergy;
                        satas.totalSamplesDuration = audioTrackStats.TotalSamplesDuration;
                        satas.totalSamplesSent = audioTrackStats.TotalSamplesSent;
                        satas.trackIdentifier = audioTrackStats.TrackIdentifier;
                        satas.voiceActivityFlag = audioTrackStats.VoiceActivityFlag;

                        SC.statsObjects.Add(satas);
                    }

                    else if (mediaHandlerStats.Kind == "video")
                    {
                        RTCSenderVideoTrackAttachmentStats videoTrackStats = RTCSenderVideoTrackAttachmentStats.Cast(rtcStats);

                        SenderVideoTrackAttachmentStats svtas = new SenderVideoTrackAttachmentStats();
                        svtas.ended = videoTrackStats.Ended;
                        svtas.frameHeight = videoTrackStats.FrameHeight;
                        svtas.framesCaptured = videoTrackStats.FramesCaptured;
                        svtas.framesPerSecond = videoTrackStats.FramesPerSecond;
                        svtas.framesSent = videoTrackStats.FramesSent;
                        svtas.frameWidth = videoTrackStats.FrameWidth;
                        svtas.hugeFramesSent = videoTrackStats.HugeFramesSent;
                        svtas.id = videoTrackStats.Id;
                        svtas.keyFramesSent = videoTrackStats.KeyFramesSent;
                        svtas.kind = videoTrackStats.Kind;
                        svtas.priority = videoTrackStats.Priority.ToString();
                        svtas.remoteSource = (bool)videoTrackStats.RemoteSource;
                        svtas.type = videoTrackStats.StatsType.ToString().ToLower();
                        svtas.statsTypeOther = videoTrackStats.StatsTypeOther;
                        svtas.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                        svtas.trackIdentifier = videoTrackStats.TrackIdentifier;

                        SC.statsObjects.Add(svtas);
                    }
                }

                else if (statsType == RTCStatsType.Sender)
                {
                    RTCMediaHandlerStats mediaHandlerStats = RTCMediaHandlerStats.Cast(rtcStats);

                    if (mediaHandlerStats.Kind == "audio")
                    {
                        RTCAudioSenderStats audioSenderStats = RTCAudioSenderStats.Cast(rtcStats);

                        AudioSenderStats aus = new AudioSenderStats();
                        aus.audioLevel = audioSenderStats.AudioLevel;
                        aus.echoReturnLoss = audioSenderStats.EchoReturnLoss;
                        aus.echoReturnLossEnhancement = audioSenderStats.EchoReturnLossEnhancement;
                        aus.ended = audioSenderStats.Ended;
                        aus.id = audioSenderStats.Id;
                        aus.kind = audioSenderStats.Kind;
                        aus.priority = audioSenderStats.Priority.ToString();
                        aus.remoteSource = audioSenderStats.RemoteSource;
                        aus.statsTypeOther = audioSenderStats.StatsTypeOther;
                        aus.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                        aus.totalAudioEnergy = audioSenderStats.TotalAudioEnergy;
                        aus.totalSamplesDuration = audioSenderStats.TotalSamplesDuration;
                        aus.totalSamplesSent = audioSenderStats.TotalSamplesSent;
                        aus.trackIdentifier = audioSenderStats.TrackIdentifier;
                        aus.type = audioSenderStats.StatsType.ToString();
                        aus.voiceActivityFlag = audioSenderStats.VoiceActivityFlag;

                        SC.statsObjects.Add(aus);
                    }

                    else if (mediaHandlerStats.Kind == "video")
                    {
                        RTCVideoSenderStats videoSenderStats = RTCVideoSenderStats.Cast(rtcStats);

                        VideoSenderStats vis = new VideoSenderStats();
                        vis.ended = videoSenderStats.Ended;
                        vis.frameHeight = videoSenderStats.FrameHeight;
                        vis.framesCaptured = videoSenderStats.FramesCaptured;
                        vis.framesPerSecond = videoSenderStats.FramesPerSecond;
                        vis.framesSent = videoSenderStats.FramesSent;
                        vis.frameWidth = videoSenderStats.FrameWidth;
                        vis.hugeFramesSent = videoSenderStats.HugeFramesSent;
                        vis.id = videoSenderStats.Id;
                        vis.keyFramesSent = videoSenderStats.KeyFramesSent;
                        vis.kind = videoSenderStats.Kind;
                        vis.priority = videoSenderStats.Priority.ToString();
                        vis.remoteSource = videoSenderStats.RemoteSource;
                        vis.statsTypeOther = videoSenderStats.StatsTypeOther;
                        vis.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                        vis.trackIdentifier = videoSenderStats.TrackIdentifier;
                        vis.type = videoSenderStats.StatsType.ToString();

                        SC.statsObjects.Add(vis);
                    }
                }

                else if (statsType == RTCStatsType.Receiver)
                {
                    RTCMediaHandlerStats mediaHandlerStats = RTCMediaHandlerStats.Cast(rtcStats);

                    if (mediaHandlerStats.Kind == "audio")
                    {
                        RTCAudioReceiverStats audioReceiverStats = RTCAudioReceiverStats.Cast(rtcStats);

                        AudioReceiverStats aur = new AudioReceiverStats();
                        aur.audioLevel = audioReceiverStats.AudioLevel;
                        aur.concealedSamples = audioReceiverStats.ConcealedSamples;
                        aur.concealmentEvents = audioReceiverStats.ConcealmentEvents;
                        aur.ended = audioReceiverStats.Ended;
                        aur.id = audioReceiverStats.Id;
                        aur.jitterBufferEmittedCount = audioReceiverStats.JitterBufferEmittedCount;
                        aur.kind = audioReceiverStats.Kind;
                        aur.priority = audioReceiverStats.Priority.ToString();
                        aur.remoteSource = audioReceiverStats.RemoteSource;
                        aur.statsTypeOther = audioReceiverStats.StatsTypeOther;
                        aur.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                        aur.totalAudioEnergy = audioReceiverStats.TotalAudioEnergy;
                        aur.totalSamplesDuration = audioReceiverStats.TotalSamplesDuration;
                        aur.totalSamplesReceived = audioReceiverStats.TotalSamplesReceived;
                        aur.trackIdentifier = audioReceiverStats.TrackIdentifier;
                        aur.type = audioReceiverStats.StatsType.ToString();
                        aur.voiceActivityFlag = audioReceiverStats.VoiceActivityFlag;

                        SC.statsObjects.Add(aur);
                    }

                    else if (mediaHandlerStats.Kind == "video")
                    {
                        RTCVideoReceiverStats videoReceiverStats = RTCVideoReceiverStats.Cast(rtcStats);

                        VideoReceiverStats vir = new VideoReceiverStats();
                        vir.ended = videoReceiverStats.Ended;
                        vir.frameHeight = videoReceiverStats.FrameHeight;
                        vir.framesDecoded = videoReceiverStats.FramesDecoded;
                        vir.framesDropped = videoReceiverStats.FramesDropped;
                        vir.framesPerSecond = videoReceiverStats.FramesPerSecond;
                        vir.framesReceived = videoReceiverStats.FramesReceived;
                        vir.frameWidth = videoReceiverStats.FrameWidth;
                        vir.fullFramesLost = videoReceiverStats.FullFramesLost;
                        vir.id = videoReceiverStats.Id;
                        vir.jitterBufferEmittedCount = videoReceiverStats.JitterBufferEmittedCount;
                        vir.keyFramesReceived = videoReceiverStats.KeyFramesReceived;
                        vir.kind = videoReceiverStats.Kind;
                        vir.partialFramesLost = videoReceiverStats.PartialFramesLost;
                        vir.priority = videoReceiverStats.Priority.ToString();
                        vir.remoteSource = videoReceiverStats.RemoteSource;
                        vir.StatsTypeOther = videoReceiverStats.StatsTypeOther;
                        vir.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                        vir.trackIdentifier = videoReceiverStats.TrackIdentifier;
                        vir.type = videoReceiverStats.StatsType.ToString();

                        SC.statsObjects.Add(vir);
                    }
                }

                else if (statsType == RTCStatsType.Transport)
                {
                    RTCTransportStats transportStats = RTCTransportStats.Cast(rtcStats);

                    TransportStats ts = new TransportStats();
                    ts.bytesReceived = transportStats.BytesReceived;
                    ts.bytesSent = transportStats.BytesSent;
                    ts.dtlsCipher = transportStats.DtlsCipher;
                    ts.dtlsState = transportStats.DtlsState.ToString();
                    ts.iceRole = transportStats.IceRole.ToString();
                    ts.id = transportStats.Id;
                    ts.localCertificateId = transportStats.LocalCertificateId;
                    ts.packetsReceived = transportStats.PacketsReceived;
                    ts.packetsSent = transportStats.PacketsSent;
                    ts.remoteCertificateId = transportStats.RemoteCertificateId;
                    ts.rtcpTransportStatsId = transportStats.RtcpTransportStatsId;
                    ts.selectedCandidatePairId = transportStats.SelectedCandidatePairId;
                    ts.srtpCipher = transportStats.SrtpCipher;
                    ts.type = transportStats.StatsType.ToString().ToLower();
                    ts.statsTypeOther = transportStats.StatsTypeOther;
                    ts.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

                    SC.statsObjects.Add(ts);

                    SC.currSelectedCandidateId = transportStats.SelectedCandidatePairId;
                }

                else if (statsType == RTCStatsType.CandidatePair)
                {
                    RTCIceCandidatePairStats candidatePairStats = RTCIceCandidatePairStats.Cast(rtcStats);

                    SC.prevIceCandidatePairObj = SC.currIceCandidatePairObj;

                    IceCandidatePairStats icp = new IceCandidatePairStats();
                    //icp.availableIncomingBitrate = candidatePairStats.AvailableIncomingBitrate;
                    //icp.availableOutgoingBitrate = candidatePairStats.AvailableOutgoingBitrate;
                    icp.bytesReceived = candidatePairStats.BytesReceived;
                    icp.bytesSent = candidatePairStats.BytesSent;
                    //icp.circuitBreakerTriggerCount = candidatePairStats.CircuitBreakerTriggerCount;
                    //icp.consentExpiredTimestamp = candidatePairStats.ConsentExpiredTimestamp;
                    icp.consentRequestsSent = candidatePairStats.ConsentRequestsSent;
                    //icp.currentRoundTripTime = candidatePairStats.CurrentRoundTripTime;
                    icp.currentRoundTripTime = 0.199;
                    //icp.firstRequestTimestamp = candidatePairStats.FirstRequestTimestamp;
                    icp.id = candidatePairStats.Id;
                    //icp.lastPacketReceivedTimestamp = candidatePairStats.LastPacketReceivedTimestamp;
                    //icp.lastPacketSentTimestamp = candidatePairStats.LastPacketSentTimestamp;
                    //icp.lastRequestTimestamp = candidatePairStats.LastRequestTimestamp;
                    //icp.lastResponseTimestamp = candidatePairStats.LastResponseTimestamp;
                    icp.localCandidateId = candidatePairStats.LocalCandidateId;
                    icp.nominated = candidatePairStats.Nominated;
                    icp.packetsReceived = candidatePairStats.PacketsReceived;
                    icp.packetsSent = candidatePairStats.PacketsSent;
                    icp.remoteCandidateId = candidatePairStats.RemoteCandidateId;
                    icp.requestsReceived = candidatePairStats.RequestsReceived;
                    icp.requestsSent = candidatePairStats.RequestsSent;
                    icp.responsesReceived = candidatePairStats.ResponsesReceived;
                    icp.responsesSent = candidatePairStats.ResponsesSent;
                    icp.retransmissionsReceived = candidatePairStats.RetransmissionsReceived;
                    icp.retransmissionsSent = candidatePairStats.RetransmissionsSent;
                    icp.state = candidatePairStats.State.ToString().ToLower();
                    //icp.type = candidatePairStats.StatsType.ToString().ToLower();
                    icp.type = "candidate-pair";
                    icp.statsTypeOther = candidatePairStats.StatsTypeOther;
                    icp.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                    //icp.totalRoundTripTime = candidatePairStats.TotalRoundTripTime;
                    icp.totalRoundTripTime = 1.234;
                    icp.transportId = candidatePairStats.TransportId;

                    SC.statsObjects.Add(icp);

                    if (!candidatePairsDict.ContainsKey(candidatePairStats.LocalCandidateId))
                        candidatePairsDict.Add(candidatePairStats.LocalCandidateId, "local-candidate");

                    if (!candidatePairsDict.ContainsKey(candidatePairStats.RemoteCandidateId))
                        candidatePairsDict.Add(candidatePairStats.RemoteCandidateId, "remote-candidate");

                    SC.iceCandidatePairStatsList.Add(icp);

                    SC.currIceCandidatePair = candidatePairStats;

                    SC.currIceCandidatePairObj = SC.GetIceCandidatePairData();
                }

                else if (statsType == RTCStatsType.Certificate)
                {
                    RTCCertificateStats certificateStats = RTCCertificateStats.Cast(rtcStats);

                    CertificateStats cs = new CertificateStats();
                    cs.base64Certificate = certificateStats.Base64Certificate;
                    cs.fingerprint = certificateStats.Fingerprint;
                    cs.fingerprintAlgorithm = certificateStats.FingerprintAlgorithm;
                    cs.id = certificateStats.Id;
                    cs.issuerCertificateId = certificateStats.IssuerCertificateId;
                    cs.type = certificateStats.StatsType.ToString().ToLower();
                    cs.statsTypeOther = certificateStats.StatsTypeOther;
                    cs.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

                    SC.statsObjects.Add(cs);
                }
            }
        }
        #endregion
    }
}
