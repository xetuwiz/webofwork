import { useMemo, useState } from "react";
import { useSendChatMessage, useMarkChatRoomRead } from "./chatMutations.js";
import { useChatMessages, useChatRooms } from "./chatQueries.js";

const layoutStyle = {
  display: "grid",
  gridTemplateColumns: "280px minmax(0, 1fr)",
  minHeight: "640px",
  border: "1px solid #e5e7eb",
  borderRadius: "20px",
  overflow: "hidden",
  background: "#fff",
  boxShadow: "0 10px 30px rgba(15, 23, 42, 0.06)",
};

const sidebarStyle = {
  borderRight: "1px solid #e5e7eb",
  background: "#f8fafc",
  padding: "1rem",
};

const roomButtonStyle = (active) => ({
  width: "100%",
  border: active ? "1px solid #0f172a" : "1px solid transparent",
  background: active ? "#eef2ff" : "transparent",
  borderRadius: "12px",
  padding: "0.8rem 0.9rem",
  textAlign: "left",
  cursor: "pointer",
  marginBottom: "0.65rem",
  color: "#0f172a",
});

const contentStyle = {
  display: "flex",
  flexDirection: "column",
  minHeight: 0,
};

const headerStyle = {
  padding: "1rem 1.25rem",
  borderBottom: "1px solid #e5e7eb",
  display: "flex",
  alignItems: "center",
  justifyContent: "space-between",
  gap: "1rem",
};

const messagesStyle = {
  flex: 1,
  overflowY: "auto",
  padding: "1.25rem",
  display: "flex",
  flexDirection: "column",
  gap: "0.85rem",
  background: "#f8fafc",
};

const messageBubbleStyle = (mine) => ({
  maxWidth: "72%",
  alignSelf: mine ? "flex-end" : "flex-start",
  background: mine ? "#0f172a" : "#ffffff",
  color: mine ? "#ffffff" : "#0f172a",
  border: mine ? "1px solid #0f172a" : "1px solid #e2e8f0",
  borderRadius: "14px",
  padding: "0.7rem 0.9rem",
  lineHeight: 1.5,
  boxShadow: "0 4px 10px rgba(15, 23, 42, 0.04)",
});

const composerStyle = {
  borderTop: "1px solid #e5e7eb",
  padding: "1rem 1.25rem",
  display: "flex",
  gap: "0.75rem",
  background: "#fff",
};

const inputStyle = {
  flex: 1,
  border: "1px solid #cbd5e1",
  borderRadius: "10px",
  padding: "0.8rem 0.9rem",
  fontSize: "0.95rem",
  outline: "none",
};

const buttonStyle = {
  border: "none",
  background: "#0f172a",
  color: "#fff",
  borderRadius: "10px",
  padding: "0.8rem 1rem",
  fontWeight: 600,
  cursor: "pointer",
};

export default function Chat() {
  const { data: rooms = [] } = useChatRooms();
  const [selectedRoomId, setSelectedRoomId] = useState(null);
  const [draft, setDraft] = useState("");

  const selectedRoom = useMemo(
    () => rooms.find((room) => room.id === selectedRoomId) ?? rooms[0] ?? null,
    [rooms, selectedRoomId],
  );

  const { data: messages = [] } = useChatMessages(selectedRoom?.id);
  const sendMessage = useSendChatMessage();
  const markRoomRead = useMarkChatRoomRead();

  const handleSubmit = (event) => {
    event.preventDefault();

    if (!selectedRoom || !draft.trim()) {
      return;
    }

    sendMessage.mutate(
      { roomId: selectedRoom.id, content: draft.trim() },
      {
        onSuccess: () => {
          setDraft("");
          markRoomRead.mutate({ roomId: selectedRoom.id, messageId: null });
        },
      },
    );
  };

  if (!rooms.length) {
    return (
      <section style={{ padding: "2rem", background: "#fff", border: "1px solid #e2e8f0", borderRadius: "20px" }}>
        <span className="app-shell__eyebrow">Messages</span>
        <h1 style={{ margin: "0.5rem 0 0.5rem" }}>No active conversations yet</h1>
        <p style={{ margin: 0, color: "#475569" }}>Open a contract or conversation to start messaging.</p>
      </section>
    );
  }

  return (
    <section style={layoutStyle} aria-label="Chat conversations">
      <aside style={sidebarStyle}>
        <span className="app-shell__eyebrow">Conversations</span>
        <h2 style={{ margin: "0.75rem 0 1rem", fontSize: "1.5rem" }}>Inbox</h2>

        {rooms.map((room) => (
          <button
            key={room.id}
            type="button"
            style={roomButtonStyle(selectedRoom?.id === room.id)}
            onClick={() => setSelectedRoomId(room.id)}
          >
            <div style={{ fontWeight: 700, marginBottom: "0.2rem" }}>
              {room.title ?? room.participantName ?? `Room ${room.id}`}
            </div>
            <div style={{ color: "#475569", fontSize: "0.82rem" }}>
              {room.lastMessage ?? "No messages yet"}
            </div>
          </button>
        ))}
      </aside>

      <div style={contentStyle}>
        <header style={headerStyle}>
          <div>
            <div className="app-shell__eyebrow">Chat</div>
            <h3 style={{ margin: "0.25rem 0 0" }}>{selectedRoom?.title ?? selectedRoom?.participantName ?? "Conversation"}</h3>
          </div>
          <span style={{ color: "#475569", fontSize: "0.8rem" }}>
            {messages.length} message{messages.length === 1 ? "" : "s"}
          </span>
        </header>

        <div style={messagesStyle}>
          {messages.length === 0 ? (
            <div style={{ color: "#475569", margin: "auto", textAlign: "center" }}>
              Start the conversation with a quick introduction.
            </div>
          ) : (
            messages.map((message) => {
              const mine = message.isMine ?? message.senderId === selectedRoom?.currentUserId;
              const messageKey = message.id ?? `${message.createdAt ?? "unknown"}-${message.senderName ?? "user"}-${message.content ?? "content"}`;
              return (
                <div key={messageKey} style={messageBubbleStyle(mine)}>
                  <div style={{ fontSize: "0.72rem", opacity: 0.8, marginBottom: "0.25rem" }}>
                    {message.senderName ?? (mine ? "You" : "Participant")}
                  </div>
                  <div>{message.content ?? ""}</div>
                  <div style={{ fontSize: "0.68rem", opacity: 0.78, marginTop: "0.35rem", textAlign: mine ? "right" : "left" }}>
                    {message.createdAt ? new Date(message.createdAt).toLocaleString() : "Just now"}
                  </div>
                </div>
              );
            })
          )}
        </div>

        <form onSubmit={handleSubmit} style={composerStyle}>
          <input
            type="text"
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
            placeholder="Write a message..."
            style={inputStyle}
            aria-label="Message text"
          />
          <button type="submit" disabled={sendMessage.isPending || !draft.trim()} style={{ ...buttonStyle, opacity: sendMessage.isPending || !draft.trim() ? 0.6 : 1 }}>
            {sendMessage.isPending ? "Sending..." : "Send"}
          </button>
        </form>
      </div>
    </section>
  );
}
