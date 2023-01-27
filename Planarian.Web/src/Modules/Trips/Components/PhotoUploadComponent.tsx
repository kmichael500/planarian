import {
  Button,
  Card,
  Col,
  Divider,
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
import { DeleteOutlined, EyeOutlined, PlusOutlined } from "@ant-design/icons";

import { RcFile } from "antd/lib/upload";
import React, { useEffect, useState } from "react";
import { CreateOrEditTripVm } from "../Models/CreateOrEditTripVm";
import { TripService } from "../Services/TripService";
import { PhotoMetaData } from "../Models/PhotoMetaData";
import { TripPhotoUpload } from "../Models/TripPhotoUpload";
import { Link, useNavigate, useParams } from "react-router-dom";
import "./PhotoUploadComponent.scss";

const { TextArea } = Input;

interface PhotoUploadComponentProps {}

const PhotoUploadComponent: React.FC<PhotoUploadComponentProps> = (
  props: PhotoUploadComponentProps
) => {
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

  const onSubmit = async (values: CreateOrEditTripVm): Promise<void> => {
    setConfirmLoading(true);
    const newTripid = await TripService.AddTrip(values);

    setOpen(false);
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
    } catch {
      message.error("Upload failed");
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
  useEffect(() => {
    console.log("Do something after counter has changed", fileList);
  }, [fileList]);

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
      <Row align="middle" gutter={10}>
        <Col>{/* <Title level={2}>{project?.name}</Title> */}</Col>
        {/* take up rest of space to push others to right and left side */}
        <Col flex="auto"></Col>
        <Col>
          <Link to={"./../"}>
            <Button>Back</Button>
          </Link>
        </Col>
        <Col> </Col>
      </Row>
      <Divider />
      <Spin spinning={uploading}>
        <Card
          title="Upload"
          extra={
            <Button onClick={handleUpload} type="primary">
              Upload
            </Button>
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
                                <Button
                                  onClick={() => {
                                    handlePreview(file);
                                  }}
                                  ghost
                                  icon={<EyeOutlined />}
                                ></Button>
                              </Tooltip>
                            </Col>
                            <Col>
                              <Tooltip title="Remove">
                                <Button
                                  onClick={() => {
                                    onRemoveImageClick(file);
                                  }}
                                  ghost
                                  icon={<DeleteOutlined />}
                                ></Button>
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

export { PhotoUploadComponent };
