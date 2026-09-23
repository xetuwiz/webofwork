import { googleAuth } from "../../../../api/auth/authApi.js";

export const handleGoogleSignup = async ({ idToken, role = 1 }) => {
  if (!idToken || !String(idToken).trim()) {
    throw new Error("Google authentication token is required.");
  }

  const response = await googleAuth({ idToken, role });
  return response;
};

export default handleGoogleSignup;
