using System;
using System.Collections.Generic;
using Content.Shared.Input;
using Content.Shared.VoiceChat;
using OpenTK.Audio.OpenAL;
using Robust.Client.Audio;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Input;
using Robust.Shared.Network;

namespace Content.Client.VoiceChat;

/// <summary>
///     Prototype proximity voice chat. Hold the push-to-talk key (V by default) to transmit
///     microphone audio to every player within <see cref="VoiceChatConstants.VoiceRange"/> tiles.
/// </summary>
public sealed partial class VoiceChatSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IPlayerManager _player = default!;

    private IAudioManager _audioManager = default!;
    private ISawmill _sawmill = default!;

    private ALCaptureDevice _captureDevice;
    private bool _captureAvailable;

    private float _sendAccumulator;

    // One-shot audio entities created for received chunks, pending cleanup.
    private readonly List<(AudioStream Stream, EntityUid Entity)> _activeStreams = new();

    public override void Initialize()
    {
        base.Initialize();
        _audioManager = IoCManager.Resolve<IAudioManager>();
        _sawmill = Logger.GetSawmill("voice");
        _net.RegisterNetMessage<MsgVoiceChat>(OnServerVoice);

        TryOpenCapture();
    }

    public override void Shutdown()
    {
        base.Shutdown();

        if (_captureAvailable)
        {
            ALC.CaptureStop(_captureDevice);
            ALC.CaptureCloseDevice(_captureDevice);
            _captureAvailable = false;
        }

        foreach (var (stream, _) in _activeStreams)
            stream.Dispose();
        _activeStreams.Clear();
    }

    private void TryOpenCapture()
    {
        try
        {
            _captureDevice = ALC.CaptureOpenDevice(null, VoiceChatConstants.SampleRate,
                ALFormat.Mono16, VoiceChatConstants.SampleRate * 4);

            if (_captureDevice == ALCaptureDevice.Null)
            {
                _sawmill.Error("Voice chat: failed to open the default microphone.");
                return;
            }

            _captureAvailable = true;
            ALC.CaptureStart(_captureDevice);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Voice chat: exception while opening the microphone: {e}");
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        CleanupFinishedStreams();

        if (!_captureAvailable)
            return;

        if (!_net.IsConnected || _player.LocalEntity is not { Valid: true })
        {
            _sendAccumulator = 0f;
            return;
        }

        var pttDown = _input.TryGetKeyBinding(ContentKeyFunctions.VoiceChatPTT, out var binding)
                      && binding.State == BoundKeyState.Down;

        if (!pttDown)
        {
            _sendAccumulator = 0f;
            return;
        }

        _sendAccumulator += frameTime;

        var chunkTime = VoiceChatConstants.ChunkMs / 1000f;
        while (_sendAccumulator >= chunkTime)
        {
            _sendAccumulator -= chunkTime;

            var available = ALC.GetInteger(_captureDevice, AlcGetInteger.CaptureSamples);
            if (available < VoiceChatConstants.ChunkSamples)
                break;

            var samples = new short[VoiceChatConstants.ChunkSamples];
            ALC.CaptureSamples(_captureDevice, samples, VoiceChatConstants.ChunkSamples);

            var data = new byte[VoiceChatConstants.ChunkBytes];
            for (var i = 0; i < samples.Length; i++)
            {
                data[i * 2] = (byte) (samples[i] & 0xFF);
                data[i * 2 + 1] = (byte) (samples[i] >> 8);
            }

            _net.ClientSendMessage(new MsgVoiceChat { Data = data });
        }
    }

    private void OnServerVoice(MsgVoiceChat message)
    {
        if (message.Data.Length <= 0 || message.Data.Length > VoiceChatConstants.ChunkBytes * 2)
            return;

        var speaker = GetEntity(message.NetEntity);
        if (TerminatingOrDeleted(speaker))
            return;

        PlayChunk(message.Data, speaker);
    }

    private void PlayChunk(byte[] data, EntityUid speaker)
    {
        var sampleCount = data.Length / sizeof(short);
        var samples = new short[sampleCount];
        for (var i = 0; i < samples.Length; i++)
            samples[i] = (short) (data[i * 2] | (data[i * 2 + 1] << 8));

        AudioStream stream;
        try
        {
            stream = _audioManager.LoadAudioRaw(samples, VoiceChatConstants.Channels,
                VoiceChatConstants.SampleRate, "voice");
        }
        catch (Exception e)
        {
            _sawmill.Error($"Voice chat: failed to load incoming audio: {e}");
            return;
        }

        var audioParams = AudioParams.Default
            .WithMaxDistance(VoiceChatConstants.VoiceRange)
            .WithReferenceDistance(1f)
            .WithRolloffFactor(1f);

        var result = _audio.PlayEntity(stream, speaker, null, audioParams);
        if (result == null)
        {
            stream.Dispose();
            return;
        }

        _activeStreams.Add((stream, result.Value.Entity));
    }

    private void CleanupFinishedStreams()
    {
        for (var i = _activeStreams.Count - 1; i >= 0; i--)
        {
            var (stream, entity) = _activeStreams[i];
            if (!EntityManager.Deleted(entity))
                continue;

            stream.Dispose();
            _activeStreams.RemoveAt(i);
        }
    }
}