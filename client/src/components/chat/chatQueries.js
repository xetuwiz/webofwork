import { useQuery } from "@tanstack/react-query";
import { fetchChatMessages, fetchChatRoom, fetchChatRooms } from "./chatApi.js";
export const useChatRooms = () => useQuery({ queryKey: ["chat-rooms"], queryFn: fetchChatRooms });
export const useChatRoom = (roomId) => useQuery({ queryKey: ["chat-room", roomId], queryFn: () => fetchChatRoom(roomId), enabled: Boolean(roomId) });
export const useChatMessages = (roomId, options = {}) => useQuery({ queryKey: ["chat-messages", roomId, options], queryFn: () => fetchChatMessages({ roomId, ...options }), enabled: Boolean(roomId) });
