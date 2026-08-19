namespace Content.Shared.VoiceChat;

/// <summary>
///     Shared constants for the proximity voice chat prototype.
/// </summary>
public static class VoiceChatConstants
{
    /// <summary>
    ///     Radius in world units (1 unit == 1 tile) in which other players hear you.
    /// </summary>
    public const float VoiceRange = 6f;

    /// <summary>
    ///     Sample rate captured from the microphone and sent over the network.
    /// </summary>
    public const int SampleRate = 16000;

    /// <summary>
    ///     Number of channels captured from the microphone.
    /// </summary>
    public const int Channels = 1;

    /// <summary>
    ///     Length of one audio chunk, in milliseconds.
    /// </summary>
    public const int ChunkMs = 40;

    /// <summary>
    ///     Number of samples per chunk.
    /// </summary>
    public const int ChunkSamples = SampleRate * ChunkMs / 1000;

    /// <summary>
    ///     Byte size of one chunk (mono 16 bit PCM).
    /// </summary>
    public const int ChunkBytes = ChunkSamples * sizeof(short);

    /// <summary>
    ///     Whether the talker also hears their own voice (useful for testing).
    /// </summary>
    public const bool HearSelf = true;
}