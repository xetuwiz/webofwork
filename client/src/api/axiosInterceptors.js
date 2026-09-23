import axiosInstance from "./axiosInstance.js";
import store from "../app/store.js";

const publicPaths = new Set([
  "/auth/send-otp",
  "/auth/verify-otp",
  "/auth/login",
  "/auth/refresh",
  "/auth/forgot-password",
  "/auth/reset-password",
  "/auth/google",
]);

axiosInstance.interceptors.request.use((config) => {
  const path = new URL(config.url ?? "", config.baseURL).pathname;
  const isPublic = [...publicPaths].some((publicPath) => path.endsWith(publicPath));
  const token = store.getState().auth?.accessToken;

  if (token && !isPublic) {
    config.headers.Authorization = `Bearer ${token}`;
  }

  return config;
});
