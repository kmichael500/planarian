import { render, screen } from "@testing-library/react";
import React from "react";
import { FeatureKey, FeatureSettingVm } from "../Models/FeatureSettingVm";
import { EnabledFieldsComponent } from "./EnabledFieldsComponent";

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

describe("EnabledFieldsComponent", () => {
  it("ignores unknown server feature keys instead of crashing the entrance tab", () => {
    const featureSettings: FeatureSettingVm[] = [
      {
        id: "known",
        key: FeatureKey.EnabledFieldEntranceDescription,
        isEnabled: true,
        isDefault: false,
      },
      {
        id: "unknown",
        key: "EnabledFieldEntranceOtherTags" as FeatureKey,
        isEnabled: true,
        isDefault: false,
      },
    ];

    const { rerender } = render(
      <EnabledFieldsComponent featureSettings={[]} filterType="entrance" />
    );

    rerender(
      <EnabledFieldsComponent
        featureSettings={featureSettings}
        filterType="entrance"
      />
    );

    const description = screen.getByRole("checkbox", {
      name: "Description",
    });

    expect(description).toBeChecked();
    expect(screen.getAllByRole("checkbox")).toHaveLength(1);
  });
});
