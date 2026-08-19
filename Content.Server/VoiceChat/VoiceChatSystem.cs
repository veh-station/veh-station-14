using System.Collections.Generic;
using Content.Shared.VoiceChat;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.VoiceChat;

/// <summary>
///     Receives voice chunks from talking players and relays them to every other player
///     within <see cref="VoiceChatConstants.VoiceRange"/> tiles of the speaker.
/// </summary>
public sealed partial class VoiceChatSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private IGameTiming _gameTiming = default!;

    private ISawmill _sawmill = default!;

    private readonly Dictionary<NetUserId, float> _lastChunkTime = new();

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("voice");
        _net.RegisterNetMessage<MsgVoiceChat>(OnClientVoice);
    }

    private void OnClientVoice(MsgVoiceChat message)
    {
        // Sanity check the payload before doing anything.
        if (message.Data.Length <= 0 || message.Data.Length > VoiceChatConstants.ChunkBytes * 2)
            return;

        var session = _player.GetSessionByChannel(message.MsgChannel);
        if (session.AttachedEntity is not { } speaker)
            return;

        // Simple rate limit: at most ~33 chunks per second per player.
        var now = (float) _gameTiming.CurTime.TotalSeconds;
        if (_lastChunkTime.TryGetValue(session.UserId, out var last) && now - last < 0.03f)
            return;
        _lastChunkTime[session.UserId] = now;

        var mapCoords = _xform.GetMapCoordinates(Transform(speaker));
        var netSpeaker = GetNetEntity(speaker);

        var recipients = Filter.Empty().AddInRange(mapCoords, VoiceChatConstants.VoiceRange, _player, EntityManager);

        foreach (var recipient in recipients.Recipients)
        {
            if (recipient.Channel == message.MsgChannel && !VoiceChatConstants.HearSelf)
                continue;

            if (recipient.AttachedEntity == null)
                continue;

            _net.ServerSendMessage(new MsgVoiceChat
            {
                NetEntity = netSpeaker,
                Data = message.Data,
            }, recipient.Channel);
        }
    }
}