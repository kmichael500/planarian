import {
  Card,
  Form,
  Input,
  Button,
  InputNumber,
  Row,
  Col,
  Radio,
  ColProps,
} from "antd";
import { PlusOutlined } from "@ant-design/icons";
import { AddCaveVm } from "../Models/AddCaveVm";
import { DeleteButtonComponent } from "../../../Shared/Components/Buttons/DeleteButtonComponent";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { AddEntranceVm } from "../Models/AddEntranceVm";
import { FormInstance, RuleObject } from "antd/lib/form";
import { StateDropdown } from "./StateDropdown";
import { useState } from "react";
import { CountyDropdown } from "./CountyDropdown";
import { TagSelectComponent } from "../../Tag/Components/TagSelectComponent";
import { TagType } from "../../Tag/Models/TagType";
import { nameof } from "../../../Shared/Helpers/StringHelpers";

export interface AddCaveComponentProps {
  form: FormInstance<AddCaveVm>;
}
const AddCaveComponent = ({ form }: AddCaveComponentProps) => {
  const [selectedStateId, setSelectedStateId] = useState<string>();
  const [isInitialLoad, setIsInitialLoad] = useState<boolean>(true);
  const handlePrimaryEntranceChange = (index: number) => {
    form.setFieldsValue({
      entrances: form
        .getFieldValue("entrances")
        .map((entrance: AddEntranceVm, i: number) => ({
          ...entrance,
          isPrimary: i === index,
        })),
    });
  };

  const validateEntrances = async (
    rule: RuleObject,
    entrances: AddEntranceVm[]
  ) => {
    if (!entrances || entrances.length === 0) {
      throw new Error("At least one entrance is required");
    }
    const primaryEntrances = entrances.filter(
      (entrance) => entrance?.isPrimary
    );
    if (primaryEntrances.length === 0) {
      throw new Error("One entrance must be marked as the primary entrance");
    }
  };

  //#region  Column Props
  const fourColProps = {
    xxl: 6,
    xl: 6,
    lg: 12,
    md: 12,
    sm: 12,
    xs: 24,
  } as ColProps;

  const threeColProps = {
    xxl: 8,
    xl: 8,
    lg: 8,
    md: 8,
    sm: 24,
    xs: 24,
  } as ColProps;

  const twoColProps = {
    xxl: 12,
    xl: 12,
    lg: 12,
    md: 12,
    sm: 12,
    xs: 24,
  } as ColProps;
  //#endregion
  return (
    <Row style={{ marginBottom: 10 }} gutter={5}>
      <Form.Item name="id" noStyle>
        <Input type="hidden" />
      </Form.Item>
      <Col span={24}>
        <Form.Item
          label="Name"
          name={nameof<AddCaveVm>("name")}
          rules={[{ required: true, message: "Please enter a name" }]}
        >
          <Input />
        </Form.Item>
      </Col>

      <Col {...threeColProps}>
        <Form.Item
          label="State"
          name={nameof<AddCaveVm>("stateId")}
          rules={[{ required: true, message: "Please select a state" }]}
        >
          <StateDropdown
            autoSelectFirst={true}
            onChange={(e) => {
              setSelectedStateId(e.toString());
              if (isInitialLoad) return setIsInitialLoad(false);
              form.setFieldsValue({ countyId: undefined });
            }}
          />
        </Form.Item>
      </Col>
      <Col {...threeColProps}>
        <Form.Item
          label="County"
          name={nameof<AddCaveVm>("countyId")}
          rules={[{ required: true, message: "Please select a county" }]}
        >
          {selectedStateId && (
            <CountyDropdown selectedState={selectedStateId}></CountyDropdown>
          )}
        </Form.Item>
      </Col>

      <Col {...threeColProps}>
        <Form.Item label="Geology" name={nameof<AddCaveVm>("geologyTagIds")}>
          <TagSelectComponent
            tagType={TagType.Geology}
            projectId={""}
            mode="multiple"
          />
        </Form.Item>
      </Col>
      <Col {...fourColProps}>
        <Form.Item
          label="Length (Feet)"
          name={nameof<AddCaveVm>("lengthFeet")}
          rules={[
            {
              required: true,
              message: "Please enter the length in feet",
            },
          ]}
        >
          <InputNumber min={0} style={{ width: "100%" }} />
        </Form.Item>
      </Col>
      <Col {...fourColProps}>
        <Form.Item
          label="Depth (Feet)"
          name={nameof<AddCaveVm>("depthFeet")}
          rules={[
            { required: true, message: "Please enter the depth in feet" },
          ]}
        >
          <InputNumber min={0} style={{ width: "100%" }} />
        </Form.Item>
      </Col>
      <Col {...fourColProps}>
        <Form.Item
          label="Max Pit Depth (Feet)"
          name={nameof<AddCaveVm>("maxPitDepthFeet")}
          rules={[
            {
              required: true,
              message: "Please enter the max pit depth in feet",
            },
          ]}
        >
          <InputNumber min={0} style={{ width: "100%" }} />
        </Form.Item>
      </Col>
      <Col {...fourColProps}>
        <Form.Item
          label="Number of Pits"
          name={nameof<AddCaveVm>("numberOfPits")}
          rules={[
            {
              required: true,
              message: "Please enter the number of pits",
            },
          ]}
        >
          <InputNumber min={0} style={{ width: "100%" }} />
        </Form.Item>
      </Col>
      <Col span={24}>
        <Form.Item label="Narrative" name={nameof<AddCaveVm>("narrative")}>
          <Input.TextArea rows={10} />
        </Form.Item>
      </Col>
      <Col {...twoColProps}>
        <Form.Item label="Reported On" name={nameof<AddCaveVm>("reportedOn")}>
          <Input type="date" />
        </Form.Item>
      </Col>
      <Col {...twoColProps}>
        <Form.Item
          label="Reported By"
          name={nameof<AddCaveVm>("reportedByName")}
        >
          <Input />
        </Form.Item>
      </Col>

      <Col span={24}>
        <Form.List
          name={nameof<AddCaveVm>("entrances")}
          rules={[{ validator: validateEntrances }]}
        >
          {(fields, { add, remove }, { errors }) => (
            <>
              <Row gutter={16}>
                {fields.map((field, index) => (
                  <Col
                    key={field.name}
                    span={
                      fields.length % 2 === 1 && index === fields.length - 1
                        ? 24
                        : 12
                    }
                  >
                    <Card
                      key={field.name}
                      title={`Entrance ${index + 1}`}
                      style={{
                        marginBottom: "16px",
                        background: "#FCF5C7",
                      }}
                      extra={
                        <Form.Item>
                          <DeleteButtonComponent
                            title={`Are you sure you want to delete entrance ${(
                              field.name + 1
                            ).toString()}?`}
                            onConfirm={() => remove(field.name)}
                            okText="Yes"
                            cancelText="No"
                          ></DeleteButtonComponent>
                        </Form.Item>
                      }
                    >
                      <Row gutter={16}>
                        <Col span={24}>
                          <Form.Item
                            name={[
                              field.name,
                              nameof<AddEntranceVm>("isPrimary"),
                            ]}
                            rules={[]}
                          >
                            <Radio.Group
                              onChange={() =>
                                handlePrimaryEntranceChange(index)
                              }
                            >
                              <Radio value={true}>Primary Entrance</Radio>
                            </Radio.Group>
                          </Form.Item>
                        </Col>
                        <Col {...fourColProps}>
                          <Form.Item
                            {...field}
                            label="Status"
                            name={[
                              field.name,
                              nameof<AddEntranceVm>("entranceStatusTagIds"),
                            ]}
                          >
                            <TagSelectComponent
                              tagType={TagType.EntranceStatus}
                              mode="multiple"
                            />
                          </Form.Item>
                        </Col>
                        <Col {...fourColProps}>
                          <Form.Item
                            {...field}
                            label="Field Indication"
                            name={[
                              field.name,
                              nameof<AddEntranceVm>("fieldIndicationTagIds"),
                            ]}
                          >
                            <TagSelectComponent
                              tagType={TagType.FieldIndication}
                              mode="multiple"
                            />
                          </Form.Item>
                        </Col>
                        <Col {...fourColProps}>
                          <Form.Item
                            {...field}
                            label="Hydrology"
                            name={[
                              field.name,
                              nameof<AddEntranceVm>("entranceHydrologyTagIds"),
                            ]}
                          >
                            <TagSelectComponent
                              tagType={TagType.EntranceHydrology}
                              mode="multiple"
                            />
                          </Form.Item>
                        </Col>
                        <Col {...fourColProps}>
                          <Form.Item
                            {...field}
                            label="Hydrology Frequency"
                            name={[
                              field.name,
                              nameof<AddEntranceVm>(
                                "entranceHydrologyFrequencyTagIds"
                              ),
                            ]}
                          >
                            <TagSelectComponent
                              tagType={TagType.EntranceHydrologyFrequency}
                              mode="multiple"
                            />
                          </Form.Item>
                        </Col>
                        <Col {...fourColProps}>
                          <Form.Item
                            {...field}
                            label="Latitude"
                            name={[
                              field.name,
                              nameof<AddEntranceVm>("latitude"),
                            ]}
                            rules={[
                              {
                                required: true,
                                message: "Please enter the latitude",
                              },
                            ]}
                          >
                            <InputNumber
                              min={-90}
                              max={90}
                              step={0.0000001}
                              style={{ width: "100%" }}
                            />
                          </Form.Item>
                        </Col>

                        <Col {...fourColProps}>
                          <Form.Item
                            {...field}
                            label="Longitude"
                            name={[
                              field.name,
                              nameof<AddEntranceVm>("longitude"),
                            ]}
                            rules={[
                              {
                                required: true,
                                message: "Please enter the longitude",
                              },
                            ]}
                          >
                            <InputNumber
                              style={{ width: "100%" }}
                              min={-180}
                              max={180}
                              step={0.0000001}
                            />
                          </Form.Item>
                        </Col>
                        <Col {...fourColProps}>
                          <Form.Item
                            {...field}
                            label="Elevation (Feet)"
                            name={[
                              field.name,
                              nameof<AddEntranceVm>("elevationFeet"),
                            ]}
                            rules={[
                              {
                                required: true,
                                message: "Please enter the elevation in feet",
                              },
                            ]}
                          >
                            <InputNumber style={{ width: "100%" }} min={0} />
                          </Form.Item>
                        </Col>
                        <Col {...fourColProps}>
                          <Form.Item
                            {...field}
                            label="Location Quality"
                            name={[
                              field.name,
                              nameof<AddEntranceVm>("locationQualityTagId"),
                            ]}
                            rules={[
                              {
                                required: true,
                                message: "Please select a location quality tag",
                              },
                            ]}
                          >
                            <TagSelectComponent
                              mode={undefined}
                              tagType={TagType.LocationQuality}
                            />
                          </Form.Item>
                        </Col>
                        <Col span={24}>
                          <Form.Item
                            {...field}
                            label="Name"
                            name={[field.name, nameof<AddEntranceVm>("name")]}
                          >
                            <Input />
                          </Form.Item>
                        </Col>
                        <Col span={24}>
                          <Form.Item
                            {...field}
                            label="Pit (Feet)"
                            name={[
                              field.name,
                              nameof<AddEntranceVm>("pitFeet"),
                            ]}
                          >
                            <InputNumber style={{ width: "100%" }} min={0} />
                          </Form.Item>
                        </Col>
                        <Col span={24}>
                          <Form.Item
                            {...field}
                            label="Description"
                            name={[
                              field.name,
                              nameof<AddEntranceVm>("description"),
                            ]}
                          >
                            <Input.TextArea rows={4} />
                          </Form.Item>
                        </Col>

                        <Col {...twoColProps}>
                          <Form.Item
                            {...field}
                            label="Reported On"
                            name={[
                              field.name,
                              nameof<AddEntranceVm>("reportedOn"),
                            ]}
                          >
                            <Input type="date" />
                          </Form.Item>
                        </Col>
                        <Col {...twoColProps}>
                          <Form.Item
                            {...field}
                            label="Reported By Name"
                            name={[
                              field.name,
                              nameof<AddEntranceVm>("reportedByName"),
                            ]}
                          >
                            <Input />
                          </Form.Item>
                        </Col>
                      </Row>
                    </Card>
                  </Col>
                ))}
                <Col span={24}>
                  <Form.Item>
                    <PlanarianButton
                      onClick={() => add()}
                      icon={<PlusOutlined />}
                      block
                    >
                      Add Entrance
                    </PlanarianButton>
                  </Form.Item>
                </Col>
              </Row>
              {errors.length > 0 && (
                <div style={{ color: "red" }}>
                  {errors.map((error, index) => (
                    <span key={index}>{error}</span>
                  ))}
                </div>
              )}
            </>
          )}
        </Form.List>
      </Col>
      <Form.Item>
        <Button type="primary" htmlType="submit">
          Submit
        </Button>
      </Form.Item>
    </Row>
  );
};

export { AddCaveComponent };
