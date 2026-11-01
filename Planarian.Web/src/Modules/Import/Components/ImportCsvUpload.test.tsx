import React from "react";
import { MemoryRouter } from "react-router-dom";
import { act, render } from "@testing-library/react";
import { AccountService } from "../../Account/Services/AccountService";
import { ImportCaveComponent } from "./ImportCaves";
import { ImportEntrancesComponent } from "./ImportEntrances";

let mockUploadSource: ((args: {
  file: File;
  uid: string;
  onProgress: () => void;
}) => Promise<unknown>) | undefined;

jest.mock("../../Files/Components/UploadComponent", () => ({
  UploadComponent: ({ uploadFunction }: { uploadFunction: typeof mockUploadSource }) => {
    mockUploadSource = uploadFunction;
    return <div data-testid="csv-upload-control" />;
  },
}));

jest.mock("../../Account/Services/AccountService", () => ({
  AccountService: {
    ImportCavesFile: jest.fn(),
    ImportEntrancesFile: jest.fn(),
  },
}));

beforeAll(() => {
  Object.defineProperty(window, "matchMedia", {
    writable: true,
    value: (query: string) => ({
      matches: false, media: query, onchange: null,
      addListener: () => undefined, removeListener: () => undefined,
      addEventListener: () => undefined, removeEventListener: () => undefined,
      dispatchEvent: () => false,
    }),
  });
});

describe.each([
  ["Cave", ImportCaveComponent, "ImportCavesFile"],
  ["Entrance", ImportEntrancesComponent, "ImportEntrancesFile"],
] as const)("%s CSV upload", (_label, Component, method) => {
  beforeEach(() => {
    mockUploadSource = undefined;
    jest.clearAllMocks();
  });

  it("rejects a failed upload rather than passing a fake source to the uploader", async () => {
    const failure = new Error("Upload rejected");
    (AccountService[method] as jest.Mock).mockRejectedValue(failure);
    render(<MemoryRouter><Component onUploaded={jest.fn()} /></MemoryRouter>);
    expect(mockUploadSource).toBeDefined();

    let promise!: Promise<unknown>;
    await act(async () => {
      promise = mockUploadSource!({
        file: new File(["test"], "test.csv", { type: "text/csv" }),
        uid: "test-1",
        onProgress: () => undefined,
      });
      await expect(promise).rejects.toBe(failure);
    });
  });

  it("returns the actual source response to the uploader", async () => {
    const result = { id: "_source001", fileName: "test.csv", uuid: "test-2" };
    (AccountService[method] as jest.Mock).mockResolvedValue(result);
    render(<MemoryRouter><Component onUploaded={jest.fn()} /></MemoryRouter>);
    expect(mockUploadSource).toBeDefined();

    await act(async () => {
      await expect(mockUploadSource!({
        file: new File(["test"], "test.csv", { type: "text/csv" }),
        uid: "test-2",
        onProgress: () => undefined,
      })).resolves.toBe(result);
    });
  });
});
