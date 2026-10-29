import { render, screen } from "@testing-library/react";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import { SettingsService } from "../../Setting/Services/SettingsService";
import { StateDropdown } from "./StateDropdown";

jest.mock("../../Setting/Services/SettingsService", () => ({
  SettingsService: {
    GetStates: jest.fn(),
  },
}));

const getStates = SettingsService.GetStates as jest.MockedFunction<
  typeof SettingsService.GetStates
>;

describe("StateDropdown permission filtering", () => {
  beforeEach(() => {
    getStates.mockReset();
    getStates.mockResolvedValue([]);
  });

  it("forwards the requested permission capability to the settings API", async () => {
    render(<StateDropdown permissionKey={PermissionKey.Manager} />);

    await screen.findByRole("combobox");
    expect(getStates).toHaveBeenCalledWith(PermissionKey.Manager);
  });

  it("keeps ordinary state dropdowns unfiltered", async () => {
    render(<StateDropdown />);

    await screen.findByRole("combobox");
    expect(getStates).toHaveBeenCalledWith(undefined);
  });
});
