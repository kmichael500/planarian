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
  Select,
  DatePicker,
  Upload,
  message,
} from "antd";
import { PlusOutlined, UploadOutlined } from "@ant-design/icons";
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
import { ShouldDisplay } from "../../../Shared/Permissioning/Components/ShouldDisplay";
import { FeatureKey } from "../../Account/Models/FeatureSettingVm";
import { TagComponent } from "../../Tag/Components/TagComponent";
import { v4 as uuidv4 } from "uuid";
import { FileService } from "../../Files/Services/FileService";
import { RcFile } from "antd/es/upload/interface";
import { UploadRequestOption as RcUploadRequestOption } from "rc-upload/lib/interface";
import { AxiosProgressEvent } from "axios";

export interface AddCaveComponentProps {
  form: FormInstance<AddCaveVm>;
  isEditing?: boolean;
  cave?: AddCaveVm;
}
const AddCaveComponent = ({ form, isEditing, cave }: AddCaveComponentProps) => {
  const [selectedStateId, setSelectedStateId] = useState<string>();
  const [isInitialLoad, setIsInitialLoad] = useState<boolean>(true);
  const [isFileUploading, setIsFileUploading] = useState<boolean>(false);

  const [caveState, setCaveState] = useState<AddCaveVm>(
    cave ?? ({} as AddCaveVm)
  );
  const [groupedNewFiles, setGroupedNewFiles] = useState<{
    [key: string]: EditFileMetadataVm[];
  }>({});
  const [groupedExistingFiles, setGroupedExistingFiles] = useState<{
    [key: string]: EditFileMetadataVm[];
  }>({});

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
    // if stateId has a value in the form, set the selectedStateId to that value
    const initialStateValue = form.getFieldValue("stateId");
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

  useEffect(() => {
    const currentFilesFromState = caveState.files || [];
    const newFilesList: EditFileMetadataVm[] = [];
    const existingFilesList: EditFileMetadataVm[] = [];

    currentFilesFromState.forEach((file) => {
      if (file.isNew) {
        newFilesList.push(file);
      } else {
        existingFilesList.push(file);
      }
    });

    setGroupedNewFiles(groupBy(newFilesList, (file) => file.fileTypeTagId));
    setGroupedExistingFiles(
      groupBy(existingFilesList, (file) => file.fileTypeTagId)
    );
  }, [caveState.files]);

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

  const handleUpload = async (options: RcUploadRequestOption) => {
    const { onSuccess, onError, file, onProgress } = options;
    setIsFileUploading(true);

    const tempId = uuidv4();

    try {
      const currentFile = file as RcFile;
      const uploadedFileVm = await FileService.AddTemporaryFile(
        currentFile,
        tempId,
        (event: AxiosProgressEvent) => {
          if (event.total) {
            onProgress?.({ percent: (event.loaded / event.total) * 100 });
          }
        }
      );

      const newFileMetadata: EditFileMetadataVm = {
        id: uploadedFileVm.id,
        displayName: uploadedFileVm.displayName || uploadedFileVm.fileName,
        fileTypeTagId: uploadedFileVm.fileTypeTagId,
        fileTypeKey: uploadedFileVm.fileTypeKey,
        isNew: true,
      };

      const currentFiles: EditFileMetadataVm[] =
        form.getFieldValue("files") || [];
      const updatedFiles = [...currentFiles, newFileMetadata];

      form.setFieldsValue({ [nameof<AddCaveVm>("files")]: updatedFiles });
      setCaveState((prevState) => ({ ...prevState, files: updatedFiles }));

      onSuccess?.(uploadedFileVm, currentFile);
      message.success(`${currentFile.name} file uploaded successfully.`);
    } catch (err: any) {
      console.error("Upload failed:", err);
      message.error(
        `${(file as RcFile).name} file upload failed: ${
          err.message || "Unknown error"
        }`
      );
      onError?.(err);
    } finally {
      setIsFileUploading(false);
    }
  };

  return (
    <Row style={{ marginBottom: 10 }} gutter={5}>
      <Form.Item name="id" noStyle>
        <Input type="hidden" />
      </Form.Item>
      <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveName}>
        <Col span={12}>
          <Form.Item
            label="Name"
            name={nameof<AddCaveVm>("name")}
            rules={[{ required: true, message: "Please enter a name" }]}
          >
            <Input />
          </Form.Item>
        </Col>
      </ShouldDisplay>
      <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveAlternateNames}>
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
      </ShouldDisplay>
      <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveState}>
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
      </ShouldDisplay>
      <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveCounty}>
        <Col {...twoColProps}>
          <Form.Item
            label="County"
            name={nameof<AddCaveVm>("countyId")}
            rules={[{ required: true, message: "Please select a county" }]}
          >
            {selectedStateId && (
              <CountyDropdown
                selectedStateId={selectedStateId}
              ></CountyDropdown>
            )}
          </Form.Item>
        </Col>
      </ShouldDisplay>
      <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveMapStatusTags}>
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
      </ShouldDisplay>
      <ShouldDisplay
        featureKey={FeatureKey.EnabledFieldCaveCartographerNameTags}
      >
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
      </ShouldDisplay>
      <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveGeologyTags}>
        <Col {...threeColProps}>
          <Form.Item label="Geology" name={nameof<AddCaveVm>("geologyTagIds")}>
            <TagSelectComponent
              tagType={TagType.Geology}
              projectId={""}
              mode="multiple"
            />
          </Form.Item>
        </Col>
      </ShouldDisplay>
      <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveGeologicAgeTags}>
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
      </ShouldDisplay>
      <ShouldDisplay
        featureKey={FeatureKey.EnabledFieldCavePhysiographicProvinceTags}
      >
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
      </ShouldDisplay>
      <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveLengthFeet}>
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
      </ShouldDisplay>
      <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveDepthFeet}>
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
      </ShouldDisplay>
      <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveMaxPitDepthFeet}>
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
      </ShouldDisplay>
      <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveNumberOfPits}>
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
      </ShouldDisplay>
      <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveBiologyTags}>
        <Col {...twoColProps}>
          <Form.Item label="Biology" name={nameof<AddCaveVm>("biologyTagIds")}>
            <TagSelectComponent
              allowCustomTags
              tagType={TagType.Biology}
              mode="tags"
            />
          </Form.Item>
        </Col>
      </ShouldDisplay>
      <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveArcheologyTags}>
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
      </ShouldDisplay>
      <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveNarrative}>
        <Col span={24}>
          <Form.Item label="Narrative" name={nameof<AddCaveVm>("narrative")}>
            <Input.TextArea rows={15} />
          </Form.Item>
        </Col>{" "}
      </ShouldDisplay>
      <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveOtherTags}>
        <Col {...oneColProps}>
          <Form.Item label="Other Tags" name={nameof<AddCaveVm>("otherTagIds")}>
            <TagSelectComponent
              tagType={TagType.CaveOther}
              projectId={""}
              mode="multiple"
            />
          </Form.Item>
        </Col>
      </ShouldDisplay>
      <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveReportedByNameTags}>
        <Col {...twoColProps}>
          <Form.Item label="Reported On" name={nameof<AddCaveVm>("reportedOn")}>
            <DatePicker style={{ width: "100%" }} />
          </Form.Item>
        </Col>
      </ShouldDisplay>
      <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveReportedByNameTags}>
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
      </ShouldDisplay>

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
                        <DeleteButtonComponent
                          title={`Are you sure you want to delete entrance ${(
                            field.name + 1
                          ).toString()}?`}
                          onConfirm={() => {
                            remove(field.name);
                          }}
                          okText="Yes"
                          cancelText="No"
                        />
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
                        <ShouldDisplay
                          featureKey={FeatureKey.EnabledFieldEntranceName}
                        >
                          <Col {...twoColProps}>
                            <Form.Item
                              {...field}
                              label="Name"
                              name={[field.name, nameof<AddEntranceVm>("name")]}
                            >
                              <Input />
                            </Form.Item>
                          </Col>
                        </ShouldDisplay>
                        <ShouldDisplay
                          featureKey={FeatureKey.EnabledFieldEntranceStatusTags}
                        >
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
                        </ShouldDisplay>
                        <ShouldDisplay
                          featureKey={
                            FeatureKey.EnabledFieldEntranceFieldIndicationTags
                          }
                        >
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
                        </ShouldDisplay>
                        <ShouldDisplay
                          featureKey={
                            FeatureKey.EnabledFieldEntranceHydrologyTags
                          }
                        >
                          <Col {...twoColProps}>
                            <Form.Item
                              {...field}
                              label="Hydrology"
                              name={[
                                field.name,
                                nameof<AddEntranceVm>(
                                  "entranceHydrologyTagIds"
                                ),
                              ]}
                            >
                              <TagSelectComponent
                                tagType={TagType.EntranceHydrology}
                                mode="multiple"
                              />
                            </Form.Item>
                          </Col>
                        </ShouldDisplay>
                        <ShouldDisplay
                          featureKey={
                            FeatureKey.EnabledFieldEntranceCoordinates
                          }
                        >
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
                        </ShouldDisplay>
                        <ShouldDisplay
                          featureKey={
                            FeatureKey.EnabledFieldEntranceCoordinates
                          }
                        >
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
                        </ShouldDisplay>
                        <ShouldDisplay
                          featureKey={FeatureKey.EnabledFieldEntranceElevation}
                        >
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
                        </ShouldDisplay>
                        <ShouldDisplay
                          featureKey={
                            FeatureKey.EnabledFieldEntranceLocationQuality
                          }
                        >
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
                                  message:
                                    "Please select a location quality tag",
                                },
                              ]}
                            >
                              <TagSelectComponent
                                mode={undefined}
                                tagType={TagType.LocationQuality}
                              />
                            </Form.Item>
                          </Col>
                        </ShouldDisplay>
                        <ShouldDisplay
                          featureKey={FeatureKey.EnabledFieldEntrancePitDepth}
                        >
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
                        </ShouldDisplay>
                        <ShouldDisplay
                          featureKey={
                            FeatureKey.EnabledFieldEntranceDescription
                          }
                        >
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
                        </ShouldDisplay>
                        <ShouldDisplay
                          featureKey={FeatureKey.EnabledFieldEntranceReportedOn}
                        >
                          <Col {...twoColProps}>
                            <Form.Item
                              {...field}
                              label="Reported On"
                              name={[
                                field.name,
                                nameof<AddEntranceVm>("reportedOn"),
                              ]}
                            >
                              <DatePicker style={{ width: "100%" }} />
                            </Form.Item>
                          </Col>
                        </ShouldDisplay>
                        <ShouldDisplay
                          featureKey={
                            FeatureKey.EnabledFieldEntranceReportedByNameTags
                          }
                        >
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
                        </ShouldDisplay>
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
      <Col span={24}>
        <PlanarianDividerComponent title={"File Uploads"} />
      </Col>
      <Col span={24} style={{ marginBottom: 16 }}>
        <Upload
          customRequest={handleUpload}
          showUploadList={false}
          disabled={isFileUploading}
        >
          <Button icon={<UploadOutlined />} loading={isFileUploading}>
            Upload New File
          </Button>
        </Upload>
      </Col>

      <Col span={24}>
        <Form.List name={nameof<AddCaveVm>("files")}>
          {(fields, { remove: removeFormListItem }) => (
            <>
              {Object.entries(groupedNewFiles).length > 0 && (
                <>
                  <Col span={24}>
                    <PlanarianDividerComponent title={"New Files"} />
                  </Col>
                  <Col span={24} style={{ marginBottom: "16px" }}>
                    <Row gutter={16}>
                      {" "}
                      {/* MODIFIED: Removed Collapse, added Row here */}
                      {fields.map((field) => {
                        const fileData = form.getFieldValue("files")[
                          field.name
                        ] as EditFileMetadataVm | undefined;
                        if (fileData && fileData.isNew) {
                          return (
                            <Col
                              key={field.key} // MODIFIED: Ensure key is unique if field.key isn't sufficient, e.g., `fileData.id || field.key`
                              span={12}
                              style={{ marginBottom: "16px" }}
                            >
                              <Card
                                variant="outlined"
                                style={{ height: "100%" }}
                                actions={[
                                  <DeleteButtonComponent
                                    title={"Remove this new file?"}
                                    onConfirm={() => {
                                      removeFormListItem(field.name);
                                      const updatedFilesFromForm =
                                        form.getFieldValue("files") || [];
                                      setCaveState((prevState) => ({
                                        ...prevState,
                                        files: updatedFilesFromForm,
                                      }));
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
                                  rules={[
                                    {
                                      required: true,
                                      message: "Please select a file type",
                                    },
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
                    </Row>{" "}
                    {/* MODIFIED: Removed Collapse */}
                  </Col>
                </>
              )}

              {Object.entries(groupedExistingFiles).length > 0 && (
                <>
                  <Col
                    span={24}
                    style={{
                      marginTop:
                        Object.entries(groupedNewFiles).length > 0 ? "16px" : 0,
                    }}
                  >
                    <PlanarianDividerComponent title={"Files"} />
                  </Col>
                  <Col span={24}>
                    <Collapse accordion>
                      {Object.entries(groupedExistingFiles).map(
                        ([fileType, _filesInGroup]) => (
                          <Collapse.Panel
                            header={<TagComponent tagId={fileType} />}
                            key={`existing-${fileType}`}
                          >
                            <Row gutter={16}>
                              {fields.map((field) => {
                                const fileData = form.getFieldValue("files")[
                                  field.name
                                ] as EditFileMetadataVm | undefined;
                                if (
                                  fileData &&
                                  !fileData.isNew &&
                                  fileData.fileTypeTagId === fileType
                                ) {
                                  return (
                                    <Col
                                      key={field.key}
                                      span={12}
                                      style={{ marginBottom: "16px" }}
                                    >
                                      <Card
                                        variant="outlined"
                                        style={{ height: "100%" }}
                                        actions={[
                                          <DeleteButtonComponent
                                            title={
                                              "Are you sure? This cannot be undone!"
                                            }
                                            onConfirm={() => {
                                              removeFormListItem(field.name);
                                              const updatedFilesFromForm =
                                                form.getFieldValue("files") ||
                                                [];
                                              setCaveState((prevState) => ({
                                                ...prevState,
                                                files: updatedFilesFromForm,
                                              }));
                                            }}
                                          />,
                                        ]}
                                      >
                                        <Form.Item
                                          {...field}
                                          label="Name"
                                          name={[
                                            field.name,
                                            nameof<EditFileMetadataVm>(
                                              "displayName"
                                            ),
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
                                            nameof<EditFileMetadataVm>(
                                              "fileTypeTagId"
                                            ),
                                          ]}
                                          rules={[
                                            {
                                              required: true,
                                              message:
                                                "Please select a file type",
                                            },
                                          ]}
                                        >
                                          <TagSelectComponent
                                            tagType={TagType.File}
                                          />
                                        </Form.Item>
                                      </Card>
                                    </Col>
                                  );
                                }
                                return null;
                              })}
                            </Row>
                          </Collapse.Panel>
                        )
                      )}
                    </Collapse>
                  </Col>
                </>
              )}
            </>
          )}
        </Form.List>
      </Col>
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
