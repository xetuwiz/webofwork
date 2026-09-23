import { useState } from "react";
import { useDispatch, useSelector } from "react-redux";
import { Link, useNavigate } from "react-router-dom";
import { logoutThunk } from "../../account/auth/authThunks.js";
import { useSwitchRole } from "../homePageMutation.js";

export default function ProfileModal({ onClose }) {
  const dispatch = useDispatch();
  const navigate = useNavigate();
  const user = useSelector((state) => state.auth.user);
  const { mutateAsync, isPending } = useSwitchRole();
  const [error, setError] = useState("");

  const handleLogout = async () => {
    await dispatch(logoutThunk());
    onClose();
    navigate("/");
  };

  const handleSwitchRole = async () => {
    setError("");
    try {
      await mutateAsync({ role: "freelancer" });
      onClose();
      navigate("/freelancer");
    } catch {
      setError("We could not switch roles right now.");
    }
  };

  return (
    <aside className="client-profile-menu" aria-label="Profile menu">
      <strong>{user?.fullName ?? user?.FullName ?? "Your account"}</strong>
      <nav>
        <Link to="/profile" onClick={onClose}>Profile</Link>
        <Link to="/contracts" onClick={onClose}>Contracts</Link>
        <Link to="/profile/settings" onClick={onClose}>Settings</Link>
        <Link to="/support" onClick={onClose}>Support</Link>
      </nav>
      {error && <p className="client-profile-menu__error">{error}</p>}
      <button type="button" disabled={isPending} onClick={handleSwitchRole}>{isPending ? "Switching..." : "Switch to freelancer"}</button>
      <button type="button" className="client-profile-menu__logout" onClick={handleLogout}>Log out</button>
    </aside>
  );
}