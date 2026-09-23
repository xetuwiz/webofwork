import { configureStore } from "@reduxjs/toolkit";
import { combineReducers } from "redux";
import { persistReducer, persistStore } from "redux-persist";
import storage from "redux-persist/lib/storage";
import authReducer from "../features/client/account/auth/authslice.js";

const rootReducer = combineReducers({
  auth: persistReducer(
    {
      key: "auth",
      storage,
      whitelist: ["user", "accessToken"],
    },
    authReducer,
  ),
});

const resettableRootReducer = (state, action) => {
  if (action.type === "auth/logout" || action.type === "auth/logout/fulfilled") {
    storage.removeItem("persist:auth");
    state = undefined;
  }
  return rootReducer(state, action);
};

const store = configureStore({
  reducer: resettableRootReducer,
  middleware: (getDefaultMiddleware) =>
    getDefaultMiddleware({
      serializableCheck: false,
    }),
});

export const persistor = persistStore(store);
export default store;
