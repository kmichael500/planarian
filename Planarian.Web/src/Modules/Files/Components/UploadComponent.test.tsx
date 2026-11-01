import React from "react";
import { fireEvent, render, waitFor } from "@testing-library/react";
import { message } from "antd";
import { UploadComponent } from "./UploadComponent";

const mockUploadSuccess = jest.fn();
const mockUploadError = jest.fn();
const mockUploadProgress = jest.fn();

jest.mock("antd/lib/upload/Dragger", () => ({
  __esModule: true,
  default: ({ customRequest }: { customRequest: (options: unknown) => Promise<void> }) => (
    <button
      type="button"
      onClick={() => {
        void customRequest({
          file: { uid: "upload-1", name: "test.csv" },
          onSuccess: mockUploadSuccess,
          onError: mockUploadError,
          onProgress: mockUploadProgress,
        });
      }}
    >
      Upload CSV
    </button>
  ),
}));

beforeAll(() => {
  Object.defineProperty(window, "matchMedia", {
    writable: true,
    value: (query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addListener: () => undefined,
      removeListener: () => undefined,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
      dispatchEvent: () => false,
    }),
  });
});

jest.mock("../../Tag/Components/TagSelectComponent", () => ({
  TagSelectComponent: () => <span data-testid="tag-selector" />,
}));

describe("UploadComponent", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    jest.spyOn(message, "success").mockImplementation(jest.fn() as any);
    jest.spyOn(message, "error").mockImplementation(jest.fn() as any);
  });

  afterEach(() => jest.restoreAllMocks());

  it("never forwards a non-finite progress percentage when total size is unknown", async () => {
    const uploadFunction = jest.fn(async ({ onProgress }) => {
      onProgress({ loaded: 64, total: 0 });
      onProgress({ loaded: 64 });
      onProgress({ loaded: 25, total: 100 });
      onProgress({ loaded: 200, total: 100 });
      return { id: "abc", name: "Survey", fileTypeTagId: "map" };
    });
    const { getByRole } = render(
      <UploadComponent uploadFunction={uploadFunction} updateFunction={jest.fn()} singleFile />
    );

    fireEvent.click(getByRole("button", { name: "Upload CSV" }));

    await waitFor(() => expect(mockUploadProgress).toHaveBeenCalledTimes(4));
    expect(mockUploadProgress.mock.calls.map(([value]) => value)).toEqual([
      { percent: 0 },
      { percent: 0 },
      { percent: 25 },
      { percent: 100 },
    ]);
  });

  it("reports a completed successful upload and forwards its completion", async () => {
    const uploadFunction = jest.fn().mockResolvedValue({
      id: "abc", name: "Survey", fileTypeTagId: "map",
    });
    const updateFunction = jest.fn().mockResolvedValue(undefined);
    const { getByRole } = render(
      <UploadComponent
        uploadFunction={uploadFunction}
        updateFunction={updateFunction}
        singleFile
      />
    );

    fireEvent.click(getByRole("button", { name: "Upload CSV" }));

    await waitFor(() => expect(mockUploadSuccess).toHaveBeenCalledTimes(1));
    await waitFor(() =>
      expect(message.success).toHaveBeenCalledWith(
        "All files have been uploaded successfully!"
      )
    );
    expect(mockUploadError).not.toHaveBeenCalled();

    // A completed upload must populate the edit form with its actual DTO.
    const nameField = await waitFor(() =>
      getByRole("textbox", { name: "Name" }) as HTMLInputElement
    );
    expect(nameField.value).toBe("Survey");
    fireEvent.change(nameField, { target: { value: "Updated survey" } });
    fireEvent.click(getByRole("button", { name: "Save Changes" }));
    await waitFor(() =>
      expect(updateFunction).toHaveBeenCalledWith([
        expect.objectContaining({
          id: "abc", name: "Updated survey", fileTypeTagId: "map",
        }),
      ])
    );
  });

  it("does not report an unsuccessful upload as successful", async () => {
    const uploadFunction = jest.fn().mockRejectedValue(new Error("Upload failed"));
    const { getByRole } = render(
      <UploadComponent
        uploadFunction={uploadFunction}
        updateFunction={jest.fn()}
        singleFile
      />
    );

    fireEvent.click(getByRole("button", { name: "Upload CSV" }));

    await waitFor(() => expect(mockUploadError).toHaveBeenCalledTimes(1));
    expect(mockUploadSuccess).not.toHaveBeenCalled();
    expect(mockUploadError).toHaveBeenCalledWith(
      expect.objectContaining({ message: "Upload failed" })
    );
    expect(message.success).not.toHaveBeenCalled();
    expect(getByRole("button", { name: "Upload CSV" })).toBeInTheDocument();
  });
});
