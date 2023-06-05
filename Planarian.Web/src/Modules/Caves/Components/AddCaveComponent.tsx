import {
  Card,
  Typography,
  Form,
  Input,
  Button,
  Select,
  InputNumber,
  Row,
  Col,
  Radio,
} from "antd";
import { PlusOutlined } from "@ant-design/icons";
import { AddCaveVm } from "../Models/AddCaveVm";
import { DeleteButtonComponent } from "../../../Shared/Components/Buttons/DeleteButtonComponent";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { AddEntranceVm } from "../Models/AddEntranceVm";
import { RuleObject } from "antd/lib/form";
import { StateDropdown } from "./StateDropdown";
import { useEffect, useState } from "react";
import { CountyDropdown } from "./CountyDropdown";
const { Title } = Typography;
const { Option } = Select;

const AddCaveComponent: React.FC = () => {
  const [form] = Form.useForm<AddCaveVm>();
  const [selectedStateId, setSelectedStateId] = useState<string>();
  const initialValues: AddCaveVm = {
    name: "",
    countyId: "",
    lengthFeet: 0,
    depthFeet: 0,
    maxPitDepthFeet: null,
    numberOfPits: 0,
    narrative: null,
    reportedOn: null,
    reportedByName: null,
    entrances: [
      {
        isPrimary: true,
        locationQualityTagId: "",
        name: null,
        description: null,
        latitude: 0,
        longitude: 0,
        elevationFeet: 0,
        reportedOn: null,
        reportedByName: null,
        pitFeet: null,
        entranceStatusTagIds: [],
        entranceHydrologyFrequencyTagIds: [],
        fieldIndicationTagIds: [],
        entranceHydrologyTagIds: [],
      },
    ],
    geologyTagIds: [],
  };

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

  useEffect(() => {
    form.setFieldsValue({ countyId: undefined });
  }, [selectedStateId, form]);

  const handleFormSubmit = (values: AddCaveVm) => {
    // Handle form submission logic here
    console.log(values);
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
      throw new Error(
        "At least one entrance must be marked as the primary entrance"
      );
    }
  };
  return (
    <>
      <Card>
        <Form<AddCaveVm>
          layout="vertical"
          initialValues={initialValues}
          onFinish={handleFormSubmit}
          form={form}
        >
          <Form.Item
            label="Name"
            name="name"
            rules={[{ required: true, message: "Please enter a name" }]}
          >
            <Input />
          </Form.Item>

          <Form.Item
            label="State"
            name="stateId"
            rules={[{ required: true, message: "Please select a state" }]}
          >
            <StateDropdown
              onChange={(e) => {
                setSelectedStateId(e.toString());
              }}
            />
          </Form.Item>
          <Form.Item
            label="County ID"
            name="countyId"
            rules={[{ required: true, message: "Please select a county" }]}
          >
            {selectedStateId && (
              <CountyDropdown selectedState={selectedStateId}></CountyDropdown>
            )}
          </Form.Item>

          <Form.Item
            label="Length (Feet)"
            name="lengthFeet"
            rules={[
              { required: true, message: "Please enter the length in feet" },
            ]}
          >
            <InputNumber min={0} />
          </Form.Item>

          <Form.Item
            label="Depth (Feet)"
            name="depthFeet"
            rules={[
              { required: true, message: "Please enter the depth in feet" },
            ]}
          >
            <InputNumber min={0} />
          </Form.Item>

          <Form.Item label="Max Pit Depth (Feet)" name="maxPitDepthFeet">
            <InputNumber min={0} />
          </Form.Item>

          <Form.Item
            label="Number of Pits"
            name="numberOfPits"
            rules={[
              { required: true, message: "Please enter the number of pits" },
            ]}
          >
            <InputNumber min={0} />
          </Form.Item>

          <Form.Item label="Narrative" name="narrative">
            <Input.TextArea rows={4} />
          </Form.Item>

          <Form.Item label="Reported On" name="reportedOn">
            <Input type="date" />
          </Form.Item>

          <Form.Item label="Reported By Name" name="reportedByName">
            <Input />
          </Form.Item>

          {/* AddCaveVm entrances field */}
          <Form.List
            name="entrances"
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
                        <Form.Item name={[field.name, "isPrimary"]} rules={[]}>
                          <Radio.Group
                            onChange={() => handlePrimaryEntranceChange(index)}
                          >
                            <Radio value={true}>Primary Entrance</Radio>
                          </Radio.Group>
                        </Form.Item>
                        <Form.Item
                          {...field}
                          label="Name"
                          name={[field.name, "name"]}
                        >
                          <Input />
                        </Form.Item>
                        <Form.Item
                          {...field}
                          label="Description"
                          name={[field.name, "description"]}
                        >
                          <Input.TextArea rows={4} />
                        </Form.Item>
                        <Form.Item
                          {...field}
                          label="Latitude"
                          name={[field.name, "latitude"]}
                          rules={[
                            {
                              required: true,
                              message: "Please enter the latitude",
                            },
                          ]}
                        >
                          <InputNumber min={-90} max={90} />
                        </Form.Item>

                        <Form.Item
                          {...field}
                          label="Longitude"
                          name={[field.name, "longitude"]}
                          rules={[
                            {
                              required: true,
                              message: "Please enter the longitude",
                            },
                          ]}
                        >
                          <InputNumber min={-180} max={180} />
                        </Form.Item>
                        <Form.Item
                          {...field}
                          label="Elevation (Feet)"
                          name={[field.name, "elevationFeet"]}
                          rules={[
                            {
                              required: true,
                              message: "Please enter the elevation in feet",
                            },
                          ]}
                        >
                          <InputNumber min={0} />
                        </Form.Item>
                        <Form.Item
                          {...field}
                          label="Location Quality Tag ID"
                          name={[field.name, "locationQualityTagId"]}
                          rules={[
                            {
                              required: true,
                              message: "Please select a location quality tag",
                            },
                          ]}
                        >
                          <Select>
                            <Option value={"hi"}>Test</Option>
                          </Select>
                        </Form.Item>

                        <Form.Item
                          {...field}
                          label="Reported On"
                          name={[field.name, "reportedOn"]}
                        >
                          <Input type="date" />
                        </Form.Item>

                        <Form.Item
                          {...field}
                          label="Reported By Name"
                          name={[field.name, "reportedByName"]}
                        >
                          <Input />
                        </Form.Item>

                        <Form.Item
                          {...field}
                          label="Pit (Feet)"
                          name={[field.name, "pitFeet"]}
                        >
                          <InputNumber min={0} />
                        </Form.Item>
                      </Card>
                    </Col>
                  ))}
                  <Col span={24}>
                    <Form.Item>
                      <PlanarianButton
                        // type="primary"
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

          {/* Add geologyTagIds field */}
          <Form.Item label="Geology Tag IDs" name="geologyTagIds">
            <Select mode="multiple">
              <Option value={"hi"}>Test</Option>
            </Select>
          </Form.Item>

          <Form.Item>
            <Button type="primary" htmlType="submit">
              Submit
            </Button>
          </Form.Item>
        </Form>
      </Card>
    </>
  );
};

export { AddCaveComponent };
