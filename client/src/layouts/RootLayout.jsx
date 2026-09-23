import { useEffect } from "react";
import { Outlet } from "react-router-dom";
import { useDispatch, useSelector } from "react-redux";
import { fetchUser } from "../features/client/account/auth/authThunks.js";
import Spinner from "../components/Spinner.jsx";

export default function RootLayout() {
  const dispatch = useDispatch();
  const { accessToken, initialized, loading, user } = useSelector((state) => state.auth);

  useEffect(() => {
    if (!initialized && !loading) dispatch(fetchUser());
  }, [dispatch, initialized, loading]);

  if (!initialized && accessToken) return <Spinner label="Restoring your session" />;
  return <Outlet context={{ user }} />;
}