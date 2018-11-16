using System;
using System.Collections.Generic;

namespace CallStatsLib.Request
{
    public class ConferenceStatsSubmissionData
    {
        public string localID { get; set; }
        public string originID { get; set; }
        public string deviceID { get; set; }
        public string connectionID { get; set; }
        public long timestamp { get; set; }
        public string remoteID { get; set; }
        public List<object> stats { get; set; }
    }

    public class MediaStreamStats
    {
        public string id { get; set; }
        public string type { get; set; }
        public string statsTypeOther { get; set; }
        public string streamIdentifier { get; set; }
        public long timestamp { get; set; }
        public List<string> trackIds { get; set; }
    }

    public class AudioSenderStats
    {
        public double audioLevel { get; set; }
        public double? echoReturnLoss { get; set; }
        public double? echoReturnLossEnhancement { get; set; }
        public bool ended { get; set; }
        public string id { get; set; }
        public string kind { get; set; }
        public string priority { get; set; }
        public bool? remoteSource { get; set; }
        public string type { get; set; }
        public string statsTypeOther { get; set; }
        public long timestamp { get; set; }
        public double totalAudioEnergy { get; set; }
        public double totalSamplesDuration { get; set; }
        public ulong totalSamplesSent { get; set; }
        public string trackIdentifier { get; set; }
        public bool voiceActivityFlag { get; set; }
    }

    public class VideoSenderStats
    {
        public bool ended { get; set; }
        public ulong frameHeight { get; set; }
        public ulong framesCaptured { get; set; }
        public double framesPerSecond { get; set; }
        public ulong framesSent { get; set; }
        public ulong frameWidth { get; set; }
        public ulong hugeFramesSent { get; set; }
        public string id { get; set; }
        public ulong keyFramesSent { get; set; }
        public string kind { get; set; }
        public string priority { get; set; }
        public bool? remoteSource { get; set; }
        public string type { get; set; }
        public string statsTypeOther { get; set; }
        public long timestamp { get; set; }
        public string trackIdentifier { get; set; }
    }

    public class AudioReceiverStats
    {
        public double audioLevel { get; set; }
        public ulong concealedSamples { get; set; }
        public ulong concealmentEvents { get; set; }
        public bool ended { get; set; }
        //public DateTimeOffset estimatedPlayoutTimestamp { get; set; }
        public string id { get; set; }
        //public TimeSpan jitterBufferDelay { get; set; }
        public ulong jitterBufferEmittedCount { get; set; }
        public string kind { get; set; }
        public string priority { get; set; }
        public bool? remoteSource { get; set; }
        public string type { get; set; }
        public string statsTypeOther { get; set; }
        public long timestamp { get; set; }
        public double totalAudioEnergy { get; set; }
        public double totalSamplesDuration { get; set; }
        public ulong totalSamplesReceived { get; set; }
        public string trackIdentifier { get; set; }
        public bool voiceActivityFlag { get; set; }
    }

    public class VideoReceiverStats
    {
        public bool ended { get; set; }
        //public DateTimeOffset EstimatedPlayoutTimestamp { get; set; }
        public ulong frameHeight { get; set; }
        public ulong framesDecoded { get; set; }
        public ulong framesDropped { get; set; }
        public double framesPerSecond { get; set; }
        public ulong framesReceived { get; set; }
        public ulong frameWidth { get; set; }
        public ulong fullFramesLost { get; set; }
        public string id { get; set; }
        //public TimeSpan jitterBufferDelay { get; set; }
        public ulong jitterBufferEmittedCount { get; set; }
        public ulong keyFramesReceived { get; set; }
        public string kind { get; set; }
        public ulong partialFramesLost { get; set; }
        public string priority { get; set; }
        public bool? remoteSource { get; set; }
        public string type { get; set; }
        public string StatsTypeOther { get; set; }
        public long timestamp { get; set; }
        public string trackIdentifier { get; set; }
    }

    public class SenderVideoTrackAttachmentStats
    {
        public bool ended { get; set; }
        public ulong frameHeight { get; set; }
        public ulong framesCaptured { get; set; }
        public double framesPerSecond { get; set; }
        public ulong framesSent { get; set; }
        public ulong frameWidth { get; set; }
        public ulong hugeFramesSent { get; set; }
        public string id { get; set; }
        public ulong keyFramesSent { get; set; }
        public string kind { get; set; }
        public string priority { get; set; }
        public bool remoteSource { get; set; }
        public string type { get; set; }
        public string statsTypeOther { get; set; }
        public long timestamp { get; set; }
        public string trackIdentifier { get; set; }
    }

    public class SenderAudioTrackAttachmentStats
    {
        public double audioLevel { get; set; }
        public double? echoReturnLoss { get; set; }
        public double? echoReturnLossEnhancement { get; set; }
        public bool ended { get; set; }
        public string id { get; set; }
        public string kind { get; set; }
        public string priority { get; set; }
        public bool? remoteSource { get; set; }
        public string type { get; set; }
        public string statsTypeOther { get; set; }
        public long timestamp { get; set; }
        public double totalAudioEnergy { get; set; }
        public double totalSamplesDuration { get; set; }
        public ulong totalSamplesSent { get; set; }
        public string trackIdentifier { get; set; }
        public bool voiceActivityFlag { get; set; }
    }

    public class PeerConnectionStats
    {
        public ulong dataChannelsAccepted { get; set; }
        public ulong dataChannelsClosed { get; set; }
        public ulong dataChannelsOpened { get; set; }
        public ulong dataChannelsRequested { get; set; }
        public string id { get; set; }
        public string type { get; set; }
        public string statsTypeOther { get; set; }
        public long timestamp { get; set; }
    }

    public class IceCandidateStats
    {
        public string candidateType { get; set; }
        public bool? deleted { get; set; }
        public string id { get; set; }
        public string ip { get; set; }
        public string networkType { get; set; }
        public long port { get; set; }
        public long priority { get; set; }
        public string protocol { get; set; }
        public string relayProtocol { get; set; }
        public string type { get; set; }
        public string statsTypeOther { get; set; }
        public long timestamp { get; set; }
        public string transportId { get; set; }
        public string url { get; set; }
    }

    public class CodecStats
    {
        public ulong? channels { get; set; }
        public ulong clockRate { get; set; }
        public string codecType { get; set; }
        public string id { get; set; }
        public string implementation { get; set; }
        public string mimeType { get; set; }
        public byte? payloadType { get; set; }
        public string sdpFmtpLine { get; set; }
        public string type { get; set; }
        public string statsTypeOther { get; set; }
        public long timestamp { get; set; }
        public string transportId { get; set; }
    }

    public class InboundRtpStreamStats
    {
        public double averageRtcpInterval { get; set; }
        public ulong burstDiscardCount { get; set; }
        public double burstDiscardRate { get; set; }
        public ulong burstLossCount { get; set; }
        public double burstLossRate { get; set; }
        public ulong burstPacketsDiscarded { get; set; }
        public ulong burstPacketsLost { get; set; }
        public ulong bytesReceived { get; set; }
        public string codecId { get; set; }
        public ulong fecPacketsReceived { get; set; }
        public ulong firCount { get; set; }
        public ulong? framesDecoded { get; set; }
        public double gapDiscardRate { get; set; }
        public double gapLossRate { get; set; }
        public string id { get; set; }
        public double jitter { get; set; }
        public string kind { get; set; }
        //public DateTimeOffset lastPacketReceivedTimestamp { get; set; }
        public ulong nackCount { get; set; }
        public ulong packetsDiscarded { get; set; }
        public ulong packetsDuplicated { get; set; }
        public ulong packetsFailedDecryption { get; set; }
        public ulong packetsLost { get; set; }
        public ulong packetsReceived { get; set; }
        public ulong packetsRepaired { get; set; }
        public Dictionary<string, ulong> perDscpPacketsReceived { get; set; }
        public ulong pliCount { get; set; }
        public ulong qpSum { get; set; }
        public string receiverId { get; set; }
        public string remoteId { get; set; }
        public ulong sliCount { get; set; }
        public uint? ssrc { get; set; } 
        public string type { get; set; }
        public string statsTypeOther { get; set; }
        public long timestamp { get; set; }
        public string trackId { get; set; }
        public string transportId { get; set; }
        public double csioIntBRKbps { get; set; }
    }

    public class OutboundRtpStreamStat
    {
        public double averageRtcpInterval { get; set; }
        public ulong bytesDiscardedOnSend { get; set; }
        public ulong bytesSent { get; set; }
        public string codecId { get; set; }
        public ulong fecPacketsSent { get; set; }
        public ulong firCount { get; set; }
        public ulong? framesEncoded { get; set; }
        public string id { get; set; }
        public string kind { get; set; }
        //public DateTimeOffset lastPacketSentTimestamp { get; set; }
        public ulong nackCount { get; set; }
        public ulong packetsDiscardedOnSend { get; set; }
        public ulong packetsSent { get; set; }
        public Dictionary<string, ulong> perDscpPacketsSent { get; set; }
        public ulong pliCount { get; set; }
        public ulong qpSum { get; set; }
        //Dictionary<RTCQualityLimitationReason, TimeSpan> qualityLimitationDurations { get; set; }
        public string qualityLimitationReason { get; set; }
        public string remoteId { get; set; }
        public string senderId { get; set; }
        public ulong sliCount { get; set; }
        public uint? ssrc { get; set; }
        public string type { get; set; }
        public string statsTypeOther { get; set; }
        public double targetBitrate { get; set; }
        public long timestamp { get; set; }
        //public TimeSpan TotalEncodeTime { get; set; }
        public string trackId { get; set; }
        public string transportId { get; set; }
        public double csioIntBRKbps { get; set; }
    }

    public class RemoteInboundRtpStreamStats
    {
        public ulong burstDiscardCount { get; set; }
        public double burstDiscardRate { get; set; }
        public ulong burstLossCount { get; set; }
        public double burstLossRate { get; set; }
        public ulong burstPacketsDiscarded { get; set; }
        public ulong burstPacketsLost { get; set; }
        public string codecId { get; set; }
        public ulong firCount { get; set; }
        public double fractionLost { get; set; }
        public double gapDiscardRate { get; set; }
        public double gapLossRate { get; set; }
        public string id { get; set; }
        public double jitter { get; set; }
        public string kind { get; set; }
        public string localId { get; set; }
        public ulong nackCount { get; set; }
        public ulong packetsDiscarded { get; set; }
        public ulong packetsLost { get; set; }
        public ulong packetsReceived { get; set; }
        public ulong packetsRepaired { get; set; }
        public ulong pliCount { get; set; }
        public ulong qpSum { get; set; }
        public double roundTripTime { get; set; }
        public ulong sliCount { get; set; }
        public uint? ssrc { get; set; }
        public string type { get; set; }
        public string statsTypeOther { get; set; }
        public long timestamp { get; set; }
        public string transportId { get; set; }
    }

    public class RemoteOutboundRtpStreamStats
    {
        public ulong bytesDiscardedOnSend { get; set; }
        public ulong bytesSent { get; set; }
        public string codecId { get; set; }
        public ulong fecPacketsSent { get; set; }
        public ulong firCount { get; set; }
        public string id { get; set; }
        public string kind { get; set; }
        public string localId { get; set; }
        public ulong nackCount { get; set; }
        public ulong packetsDiscardedOnSend { get; set; }
        public ulong packetsSent { get; set; }
        public ulong pliCount { get; set; }
        public ulong qpSum { get; set; }
        //public DateTimeOffset remoteTimestamp { get; set; }
        public ulong sliCount { get; set; }
        public uint? ssrc { get; set; }
        public string type { get; set; }
        public string StatsTypeOther { get; set; }
        public long timestamp { get; set; }
        public string TransportId { get; set; }
    }

    public class RtpContributingSourceStats
    {
        public double? audioLevel { get; set; }
        public ulong contributorSsrc { get; set; }
        public string id { get; set; }
        public string inboundRtpStreamId { get; set; }
        public ulong packetsContributedTo { get; set; }
        public string type { get; set; }
        public string statsTypeOther { get; set; }
        public long timestamp { get; set; }
    }

    public class TransportStats
    {
        public ulong bytesReceived { get; set; }
        public ulong bytesSent { get; set; }
        public string dtlsCipher { get; set; }
        public string dtlsState { get; set; }
        public string iceRole { get; set; }
        public string id { get; set; }
        public string localCertificateId { get; set; }
        public ulong packetsReceived { get; set; }
        public ulong packetsSent { get; set; }
        public string remoteCertificateId { get; set; }
        public string rtcpTransportStatsId { get; set; }
        public string selectedCandidatePairId { get; set; }
        public string srtpCipher { get; set; }
        public string type { get; set; }
        public string statsTypeOther { get; set; }
        public long timestamp { get; set; }
    }

    public class IceCandidatePairStats
    {
        public double availableIncomingBitrate { get; set; }
        public double availableOutgoingBitrate { get; set; }
        public ulong bytesReceived { get; set; }
        public ulong bytesSent { get; set; }
        public ulong circuitBreakerTriggerCount { get; set; }
        public DateTimeOffset consentExpiredTimestamp { get; set; }
        public ulong consentRequestsSent { get; set; }
        public double currentRoundTripTime { get; set; }
        public DateTimeOffset firstRequestTimestamp { get; set; }
        public string id { get; set; }
        public DateTimeOffset lastPacketReceivedTimestamp { get; set; }
        public DateTimeOffset lastPacketSentTimestamp { get; set; }
        public DateTimeOffset lastRequestTimestamp { get; set; }
        public DateTimeOffset lastResponseTimestamp { get; set; }
        public string localCandidateId { get; set; }
        public bool nominated { get; set; }
        public ulong packetsReceived { get; set; }
        public ulong packetsSent { get; set; }
        public string remoteCandidateId { get; set; }
        public ulong requestsReceived { get; set; }
        public ulong requestsSent { get; set; }
        public ulong responsesReceived { get; set; }
        public ulong responsesSent { get; set; }
        public ulong retransmissionsReceived { get; set; }
        public ulong retransmissionsSent { get; set; }
        public string state { get; set; }
        public string type { get; set; }
        public string statsTypeOther { get; set; }
        public long timestamp { get; set; }
        public double totalRoundTripTime { get; set; }
        public string transportId { get; set; }
    }

    public class CertificateStats
    {
        public string base64Certificate { get; set; }
        public string fingerprint { get; set; }
        public string fingerprintAlgorithm { get; set; }
        public string id { get; set; }
        public string issuerCertificateId { get; set; }
        public string type { get; set; }
        public string statsTypeOther { get; set; }
        public long timestamp { get; set; }
    }

    public class DataChannelStats
    {
        public ulong bytesReceived { get; set; }
        public ulong bytesSent { get; set; }
        public long dataChannelIdentifier { get; set; }
        public string id { get; set; }
        public string label { get; set; }
        public ulong messagesReceived { get; set; }
        public ulong messagesSent { get; set; }
        public string protocol { get; set; }
        public string state { get; set; }
        public string type { get; set; }
        public string statsTypeOther { get; set; }
        public long timestamp { get; set; }
        public string transportId { get; set; }
    }
}
