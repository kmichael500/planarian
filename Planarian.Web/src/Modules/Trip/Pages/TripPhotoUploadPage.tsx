import {
  Card,
  Col,
  Form,
  Image,
  Input,
  message,
  Modal,
  Row,
  Spin,
  Tooltip,
  Upload,
  UploadFile,
  UploadProps,
} from "antd";
import {
  CloudUploadOutlined,
  DeleteOutlined,
  EyeOutlined,
  PlusOutlined,
} from "@ant-design/icons";

import { RcFile } from "antd/lib/upload";
import React, { useContext, useEffect, useState } from "react";
import { TripService } from "../Services/TripService";
import { PhotoMetaData } from "../../Photo/Models/PhotoMetaData";
import { TripPhotoUpload } from "../Models/TripPhotoUpload";
import { useNavigate, useParams } from "react-router-dom";
import "./TripPhotoUploadComponent.scss";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { BackButtonComponent } from "../../../Shared/Components/Buttons/BackButtonComponent";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { DeleteButtonComponent } from "../../../Shared/Components/Buttons/DeleteButtonComponent";

const { TextArea } = Input;

interface TripPhotoUploadComponentProps {}

const TripPhotoUploadPage: React.FC<TripPhotoUploadComponentProps> = (
  props: TripPhotoUploadComponentProps
) => {
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);

  useEffect(() => {
    setHeaderButtons([<BackButtonComponent to={"./.."} />]);
    setHeaderTitle(["Upload Photos"]);
  }, []);

  //#region Main Modal

  const [open, setOpen] = useState(false);
  const [confirmLoading, setConfirmLoading] = useState(false);

  const [form] = Form.useForm();

  const showModal = (): void => {
    setOpen(true);
  };

  const handleUploadModalCancel = (): void => {
    form.resetFields();
    setOpen(false);
  };

  const onSubmit = async (values: TripPhotoUpload[]): Promise<void> => {
    try {
      setConfirmLoading(true);
      const newTripid = await TripService.UploadPhotos(
        values,
        tripId as string
      );
      setOpen(false);
      navigate("../");
    } catch (e) {
      const error = e as ApiErrorResponse;
      message.error(error.message);
    }

    setConfirmLoading(false);
  };

  //#endregion

  //#region Upload Modal
  const [filesMetaData, setFilesMetaData] = useState<PhotoMetaData[]>([]);

  const [previewOpen, setPreviewOpen] = useState(false);
  const [previewImage, setPreviewImage] = useState("");
  const [previewTitle, setPreviewTitle] = useState("");

  const [fileList, setFileList] = useState<UploadFile[]>([]);
  const [uploading, setUploading] = useState(false);

  const { tripId } = useParams();
  const navigate = useNavigate();

  const handleCancel = () => setPreviewOpen(false);

  const handlePreview = async (file: UploadFile) => {
    if (!file.url && !file.preview) {
      file.preview = await getBase64(file.originFileObj as RcFile);
    }

    setPreviewImage(file.url || (file.preview as string));
    setPreviewOpen(true);
    setPreviewTitle(
      file.name || file.url!.substring(file.url!.lastIndexOf("/") + 1)
    );
  };

  const handleChange: UploadProps["onChange"] = ({ fileList: newFileList }) => {
    setFileList(newFileList);
  };

  const uploadButton = (
    <div>
      <PlusOutlined />
      <div style={{ marginTop: 8 }}>Add</div>
    </div>
  );
  const handleUpload = async () => {
    var fetchBody = [] as TripPhotoUpload[];
    fileList.forEach((file) => {
      const metaData = filesMetaData.findIndex((e) => e.uid === file.uid);
      fetchBody.push({
        ...filesMetaData[metaData],
        file: file.originFileObj,
      });
    });

    try {
      setUploading(true);
      await TripService.UploadPhotos(fetchBody, tripId as string);
      setFileList([]);
      message.success("Uploaded successfully");

      navigate("./../");
    } catch (e) {
      const error = e as ApiErrorResponse;
      message.error(error.message);
    }

    setUploading(false);
  };

  const onRemoveImageClick = (file: UploadFile<any>): void => {
    const index = fileList.indexOf(file);
    const newFileList = fileList.slice();
    newFileList.splice(index, 1);
    setFileList(newFileList);
  };

  const beforeUpload = (file: RcFile, FileList: RcFile[]) => {
    setFileList([...fileList, file]);

    return false;
  };

  //#endregion

  //#region File MetaData
  const onDescriptionChanged = (
    e: React.ChangeEvent<HTMLTextAreaElement>,
    file: UploadFile
  ) => {
    const value = e.target.value;
    const uid = file.uid;

    const newFilesMetaData = [...filesMetaData];
    const index = newFilesMetaData.findIndex((e) => e.uid === uid);

    const item = { ...newFilesMetaData[index] };
    item.description = value;
    newFilesMetaData[index] = item;

    if (index !== -1) {
      setFilesMetaData([...newFilesMetaData]);
    } else {
      setFilesMetaData([
        ...filesMetaData,
        { uid: file.uid, description: value, title: undefined },
      ]);
    }
  };

  const onTitleChanged = (
    e: React.ChangeEvent<HTMLInputElement>,
    file: UploadFile
  ) => {
    const value = e.target.value;
    const uid = file.uid;

    const newFilesMetaData = [...filesMetaData];
    const index = newFilesMetaData.findIndex((e) => e.uid === uid);

    const item = { ...newFilesMetaData[index] };
    item.title = value;
    newFilesMetaData[index] = item;

    if (index !== -1) {
      setFilesMetaData([...newFilesMetaData]);
    } else {
      setFilesMetaData([
        ...filesMetaData,
        { uid: file.uid, description: undefined, title: value },
      ]);
    }
  };

  const MetaDataForm = (file: UploadFile, disabled = true) => {
    return (
      <>
        <Form.Item label="Title" labelAlign="left">
          <Input
            onChange={(e) => {
              onTitleChanged(e, file);
            }}
          />
        </Form.Item>
        <Form.Item label="Description" labelAlign="left">
          {!disabled && (
            <TextArea
              onChange={(e) => {
                onDescriptionChanged(e, file);
              }}
              rows={4}
            />
          )}
          {disabled && (
            <TextArea
              onChange={(e) => {
                onDescriptionChanged(e, file);
              }}
              rows={4}
            />
          )}
        </Form.Item>
      </>
    );
  };

  //#endregion

  return (
    <>
      <Spin spinning={uploading}>
        <Card
          title="Upload"
          extra={
            <PlanarianButton
              icon={<CloudUploadOutlined />}
              onClick={handleUpload}
              type="primary"
            >
              Submit
            </PlanarianButton>
          }
        >
          <Upload
            multiple={true}
            maxCount={100}
            accept="image/png, image/jpeg"
            listType="picture-card"
            fileList={fileList}
            style={{ height: "unset" }}
            onPreview={handlePreview}
            onChange={handleChange}
            beforeUpload={beforeUpload}
            itemRender={(node, file, fileList) => {
              return (
                <>
                  <Image
                    preview={{
                      visible: false,
                      mask: (
                        <>
                          <Row gutter={5}>
                            <Col>
                              <Tooltip title="Preview">
                                <PlanarianButton
                                  onClick={() => {
                                    handlePreview(file);
                                  }}
                                  ghost
                                  icon={<EyeOutlined />}
                                ></PlanarianButton>
                              </Tooltip>
                            </Col>
                            <Col>
                              <Tooltip title="Remove">
                                <DeleteButtonComponent
                                  neverShowChildren
                                  title={"Delete"}
                                  onConfirm={() => {
                                    onRemoveImageClick(file);
                                  }}
                                  ghost
                                  icon={<DeleteOutlined />}
                                ></DeleteButtonComponent>
                              </Tooltip>
                            </Col>
                          </Row>
                        </>
                      ),
                    }}
                    src={file.thumbUrl}
                  />
                  {MetaDataForm(file)}
                </>
              );
            }}
          >
            {uploadButton}
          </Upload>

          <Modal
            open={previewOpen}
            title={previewTitle}
            footer={null}
            onCancel={handleCancel}
          >
            <img alt="example" style={{ width: "100%" }} src={previewImage} />
          </Modal>
        </Card>
      </Spin>
    </>
  );
};

const getBase64 = (file: RcFile): Promise<string> =>
  new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.readAsDataURL(file);
    reader.onload = () => resolve(reader.result as string);
    reader.onerror = (error) => reject(error);
  });

export { TripPhotoUploadPage };
