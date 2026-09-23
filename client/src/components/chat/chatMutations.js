import { useMutation, useQueryClient } from "@tanstack/react-query";
import { archiveChatRoom, createChatRoom, deleteChatMessage, markChatRoomRead, sendChatMessage } from "./chatApi.js";
const refresh = (client, roomId) => { client.invalidateQueries({ queryKey: ["chat-rooms"] }); client.invalidateQueries({ queryKey: ["chat-room", roomId] }); client.invalidateQueries({ queryKey: ["chat-messages", roomId] }); };
export const useCreateChatRoom = () => { const client = useQueryClient(); return useMutation({ mutationFn: createChatRoom, onSuccess: () => client.invalidateQueries({ queryKey: ["chat-rooms"] }) }); };
export const useSendChatMessage = () => { const client = useQueryClient(); return useMutation({ mutationFn: sendChatMessage, onSuccess: (_, variables) => refresh(client, variables.roomId) }); };
export const useDeleteChatMessage = () => { const client = useQueryClient(); return useMutation({ mutationFn: deleteChatMessage, onSuccess: (_, variables) => refresh(client, variables.roomId) }); };
export const useArchiveChatRoom = () => { const client = useQueryClient(); return useMutation({ mutationFn: archiveChatRoom, onSuccess: (data, roomId) => refresh(client, roomId) }); };
export const useMarkChatRoomRead = () => { const client = useQueryClient(); return useMutation({ mutationFn: markChatRoomRead, onSuccess: (_, variables) => refresh(client, variables.roomId) }); };
