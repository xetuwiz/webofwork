import axiosInstance from "../axiosInstance.js";

export const sendOtp = async ({ email }) => {
  const response = await axiosInstance.post("/auth/send-otp", { email });
  return response.data;
};

export const verifyOtp = async ({ email, otp, password, fullName, role }) => {
  const response = await axiosInstance.post("/auth/verify-otp", {
    email,
    otp,
    password,
    fullName,
    role,
  });
  return response.data;
};

export const loginUser = async ({ email, password }) => {
  const response = await axiosInstance.post("/auth/login", { email, password });
  return response.data;
};

export const getCurrentUser = async () => {
  const response = await axiosInstance.get("/auth/me");
  return response.data;
};

export const logoutUser = async () => {
  const response = await axiosInstance.post("/auth/logout");
  return response.data;
};

export const refreshToken = async () => {
  const response = await axiosInstance.post("/auth/refresh");
  return response.data;
};

export const forgotPassword = async ({ email }) => {
  const response = await axiosInstance.post("/auth/forgot-password", { email });
  return response.data;
};

export const resetPassword = async ({ email, token, newPassword }) => {
  const response = await axiosInstance.post("/auth/reset-password", {
    email,
    token,
    newPassword,
  });
  return response.data;
};

export const googleAuth = async ({ idToken, role }) => {
  const response = await axiosInstance.post("/auth/google", { idToken, role });
  return response.data;
};
