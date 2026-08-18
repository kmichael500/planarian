import { lazy, Suspense } from "react";
import { render, screen } from "@testing-library/react";
import { LazyLoadErrorBoundary } from "./LazyLoadErrorBoundary";

const BrokenChild = () => {
  throw new Error("viewer render failed");
};

afterEach(() => {
  jest.restoreAllMocks();
});

it("contains a rejected lazy import and renders the supplied fallback", async () => {
  jest.spyOn(console, "error").mockImplementation(() => undefined);
  const LazyBrokenChild = lazy(() =>
    Promise.reject(new Error("lazy viewer failed"))
  );

  render(
    <LazyLoadErrorBoundary fallback={<div>Viewer could not be loaded</div>}>
      <Suspense fallback={<div>Loading viewer</div>}>
        <LazyBrokenChild />
      </Suspense>
    </LazyLoadErrorBoundary>
  );

  expect(await screen.findByText("Viewer could not be loaded")).toBeInTheDocument();
});

it("passes a captured render error to the fallback renderer", () => {
  jest.spyOn(console, "error").mockImplementation(() => undefined);

  render(
    <LazyLoadErrorBoundary renderFallback={error => <div>{`${error.name}: ${error.message}`}</div>}>
      <BrokenChild />
    </LazyLoadErrorBoundary>
  );

  expect(screen.getByText("Error: viewer render failed")).toBeInTheDocument();
});

it("renders children normally when they do not fail", () => {
  render(
    <LazyLoadErrorBoundary fallback={<div>Viewer could not be loaded</div>}>
      <div>Viewer loaded</div>
    </LazyLoadErrorBoundary>
  );

  expect(screen.getByText("Viewer loaded")).toBeInTheDocument();
});
