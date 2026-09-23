import { createAsyncThunk } from "@reduxjs/toolkit";
import * as authApi from "../../../../api/auth/authApi.js";

const apiError = (error) =>
  error.response?.data ?? { error: error.message ?? "Request failed" };

export const loginThunk = createAsyncThunk(
  "auth/login",
  async (credentials, thunkApi) => {
    try {
      return await authApi.loginUser(credentials);
    } catch (error) {
      return thunkApi.rejectWithValue(apiError(error));
    }
  },
);

export const fetchUser = createAsyncThunk(
  "auth/fetchUser",
  async (_, thunkApi) => {
    try {
      const response = await authApi.getCurrentUser();
      return response.user ?? response;
    } catch (error) {
      return thunkApi.rejectWithValue(apiError(error));
    }
  },
);

export const logoutThunk = createAsyncThunk(
  "auth/logout",
  async (_, thunkApi) => {
    try {
      return await authApi.logoutUser();
    } catch (error) {
      return thunkApi.rejectWithValue(apiError(error));
    }
  },
);

export const refreshSession = createAsyncThunk(
  "auth/refresh",
  async (_, thunkApi) => {
    try {
      return await authApi.refreshToken();
    } catch (error) {
      return thunkApi.rejectWithValue(apiError(error));
    }
  },
);
