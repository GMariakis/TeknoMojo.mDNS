namespace TeknoMojo.mDNS.AirPlay.Protocol;

/// <summary>
/// RTSP methods used in the AirPlay protocol.
/// Educational: RTSP (Real Time Streaming Protocol) is similar to HTTP but designed for multimedia.
/// Defined in RFC 2326, extended by AirPlay for audio streaming (RAOP).
/// </summary>
public enum RtspMethod
{
    /// <summary>
    /// Educational: OPTIONS - Query supported methods and capabilities.
    /// Similar to HTTP OPTIONS, used to discover what the server supports.
    /// </summary>
    OPTIONS,

    /// <summary>
    /// Educational: ANNOUNCE - Provide stream description to the server.
    /// Contains SDP (Session Description Protocol) with audio format and encryption details.
    /// </summary>
    ANNOUNCE,

    /// <summary>
    /// Educational: SETUP - Configure transport parameters.
    /// Specifies RTP/UDP ports for audio data transmission.
    /// </summary>
    SETUP,

    /// <summary>
    /// Educational: RECORD - Start streaming audio to the device.
    /// Equivalent to "play" but from client to server (reverse direction).
    /// </summary>
    RECORD,

    /// <summary>
    /// Educational: PAUSE - Pause playback temporarily.
    /// Maintains the session but stops audio transmission.
    /// </summary>
    PAUSE,

    /// <summary>
    /// Educational: FLUSH - Clear buffered audio.
    /// Used when seeking or stopping to prevent stale audio from playing.
    /// </summary>
    FLUSH,

    /// <summary>
    /// Educational: TEARDOWN - End the streaming session.
    /// Releases resources and closes the connection.
    /// </summary>
    TEARDOWN,

    /// <summary>
    /// Educational: SET_PARAMETER - Set server parameters.
    /// Used for volume control, progress updates, and metadata.
    /// </summary>
    SET_PARAMETER,

    /// <summary>
    /// Educational: GET_PARAMETER - Query server parameters.
    /// Retrieve current playback state or configuration.
    /// </summary>
    GET_PARAMETER
}
