import React from "react";

interface LazyLoadErrorBoundaryProps {
  fallback?: React.ReactNode;
  renderFallback?: (error: Error) => React.ReactNode;
}

interface LazyLoadErrorBoundaryState {
  error: Error | null;
}

export class LazyLoadErrorBoundary extends React.Component<
  React.PropsWithChildren<LazyLoadErrorBoundaryProps>,
  LazyLoadErrorBoundaryState
> {
  state: LazyLoadErrorBoundaryState = { error: null };

  static getDerivedStateFromError(error: Error): LazyLoadErrorBoundaryState {
    return { error };
  }

  componentDidCatch(error: Error, errorInfo: React.ErrorInfo): void {
    console.error("Lazy-loaded content failed.", error, errorInfo);
  }

  render(): React.ReactNode {
    if (!this.state.error) return this.props.children;
    return this.props.renderFallback?.(this.state.error) ?? this.props.fallback ?? null;
  }
}
