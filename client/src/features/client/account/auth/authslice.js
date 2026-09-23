import { createSlice } from "@reduxjs/toolkit";
import {
  fetchUser,
  loginThunk,
  logoutThunk,
  refreshSession,
} from "./authThunks.js";

const initialState = {
  user: null,
  accessToken: null,
  loading: false,
  initialized: false,
};

const authSlice = createSlice({
  name: "auth",
  initialState,
  reducers: {
    setCredentials: (state, action) => {
      state.user = action.payload.user ?? state.user;
      state.accessToken = action.payload.accessToken ?? state.accessToken;
      state.initialized = true;
      state.loading = false;
    },
    logout: () => ({ ...initialState, initialized: true }),
    finishLoading: (state) => {
      state.loading = false;
      state.initialized = true;
    },
    setUser: (state, action) => {
      state.user = action.payload;
    },
  },
  extraReducers: (builder) => {
    builder
      .addCase(loginThunk.pending, (state) => {
        state.loading = true;
      })
      .addCase(loginThunk.fulfilled, (state, action) => {
        state.user = action.payload.user;
        state.accessToken = action.payload.accessToken;
        state.loading = false;
        state.initialized = true;
      })
      .addCase(loginThunk.rejected, (state) => {
        state.loading = false;
        state.initialized = true;
      })
      .addCase(fetchUser.pending, (state) => {
        state.loading = true;
      })
      .addCase(fetchUser.fulfilled, (state, action) => {
        state.user = action.payload;
        state.loading = false;
        state.initialized = true;
      })
      .addCase(fetchUser.rejected, (state) => {
        state.loading = false;
        state.initialized = true;
      })
      .addCase(logoutThunk.fulfilled, () => ({ ...initialState, initialized: true }))
      .addCase(refreshSession.fulfilled, (state, action) => {
        state.accessToken = action.payload.accessToken;
        state.initialized = true;
      })
      .addCase(refreshSession.rejected, () => ({ ...initialState, initialized: true }));
  },
});

export const { finishLoading, logout, setCredentials, setUser } = authSlice.actions;
export default authSlice.reducer;
