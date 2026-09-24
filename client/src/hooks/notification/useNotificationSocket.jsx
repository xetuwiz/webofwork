import { useEffect, useRef } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { useSelector } from "react-redux";

const getNotificationHubUrl = () => {
  const baseUrl = (import.meta.env.VITE_API_BASE_URL ?? "https://localhost:5001/api").replace(/\/api\/?$/, "");
  return `${baseUrl}/hubs/notifications`;
};

export function useNotificationSocket(onNotificationReceived) {
  const queryClient = useQueryClient();
  const accessToken = useSelector((state) => state.auth?.accessToken ?? "");
  const connectionRef = useRef(null);

  useEffect(() => {
    if (!accessToken) {
      return undefined;
    }

    const connection = new HubConnectionBuilder()
      .withUrl(getNotificationHubUrl(), {
        accessTokenFactory: () => accessToken,
        withCredentials: true,
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on("NotificationReceived", (notification) => {
      queryClient.invalidateQueries({ queryKey: ["notifications"] });
      onNotificationReceived?.(notification);
    });

    connection.on("AiWorkflowUpdated", (workflow) => {
      queryClient.invalidateQueries({ queryKey: ["ai-workflow"] });
      queryClient.invalidateQueries({ queryKey: ["ai-workflows"] });
      onNotificationReceived?.(workflow);
    });
    connectionRef.current = connection;

    connection.start().catch((error) => {
      console.error("Notification SignalR connection failed:", error);
    });

    return () => {
      connection.stop().catch(() => {
        // Ignore shutdown errors; the app is already cleaning up.
      });
      connectionRef.current = null;
    };
  }, [accessToken, onNotificationReceived, queryClient]);

  return {};
}

export default useNotificationSocket;
