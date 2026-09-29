import { createAsyncThunk } from "@reduxjs/toolkit";
import * as authApi from "../../../../api/auth/authApi";
import { logout, setCredentials } from "./authslice";
import axiosInstance from "../../../../api/axiosInstance";
import { getErrorMessage } from "../../../../utils/errorMessage";


export const loginThunk = createAsyncThunk(
  "auth/login",
  async ({ email, password }, thunkAPI) => {
    try {
      const res = await authApi.loginUser({ email, password });
      return res; 

    } catch (err) {
      return thunkAPI.rejectWithValue(err.response?.data);
    }
  }
);

export const fetchUser = createAsyncThunk(
  "auth/fetchUser",
  async (_, thunkAPI) => {
    try {
      const res = await authApi.getCurrentUser();
      
      return res.user;
    } catch (error) {
      // `getErrorMessage` rather than `error.response.data.message`.
      //
      // The unguarded `error.response.data` threw a TypeError whenever the
      // request never reached the server -- offline, DNS failure, timeout -- which
      // is precisely the case where the user most needs a readable message. It
      // also meant the intended text was only ever reached when `error.message`
      // happened to be empty.
      return thunkAPI.rejectWithValue(
        getErrorMessage(error, "Something went wrong"),
      );
    }
  }
);

export const logoutThunk = createAsyncThunk(
  "auth/logout",
  async (_, thunkAPI) => {
    try {
      await authApi.logoutUser();          
      thunkAPI.dispatch(logout()); 
    } catch (err) {
      thunkAPI.dispatch(logout());
    }
  }
);

export const requestDeleteOTP = createAsyncThunk(
  "auth/requestDeleteOTP",
  async (_, { rejectWithValue }) => {
    try {
      const response = await axiosInstance.post("/auth/request-delete-otp/");
      return response.data;
    } catch (err) {
      return rejectWithValue(err.response.data);
    }
  }
);

export const deleteAccount = createAsyncThunk(
  "auth/deleteAccount",
  async (credentials, { rejectWithValue }) => {
    try {
      const response = await axiosInstance.post("/auth/delete-account/", credentials);
      return response.data;
    } catch (err) {
      return rejectWithValue(err.response.data);
    }
  }
);
export const refreshSession = createAsyncThunk(
  "auth/refresh",
  async (_, thunkAPI) => {
    try {
      // Relies on the HttpOnly `refresh_token` cookie, which is why it is a bare
      // POST with no body: the token is deliberately not readable by JS.
      const res = await authApi.refreshToken();

      // The refresh endpoint answers with an access token and nothing else, so
      // the user object has to be fetched separately. The token is committed to
      // the store *before* that request goes out, because the request interceptor
      // reads it off the store synchronously when building the header.
      thunkAPI.dispatch(setCredentials({ accessToken: res.access }));
      const user = await thunkAPI.dispatch(fetchUser()).unwrap();

      return { access: res.access, user };
    } catch (err) {
      return thunkAPI.rejectWithValue(
        getErrorMessage(err, "Your session has expired."),
      );
    }
  }
);
