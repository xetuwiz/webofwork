export const canAccessClientRoute = (user) => {
  if (!user) return false;

  const role = String(user.role ?? user.Role ?? user.current_role ?? user.currentRole ?? "").toLowerCase();

  return role === "client" || role === "1" || user.is_client === true || user.current_role === "client";
};
