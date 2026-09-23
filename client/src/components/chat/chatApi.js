import axiosInstance from "../../api/axiosInstance.js";
export const fetchChatRooms = async () => (await axiosInstance.get("/chat/rooms")).data;
export const createChatRoom = async (contractId) => (await axiosInstance.post("/chat/rooms", { contractId })).data;
export const fetchChatRoom = async (roomId) => (await axiosInstance.get(`/chat/rooms/${roomId}`)).data;
export const fetchChatMessages = async ({ roomId, pageSize = 50, beforeMessageId }) => (await axiosInstance.get(`/chat/rooms/${roomId}/messages`, { params: { pageSize, beforeMessageId } })).data;
export const sendChatMessage = async ({ roomId, content }) => (await axiosInstance.post(`/chat/rooms/${roomId}/messages`, { content })).data;
export const deleteChatMessage = async ({ roomId, messageId }) => (await axiosInstance.delete(`/chat/rooms/${roomId}/messages/${messageId}`)).data;
export const archiveChatRoom = async (roomId) => (await axiosInstance.patch(`/chat/rooms/${roomId}/archive`)).data;
export const markChatRoomRead = async ({ roomId, messageId }) => (await axiosInstance.patch(`/chat/rooms/${roomId}/read`, { messageId })).data;
