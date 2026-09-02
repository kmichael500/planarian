import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { message } from "antd";
import { AccountService } from "../Services/AccountService";
import CreateEditCountiesComponent from "./CreateEditCountiesComponent";

const source = {
  tagTypeId: "county000a",
  name: "Source County",
  countyDisplayId: "A01",
  occurrences: 2,
  isUserModifiable: false,
};

const destination = {
  tagTypeId: "county000b",
  name: "Destination County",
  countyDisplayId: "A02",
  occurrences: 1,
  isUserModifiable: false,
};

beforeAll(() => {
  Object.defineProperty(window, "matchMedia", {
    writable: true,
    value: () => ({
      matches: false,
      addListener: () => undefined,
      removeListener: () => undefined,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
      dispatchEvent: () => false,
    }),
  });
});
describe("CreateEditCountiesComponent County merge", () => {
  let getCounties: jest.SpyInstance;
  let mergeCounties: jest.SpyInstance;
  let mergeTagTypes: jest.SpyInstance;
  let errorMessage: jest.SpyInstance;

  beforeEach(() => {
    getCounties = jest
      .spyOn(AccountService, "GetCountiesForTable")
      .mockResolvedValue([source, destination]);
    mergeCounties = jest
      .spyOn(AccountService, "MergeCounties")
      .mockResolvedValue();
    mergeTagTypes = jest
      .spyOn(AccountService, "MergeTagTypes")
      .mockResolvedValue();
    errorMessage = jest
      .spyOn(message, "error")
      .mockImplementation(() => undefined as never);
    jest.spyOn(message, "success").mockImplementation(() => undefined as never);
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  const openMerge = async () => {
    render(<CreateEditCountiesComponent stateId="state0000a" />);
    await screen.findByText("Source County");
    const rowCheckboxes = screen.getAllByRole("checkbox");
    expect(rowCheckboxes.length).toBeGreaterThanOrEqual(3);
    fireEvent.click(rowCheckboxes[1]);
    fireEvent.click(
      await screen.findByRole("button", { name: "Merge Selected" })
    );

    const destinationSelect = await screen.findByRole("combobox");
    fireEvent.mouseDown(destinationSelect);
    fireEvent.click(
      await screen.findByText("Destination County", {
        selector: ".ant-select-item-option-content",
      })
    );
    return screen.getByRole("button", { name: "OK" });
  };

  it("uses the County merge endpoint and refreshes authoritative County state", async () => {
    getCounties
      .mockResolvedValueOnce([source, destination])
      .mockResolvedValueOnce([
        { ...source, occurrences: 0, isUserModifiable: true },
        { ...destination, occurrences: 3 },
      ]);

    fireEvent.click(await openMerge());

    await waitFor(() =>
      expect(mergeCounties).toHaveBeenCalledWith(
        [source.tagTypeId],
        destination.tagTypeId,
        "state0000a"
      )
    );
    await waitFor(() => expect(getCounties).toHaveBeenCalledTimes(2));
    expect(mergeTagTypes).not.toHaveBeenCalled();
  });

  it("surfaces the server collision error without pretending the merge succeeded", async () => {
    mergeCounties.mockRejectedValue({
      message:
        "Cannot merge Counties because County Number 1 would be duplicated in the destination County.",
    });

    fireEvent.click(await openMerge());

    await waitFor(() =>
      expect(errorMessage).toHaveBeenCalledWith(
        "Cannot merge Counties because County Number 1 would be duplicated in the destination County."
      )
    );
    expect(getCounties).toHaveBeenCalledTimes(1);
    expect(mergeTagTypes).not.toHaveBeenCalled();
  });
});
