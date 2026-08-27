import { useCallback, useEffect, useRef, useState } from "react";
import AgoraRTC from "agora-rtc-sdk-ng";

/**
 * Isolates the Agora RTC client lifecycle (join/publish mic/subscribe
 * remote audio/mute/leave/cleanup) from Conversation.jsx's own UI and
 * polling concerns. Voice-only: no video track is ever created or
 * published. One call at a time - joinChannel() guards against duplicate
 * joins from repeated clicks via the `joiningRef`/`connected` state below.
 */
export function useAgoraCall() {
  const [phase, setPhase] = useState("idle"); // idle | connecting | connected | error
  const [isMuted, setIsMuted] = useState(false);
  const [errorMessage, setErrorMessage] = useState(null);

  const clientRef = useRef(null);
  const micTrackRef = useRef(null);
  const joiningRef = useRef(false);

  function getClient() {
    if (!clientRef.current) {
      clientRef.current = AgoraRTC.createClient({ mode: "rtc", codec: "vp8" });
      clientRef.current.on("user-published", async (user, mediaType) => {
        if (mediaType !== "audio") return;
        try {
          await clientRef.current.subscribe(user, mediaType);
          user.audioTrack?.play();
        } catch {
          // Non-fatal: the remote user may have already left by the time
          // subscribe resolves.
        }
      });
    }
    return clientRef.current;
  }

  const cleanup = useCallback(async () => {
    const client = clientRef.current;
    if (micTrackRef.current) {
      micTrackRef.current.close();
      micTrackRef.current = null;
    }
    if (client) {
      try {
        await client.leave();
      } catch {
        // Already left or never joined - nothing to clean up.
      }
    }
    joiningRef.current = false;
  }, []);

  // credentials: CallCredentialsDto from startCall()/joinCall() - never
  // contains the Agora App Certificate, only what the Web SDK needs to
  // join one specific channel as one specific user.
  const joinChannel = useCallback(
    async (credentials) => {
      if (joiningRef.current) return; // guards against duplicate joins from repeated clicks
      joiningRef.current = true;
      setErrorMessage(null);
      setPhase("connecting");

      try {
        const client = getClient();
        await client.join(credentials.appId, credentials.channelName, credentials.token, credentials.uid);

        let micTrack;
        try {
          micTrack = await AgoraRTC.createMicrophoneAudioTrack();
        } catch (err) {
          const isPermissionDenied =
            err?.code === "PERMISSION_DENIED" || err?.name === "NotAllowedError" || /permission/i.test(err?.message || "");
          throw new Error(
            isPermissionDenied
              ? "Microphone access was denied. Allow microphone access in your browser to make or receive calls."
              : "Couldn't access your microphone. Check that one is connected and try again.",
            { cause: err }
          );
        }

        micTrackRef.current = micTrack;
        micTrack.setEnabled(!isMuted);
        await client.publish([micTrack]);

        setPhase("connected");
      } catch (err) {
        await cleanup();
        setPhase("error");
        setErrorMessage(err.message || "Couldn't join the call. Please try again.");
      }
    },
    [cleanup, isMuted]
  );

  const leaveChannel = useCallback(async () => {
    await cleanup();
    setPhase("idle");
    setErrorMessage(null);
  }, [cleanup]);

  const toggleMute = useCallback(() => {
    setIsMuted((current) => {
      const next = !current;
      micTrackRef.current?.setEnabled(!next);
      return next;
    });
  }, []);

  // Cleanup on unmount/navigation - never leave a mic open or a channel
  // joined after the page goes away.
  useEffect(() => {
    return () => {
      cleanup();
    };
  }, [cleanup]);

  return { phase, isMuted, errorMessage, joinChannel, leaveChannel, toggleMute };
}
