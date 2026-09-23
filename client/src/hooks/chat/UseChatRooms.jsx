import { useCallback, useEffect, useRef } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { useSelector } from "react-redux";

const getChatHubUrl = () => {
  const baseUrl = (import.meta.env.VITE_API_BASE_URL ?? "https://localhost:5001/api").replace(/\/api\/?$/, "");
  return `${baseUrl}/hubs/chat`;
};

export function useChatSocket(options = {}) {
  const queryClient = useQueryClient();
  const accessToken = useSelector((state) => state.auth?.accessToken ?? "");
  const connectionRef = useRef(null);

  const {
    onMessageReceived,
    onMessageDeleted,
    onMessagesRead,
    onUserTyping,
  } = options;

  useEffect(() => {
    if (!accessToken) {
      return undefined;
    }

    const connection = new HubConnectionBuilder()
      .withUrl(getChatHubUrl(), {
        accessTokenFactory: () => accessToken,
        withCredentials: true,
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on("MessageReceived", (message) => {
      queryClient.invalidateQueries({ queryKey: ["chat-rooms"] });
      queryClient.invalidateQueries({ queryKey: ["chat-messages"] });
      onMessageReceived?.(message);
    });

    connection.on("MessageDeleted", (payload) => {
      queryClient.invalidateQueries({ queryKey: ["chat-messages"] });
      onMessageDeleted?.(payload);
    });

    connection.on("MessagesRead", (payload) => {
      queryClient.invalidateQueries({ queryKey: ["chat-messages"] });
      onMessagesRead?.(payload);
    });

    connection.on("UserTyping", (payload) => {
      onUserTyping?.(payload);
    });

    connectionRef.current = connection;

    connection.start().catch((error) => {
      console.error("Chat SignalR connection failed:", error);
    });

    return () => {
      connection.stop().catch(() => {
        // Ignore shutdown errors; the app is already cleaning up.
      });
      connectionRef.current = null;
    };
  }, [accessToken, onMessageDeleted, onMessageReceived, onMessagesRead, onUserTyping, queryClient]);

  const joinRoom = useCallback(async (roomId) => {
    if (!roomId || !connectionRef.current) {
      return;
    }

    const connection = connectionRef.current;
    if (connection.state === "Disconnected") {
      await connection.start().catch(() => {});
    }

    if (connection.state === "Connected") {
      await connection.invoke("JoinRoom", Number(roomId)).catch(() => {});
    }
  }, []);

  const leaveRoom = useCallback(async (roomId) => {
    if (!roomId || !connectionRef.current) {
      return;
    }

    const connection = connectionRef.current;
    if (connection.state === "Connected") {
      await connection.invoke("LeaveRoom", Number(roomId)).catch(() => {});
    }
  }, []);

  return { joinRoom, leaveRoom };
}

export default useChatSocket;
