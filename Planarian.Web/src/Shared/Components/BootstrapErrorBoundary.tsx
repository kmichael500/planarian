import React from "react";

interface BootstrapErrorBoundaryState {
  hasError: boolean;
}

export class BootstrapErrorBoundary extends React.Component<
  React.PropsWithChildren,
  BootstrapErrorBoundaryState
> {
  state: BootstrapErrorBoundaryState = { hasError: false };

  static getDerivedStateFromError(): BootstrapErrorBoundaryState {
    return { hasError: true };
  }

  componentDidCatch(error: Error, errorInfo: React.ErrorInfo): void {
    console.error("Planarian failed to start.", error, errorInfo);
  }

  render(): React.ReactNode {
    if (this.state.hasError) {
      return (
        <main role="alert" style={{ padding: "2rem", fontFamily: "sans-serif" }}>
          <h1>Planarian failed to start</h1>
          <p>Please reload the page and try again.</p>
        </main>
      );
    }
    return this.props.children;
  }
}
