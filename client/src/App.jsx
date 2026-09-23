import { Toaster } from "react-hot-toast";
import GlobalErrorBoundary from "./components/common/GlobalErrorBoundary.jsx";
import AppRoutes from "./routes/AppRoutes.jsx";
import AuthModalHost from "./features/client/account/components/AuthModalHost.jsx";

export default function App() {
  return (
    <GlobalErrorBoundary>
      <Toaster
        position="top-right"
        toastOptions={{
          duration: 4000,
          style: {
            borderRadius: "12px",
            background: "#0f172a",
            color: "#f8fafc",
            fontWeight: 600,
          },
        }}
      />
      <AppRoutes />
      <AuthModalHost />
    </GlobalErrorBoundary>
  );
}
