export function formatChatMessage(message, currentUserId) {
  if (!message || !String(message.content ?? "").trim()) return null;
  return { ...message, isMine: message.isMine ?? message.senderId === currentUserId, displayTime: message.createdAt ? new Date(message.createdAt).toLocaleString() : "Just now" };
}
