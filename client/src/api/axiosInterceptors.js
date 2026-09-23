import axiosInstance from "./axiosInstance.js";
import store from "../app/store.js";
import { logout, setCredentials } from "../features/client/account/auth/authslice.js";

const publicPaths = new Set([
  "/auth/send-otp",
  "/auth/verify-otp",
  "/auth/login",
  "/auth/refresh",
  "/auth/forgot-password",
  "/auth/reset-password",
  "/auth/google",
]);

const isPublicEndpoint = (url = "") => {
  try {
    const target = new URL(url, axiosInstance.defaults.baseURL ?? "https://localhost:5001/api");
    return [...publicPaths].some((publicPath) => target.pathname.endsWith(publicPath));
  } catch {
    return [...publicPaths].some((publicPath) => url.endsWith(publicPath));
  }
};

axiosInstance.interceptors.request.use((config) => {
  const requestUrl = config.url ?? "";
  const token = store.getState().auth?.accessToken;

  if (token && !isPublicEndpoint(requestUrl)) {
    config.headers = {
      ...config.headers,
      Authorization: `Bearer ${token}`,
    };
  }

  return config;
});

let isRefreshing = false;
let refreshWaiters = [];

axiosInstance.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config;

    if (!originalRequest || !error.response || isPublicEndpoint(originalRequest.url ?? "")) {
      return Promise.reject(error);
    }

    if (error.response.status === 401 && !originalRequest._retry) {
      if (isRefreshing) {
        return new Promise((resolve, reject) => {
          refreshWaiters.push({ resolve, reject });
        })
          .then(() => axiosInstance(originalRequest))
          .catch((retryError) => Promise.reject(retryError));
      }

      originalRequest._retry = true;
      isRefreshing = true;

      try {
        const refreshResponse = await axiosInstance.post("/auth/refresh", {}, { withCredentials: true });
        const nextToken = refreshResponse.data?.accessToken ?? refreshResponse.data?.token;

        if (!nextToken) {
          throw new Error("Refresh response did not include a new access token.");
        }

        store.dispatch(setCredentials({ accessToken: nextToken }));
        originalRequest.headers.Authorization = `Bearer ${nextToken}`;

        refreshWaiters.forEach(({ resolve }) => resolve());
        refreshWaiters = [];

        return axiosInstance(originalRequest);
      } catch (refreshError) {
        refreshWaiters.forEach(({ reject }) => reject(refreshError));
        refreshWaiters = [];
        store.dispatch(logout());
        return Promise.reject(refreshError);
      } finally {
        isRefreshing = false;
      }
    }

    return Promise.reject(error);
  },
);
