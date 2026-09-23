import React from "react";
import ReactDOM from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { Provider } from "react-redux";
import App from "./App.jsx";
import { PersistGate } from "redux-persist/integration/react";
import { ModalProvider } from "./hooks/modal/useModalStore.jsx";
import store, { persistor } from "./app/store.js";
import "./api/axiosInterceptors.js";
import "./fonts.css";
import "./index.css";

const queryClient = new QueryClient();

ReactDOM.createRoot(document.getElementById("root")).render(
  <React.StrictMode>
    <Provider store={store}>
      <PersistGate persistor={persistor} loading={null}>
        <ModalProvider>
          <QueryClientProvider client={queryClient}>
            <App />
          </QueryClientProvider>
        </ModalProvider>
      </PersistGate>
    </Provider>
  </React.StrictMode>,
);
