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
  Collapse,
  Space,
  Select,
} from "antd";
import { PlusOutlined } from "@ant-design/icons";
import { AddCaveVm } from "../Models/AddCaveVm";
import { DeleteButtonComponent } from "../../../Shared/Components/Buttons/DeleteButtonComponent";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { AddEntranceVm } from "../Models/AddEntranceVm";
import { FormInstance, RuleObject } from "antd/lib/form";
import { StateDropdown } from "./StateDropdown";
import { useEffect, useState } from "react";
import { CountyDropdown } from "./CountyDropdown";
import { TagSelectComponent } from "../../Tag/Components/TagSelectComponent";
import { TagType } from "../../Tag/Models/TagType";
import {
  isNullOrWhiteSpace,
  nameof,
} from "../../../Shared/Helpers/StringHelpers";
import { InputDistanceComponent } from "../../../Shared/Components/Inputs/InputDistance";
import { EditFileMetadataVm } from "../../Files/Models/EditFileMetadataVm";
import { groupBy } from "../../../Shared/Helpers/ArrayHelpers";
import { PlanarianDividerComponent } from "../../../Shared/Components/PlanarianDivider/PlanarianDividerComponent";

export interface AddCaveComponentProps {
  form: FormInstance<AddCaveVm>;
  isEditing?: boolean;
  cave?: AddCaveVm;
}
const AddCaveComponent = ({ form, isEditing, cave }: AddCaveComponentProps) => {
  const [selectedStateId, setSelectedStateId] = useState<string>();
  const [isInitialLoad, setIsInitialLoad] = useState<boolean>(true);

  const [caveState, setCaveState] = useState<AddCaveVm>(
    cave ?? ({} as AddCaveVm)
  );

  const handlePrimaryEntranceChange = (index: number) => {
    form.setFieldsValue({
      entrances: form
        .getFieldValue(nameof<AddCaveVm>("entrances"))
        .map((entrance: AddEntranceVm, i: number) => ({
          ...entrance,
          isPrimary: i === index,
        })),
    });
  };

  useEffect(() => {
    // if stateId has a value in the form, set the selectedStateId to that value
    const initialStateValue = form.getFieldValue(nameof<AddCaveVm>("stateId"));
    if (!isNullOrWhiteSpace(initialStateValue)) {
      setSelectedStateId(initialStateValue);
    }
  }, [form]);

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

  // let groupedByFileTypes: {
  //   [key: string]: EditFileMetadataVm[];
  // } = {};
  const [groupedByFileTypes, setGroupedByFiles] = useState<{
    [key: string]: EditFileMetadataVm[];
  }>({});

  useEffect(() => {
    if (caveState?.files) {
      const temp = groupBy(caveState.files, (file) => file.fileTypeKey);
      setGroupedByFiles(temp);
    }
  }, [caveState]);

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

  const oneColProps = {
    xxl: 24,
    xl: 24,
    lg: 24,
    md: 24,
    sm: 24,
    xs: 24,
  } as ColProps;
  //#endregion
  return (
    <Row style={{ marginBottom: 10 }} gutter={5}>
      <Form.Item name="id" noStyle>
        <Input type="hidden" />
      </Form.Item>
      <Col span={12}>
        <Form.Item
          label="Name"
          name={nameof<AddCaveVm>("name")}
          rules={[{ required: true, message: "Please enter a name" }]}
        >
          <Input />
        </Form.Item>
      </Col>
      <Col span={12}>
        <Form.Item
          label="Alternate Names"
          name={nameof<AddCaveVm>("alternateNames")}
        >
          <Select
            mode="tags"
            style={{ width: "100%" }}
            placeholder="Add alternate names"
            allowClear
          ></Select>
        </Form.Item>
      </Col>
      <Col {...twoColProps}>
        <Form.Item
          label="State"
          name={nameof<AddCaveVm>("stateId")}
          rules={[{ required: true, message: "Please select a state" }]}
        >
          <StateDropdown
            autoSelectFirst={!isEditing}
            onChange={(e) => {
              setSelectedStateId(e.toString());
              if (isInitialLoad) return setIsInitialLoad(false);
              form.setFieldsValue({ countyId: undefined });
            }}
          />
        </Form.Item>
      </Col>
      <Col {...twoColProps}>
        <Form.Item
          label="County"
          name={nameof<AddCaveVm>("countyId")}
          rules={[{ required: true, message: "Please select a county" }]}
        >
          {selectedStateId && (
            <CountyDropdown selectedStateId={selectedStateId}></CountyDropdown>
          )}
        </Form.Item>
      </Col>
      <Col {...twoColProps}>
        <Form.Item
          label="Map Status"
          name={nameof<AddCaveVm>("mapStatusTagIds")}
        >
          <TagSelectComponent
            tagType={TagType.MapStatus}
            projectId={""}
            mode="multiple"
          />
        </Form.Item>
      </Col>
      <Col {...twoColProps}>
        <Form.Item
          label="Cartographer Name"
          name={nameof<AddCaveVm>("cartographerNameTagIds")}
        >
          <TagSelectComponent
            tagType={TagType.People}
            projectId={""}
            mode="tags"
          />
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
      <Col {...threeColProps}>
        <Form.Item
          label="Geologic Age"
          name={nameof<AddCaveVm>("geologicAgeTagIds")}
        >
          <TagSelectComponent
            tagType={TagType.GeologicAge}
            projectId={""}
            mode="multiple"
          />
        </Form.Item>
      </Col>
      <Col {...threeColProps}>
        <Form.Item
          label="Physiographic Province"
          name={nameof<AddCaveVm>("physiographicProvinceTagIds")}
        >
          <TagSelectComponent
            tagType={TagType.PhysiographicProvince}
            projectId={""}
            mode="multiple"
          />
        </Form.Item>
      </Col>
      <Col {...fourColProps}>
        <Form.Item
          label="Length"
          name={nameof<AddCaveVm>("lengthFeet")}
          rules={[
            {
              required: true,
              message: "Please enter the length in feet",
            },
          ]}
        >
          <InputDistanceComponent />
        </Form.Item>
      </Col>
      <Col {...fourColProps}>
        <Form.Item
          label="Depth"
          name={nameof<AddCaveVm>("depthFeet")}
          rules={[
            { required: true, message: "Please enter the depth in feet" },
          ]}
        >
          <InputDistanceComponent />
        </Form.Item>
      </Col>
      <Col {...fourColProps}>
        <Form.Item
          label="Max Pit Depth"
          name={nameof<AddCaveVm>("maxPitDepthFeet")}
          rules={[
            {
              required: true,
              message: "Please enter the max pit depth in feet",
            },
          ]}
        >
          <InputDistanceComponent />
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
      <Col {...twoColProps}>
        <Form.Item label="Biology" name={nameof<AddCaveVm>("biologyTagIds")}>
          <TagSelectComponent
            allowCustomTags
            tagType={TagType.Biology}
            mode="tags"
          />
        </Form.Item>
      </Col>
      <Col {...twoColProps}>
        <Form.Item
          label="Archeology"
          name={nameof<AddCaveVm>("archeologyTagIds")}
        >
          <TagSelectComponent
            allowCustomTags
            tagType={TagType.Archeology}
            mode="tags"
          />
        </Form.Item>
      </Col>
      <Col span={24}>
        <Form.Item label="Narrative" name={nameof<AddCaveVm>("narrative")}>
          <Input.TextArea rows={15} />
        </Form.Item>
      </Col>{" "}
      <Col {...oneColProps}>
        <Form.Item label="Other Tags" name={nameof<AddCaveVm>("otherTagIds")}>
          <TagSelectComponent
            tagType={TagType.CaveOther}
            projectId={""}
            mode="multiple"
          />
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
          name={nameof<AddCaveVm>("reportedByNameTagIds")}
        >
          <TagSelectComponent
            allowCustomTags
            tagType={TagType.People}
            mode="tags"
          />
        </Form.Item>
      </Col>
      <Col span={24}>
        <PlanarianDividerComponent title={"Entrances"} />
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
                            onConfirm={() => {
                              remove(field.name);
                            }}
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
                        <Col {...twoColProps}>
                          <Form.Item
                            {...field}
                            label="Name"
                            name={[field.name, nameof<AddEntranceVm>("name")]}
                          >
                            <Input />
                          </Form.Item>
                        </Col>
                        <Col {...twoColProps}>
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
                        <Col {...twoColProps}>
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
                        <Col {...twoColProps}>
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
                            label="Latitude"
                            help="WGS 84"
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
                            label="Elevation"
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
                            <InputDistanceComponent />
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
                            label="Pit Depth"
                            name={[
                              field.name,
                              nameof<AddEntranceVm>("pitFeet"),
                            ]}
                          >
                            <InputDistanceComponent />
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
                            <Input.TextArea rows={6} />
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
                            label="Reported By"
                            name={[
                              field.name,
                              nameof<AddEntranceVm>("reportedByNameTagIds"),
                            ]}
                          >
                            <TagSelectComponent
                              allowCustomTags
                              tagType={TagType.People}
                              mode="tags"
                            />
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
      {Object.entries(groupedByFileTypes).length > 0 && (
        <Col span={24}>
          <PlanarianDividerComponent title={"Files"} />
        </Col>
      )}
      {Object.entries(groupedByFileTypes).length > 0 && (
        <Col span={24}>
          <Form.List name={nameof<AddCaveVm>("files")}>
            {(fields, { add, remove }, { errors }) => (
              <Collapse accordion>
                {Object.entries(groupedByFileTypes).map(([fileType, files]) => (
                  <Collapse.Panel header={`${fileType}`} key={fileType}>
                    <Row gutter={16}>
                      {fields.map((field) => {
                        // Get the file at the index
                        const f = caveState?.files?.[field.key];

                        if (f?.fileTypeKey === fileType) {
                          return (
                            <Col key={field.key} span={12}>
                              <Card
                                bordered
                                style={{ height: "100%" }}
                                actions={[
                                  <DeleteButtonComponent
                                    title={
                                      "Are you sure? This cannot be undone!"
                                    }
                                    onConfirm={() => {
                                      if (caveState?.files) {
                                        remove(field.name);

                                        const filteredFiles =
                                          caveState.files.filter(
                                            (file, index) =>
                                              index !== field.key &&
                                              fields.some(
                                                (formField) =>
                                                  formField.key === index
                                              )
                                          );

                                        const temp = groupBy(
                                          filteredFiles,
                                          (file) => file.fileTypeKey
                                        );
                                        setGroupedByFiles(temp);
                                      }
                                    }}
                                  />,
                                ]}
                              >
                                <Form.Item
                                  {...field}
                                  label="Name"
                                  name={[
                                    field.name,
                                    nameof<EditFileMetadataVm>("displayName"),
                                  ]}
                                  rules={[
                                    {
                                      required: true,
                                      message: "Please enter a name",
                                    },
                                  ]}
                                >
                                  <Input />
                                </Form.Item>
                                <Form.Item
                                  {...field}
                                  label="File Type"
                                  name={[
                                    field.name,
                                    nameof<EditFileMetadataVm>("fileTypeTagId"),
                                  ]}
                                >
                                  <TagSelectComponent tagType={TagType.File} />
                                </Form.Item>
                              </Card>
                            </Col>
                          );
                        }

                        return null;
                      })}
                    </Row>
                  </Collapse.Panel>
                ))}
              </Collapse>
            )}
          </Form.List>
        </Col>
      )}
      <Col>
        <Form.Item>
          <Button
            style={{ marginTop: "16px" }}
            type="primary"
            htmlType="submit"
          >
            Submit
          </Button>
        </Form.Item>
      </Col>
    </Row>
  );
};

export { AddCaveComponent };
