import React from "react";
import { Form, Checkbox, Col, Row } from "antd";
import { FeatureKey, FeatureSettingVm } from "../Models/FeatureSettingVm";
import { AccountService } from "../Services/AccountService";

const displayNameMap = {
  EnabledFieldCaveAlternateNames: "Alternate Names",
  EnabledFieldCaveLengthFeet: "Cave Length (Feet)",
  EnabledFieldCaveDepthFeet: "Cave Depth (Feet)",
  EnabledFieldCaveMaxPitDepthFeet: "Max Pit Depth (Feet)",
  EnabledFieldCaveNumberOfPits: "Number of Pits",
  EnabledFieldCaveNarrative: "Narrative",
  EnabledFieldCaveGeologyTags: "Geology Tags",
  EnabledFieldCaveMapStatusTags: "Map Status Tags",
  EnabledFieldCaveGeologicAgeTags: "Geologic Age Tags",
  EnabledFieldCavePhysiographicProvinceTags: "Physiographic Province Tags",
  EnabledFieldCaveBiologyTags: "Biology Tags",
  EnabledFieldCaveArcheologyTags: "Archeology Tags",
  EnabledFieldCaveCartographerNameTags: "Cartographer Name Tags",
  EnabledFieldCaveReportedByNameTags: "Reported By Name Tags",
  EnabledFieldCaveOtherTags: "Other Tags",
  EnabledFieldEntranceDescription: "Entrance Description",
  EnabledFieldEntranceStatusTags: "Entrance Status Tags",
  EnabledFieldEntranceHydrologyTags: "Hydrology Tags",
  EnabledFieldEntranceFieldIndicationTags: "Field Indication Tags",
  EnabledFieldEntranceReportedByNameTags: "Entrance Reported By Name Tags",
  EnabledFieldEntranceOtherTags: "Entrance Other Tags",
};

export const FeatureSettingsComponent = ({
  featureSettings,
  filterType,
}: {
  featureSettings: FeatureSettingVm[];
  filterType: "cave" | "entrance";
}) => {
  const [form] = Form.useForm<FeatureSettingVm>();

  const handleCheckboxChange = async (
    featureKey: FeatureKey,
    checked: boolean
  ) => {
    try {
      const response = await AccountService.CreateOrUpdateFeatureSetting(
        featureKey,
        checked
      );
      console.log("Service response:", response);
    } catch (error) {
      console.error("Error updating feature setting:", error);
    }
  };

  const filteredFields = featureSettings
    .filter((feature) =>
      filterType === "cave"
        ? feature.key.startsWith("EnabledFieldCave")
        : feature.key.startsWith("EnabledFieldEntrance")
    )
    .sort((a, b) => displayNameMap[a.key].localeCompare(displayNameMap[b.key]));

  return (
    <Form form={form}>
      <Row gutter={[8, 8]}>
        {filteredFields.map((feature) => (
          <Col key={feature.id} xs={24} sm={12} md={12} lg={12} xl={8}>
            <Form.Item
              name={feature.key}
              valuePropName="checked"
              initialValue={feature.isEnabled}
            >
              <Checkbox
                onChange={(e) =>
                  handleCheckboxChange(feature.key, e.target.checked)
                }
              >
                {displayNameMap[feature.key]}
              </Checkbox>
            </Form.Item>
          </Col>
        ))}
      </Row>
    </Form>
  );
};
