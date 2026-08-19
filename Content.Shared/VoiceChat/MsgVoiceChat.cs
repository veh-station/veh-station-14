using Lidgren.Network;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.VoiceChat;

/// <summary>
///     Carries a single chunk of compressed-free PCM voice data.
///     Used client -> server (sender) and server -> client (broadcast to listeners).
///     Server resolves the actual sender entity from the channel, so the client leaves
///     <see cref="NetEntity"/> unset. It is only populated in the server -> client direction.
/// </summary>
public sealed class MsgVoiceChat : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Entity;

    public override NetDeliveryMethod DeliveryMethod => NetDeliveryMethod.UnreliableSequenced;

    public override int SequenceChannel => 1;

    /// <summary>
    ///     Speaker entity (only meaningful server -> client).
    /// </summary>
    public NetEntity NetEntity;

    /// <summary>
    ///     Mono 16 bit little endian PCM samples at <see cref="VoiceChatConstants.SampleRate"/>.
    /// </summary>
    public byte[] Data = Array.Empty<byte>();

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        NetEntity = buffer.ReadNetEntity();
        var length = buffer.ReadVariableInt32();
        Data = length > 0 ? buffer.ReadBytes(length) : Array.Empty<byte>();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(NetEntity);
        buffer.WriteVariableInt32(Data.Length);
        buffer.Write(Data);
    }
}