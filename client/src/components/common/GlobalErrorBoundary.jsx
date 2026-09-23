import { Component } from "react";

export default class GlobalErrorBoundary extends Component {
  constructor(props) {
    super(props);
    this.state = { hasError: false, error: null };
  }

  static getDerivedStateFromError(error) {
    return { hasError: true, error };
  }

  componentDidCatch(error, errorInfo) {
    console.error("Unhandled app error:", error, errorInfo);
  }

  handleReset = () => {
    this.setState({ hasError: false, error: null });
    window.location.reload();
  };

  render() {
    if (this.state.hasError) {
      return (
        <div style={{
          minHeight: "100vh",
          display: "grid",
          placeItems: "center",
          padding: "2rem",
          background: "#f8fafc",
          color: "#0f172a",
          fontFamily: "Inter, sans-serif",
        }}>
          <div style={{
            maxWidth: "560px",
            width: "100%",
            border: "1px solid #e2e8f0",
            borderRadius: "20px",
            background: "#fff",
            boxShadow: "0 18px 40px rgba(15, 23, 42, 0.08)",
            padding: "2rem",
          }}>
            <div style={{ marginBottom: "1rem", color: "#dc2626", fontWeight: 700, letterSpacing: "0.08em", textTransform: "uppercase", fontSize: "0.72rem" }}>
              Something went wrong
            </div>
            <h1 style={{ margin: "0 0 0.75rem", fontSize: "2rem" }}>The app hit an unexpected error.</h1>
            <p style={{ margin: "0 0 1.25rem", color: "#475569", lineHeight: 1.6 }}>
              Please refresh the page or try again. If the issue continues, contact support.
            </p>
            <button
              type="button"
              onClick={this.handleReset}
              style={{
                border: "none",
                borderRadius: "10px",
                background: "#0f172a",
                color: "#fff",
                padding: "0.8rem 1.1rem",
                cursor: "pointer",
                fontWeight: 700,
              }}
            >
              Reload app
            </button>
          </div>
        </div>
      );
    }

    return this.props.children;
  }
}
