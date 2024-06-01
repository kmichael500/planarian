import React, { useState } from "react";
import { Form, Checkbox, Col, Row, message } from "antd";
import { FeatureKey, FeatureSettingVm } from "../Models/FeatureSettingVm";
import { AccountService } from "../Services/AccountService";
import { PlanarianError } from "../../../Shared/Exceptions/PlanarianErrors";

const displayNameMap: { [key in FeatureKey]: string } = {
  EnabledFieldCaveId: "ID",
  EnabledFieldCaveName: "Name",
  EnabledFieldCaveAlternateNames: "Alternate Names",
  EnabledFieldCaveLengthFeet: "Length",
  EnabledFieldCaveDepthFeet: "Depth",
  EnabledFieldCaveMaxPitDepthFeet: "Max Pit Depth",
  EnabledFieldCaveNumberOfPits: "Number of Pits",
  EnabledFieldCaveNarrative: "Narrative",
  EnabledFieldCaveGeologyTags: "Geology",
  EnabledFieldCaveMapStatusTags: "Map Status",
  EnabledFieldCaveGeologicAgeTags: "Geologic Age",
  EnabledFieldCavePhysiographicProvinceTags: "Physiographic Province",
  EnabledFieldCaveBiologyTags: "Biology",
  EnabledFieldCaveArcheologyTags: "Archeology",
  EnabledFieldCaveCartographerNameTags: "Cartographers",
  EnabledFieldCaveReportedByNameTags: "Reported By",
  EnabledFieldCaveOtherTags: "Other",
  EnabledFieldCaveState: "State",
  EnabledFieldCaveCounty: "County",
  EnabledFieldCaveReportedOn: "Reported On",

  EnabledFieldEntranceCoordinates: "Coordinates",
  EnabledFieldEntranceDescription: "Description",
  EnabledFieldEntranceElevation: "Elevation",
  EnabledFieldEntranceLocationQuality: "Location Quality",
  EnabledFieldEntranceName: "Name",
  EnabledFieldEntranceReportedOn: "Reported On",
  EnabledFieldEntrancePitDepth: "Pit Depth",
  EnabledFieldEntranceStatusTags: "Status",
  EnabledFieldEntranceHydrologyTags: "Hydrology",
  EnabledFieldEntranceFieldIndicationTags: "Field Indication",
  EnabledFieldEntranceReportedByNameTags: "Reported By",
};

export const FeatureSettingsComponent = ({
  featureSettings,
  filterType,
}: {
  featureSettings: FeatureSettingVm[];
  filterType: "cave" | "entrance";
}) => {
  const [form] = Form.useForm<FeatureSettingVm>();
  const [initialSettings, setInitialSettings] = useState(new Map());

  const handleCheckboxChange = async (
    featureKey: FeatureKey,
    checked: boolean
  ) => {
    const originalValue = form.getFieldValue(featureKey);
    try {
      const response = await AccountService.CreateOrUpdateFeatureSetting(
        featureKey,
        checked
      );
      // Assuming a successful response would confirm the setting was updated
    } catch (e) {
      const error = e as PlanarianError;
      message.error(error.message);
      // Revert the checkbox to the original value
      form.setFieldsValue({ [featureKey]: originalValue });
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
    <Form
      form={form}
      initialValues={featureSettings.reduce<{ [key in FeatureKey]?: boolean }>(
        (acc, feature) => {
          acc[feature.key] = feature.isEnabled;
          return acc;
        },
        {}
      )}
    >
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
                disabled={feature.isDefault}
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
