import {
  Row,
  Col,
  Button,
  Image,
  Typography,
  Tooltip,
  Modal,
  Popconfirm,
} from "antd";
import { EyeOutlined, DeleteOutlined } from "@ant-design/icons";

import { useState, useEffect } from "react";
import { useParams } from "react-router-dom";
import { TripService } from "../Services/TripService";
import { TripPhotoVm } from "../Models/TripPhotoVm";
import { isNullOrWhiteSpace } from "../../../Shared/Helpers/StringHelpers";
import { TripPhotoService } from "../../TripPhotos/Services/TripPhotoService";

const { Title, Paragraph } = Typography;

interface TripDetailPhotoComponentProps {
  tripId: string;
}
const TripDetailPhotoComponent: React.FC<TripDetailPhotoComponentProps> = (
  props
) => {
  let [isLoading, setIsLoading] = useState(false);
  let [photos, setPhotos] = useState<TripPhotoVm[]>();

  const [previewOpen, setPreviewOpen] = useState(false);
  const [previewImage, setPreviewImage] = useState("");
  const [previewDescription, setPreviewDescription] = useState("");

  const [previewTitle, setPreviewTitle] = useState("");

  useEffect(() => {
    if (photos === undefined) {
      const getTripPhotos = async () => {
        const photosResponse = await TripService.GetTripPhotos(
          props.tripId as string
        );
        setPhotos(photosResponse);
        setIsLoading(false);
      };
      getTripPhotos();
    }
  });

  const handleCancel = () => setPreviewOpen(false);

  const onRemoveImageClick = async (photo: TripPhotoVm): Promise<void> => {
    if (photos === undefined) return;

    await TripPhotoService.DeleteTripPhoto(photo.id as string);

    const index = photos.indexOf(photo);
    const newPhotos = photos?.slice();
    newPhotos.splice(index, 1);
    setPhotos(newPhotos);
  };

  const handlePreview = async (photo: TripPhotoVm) => {
    setPreviewImage(photo.url);
    setPreviewDescription(photo.description ?? "");
    setPreviewOpen(true);
    setPreviewTitle(photo.title ?? "");
  };

  return (
    <>
      {photos?.map((photo) => {
        return (
          <>
            <Image
              alt={photo.description ?? photo.title}
              key={photo.id}
              width={200}
              src={photo.url}
              preview={{
                visible: false,
                mask: (
                  <>
                    <Row gutter={5}>
                      <Col>
                        <Tooltip title="Preview">
                          <Button
                            onClick={() => {
                              handlePreview(photo);
                            }}
                            ghost
                            icon={<EyeOutlined />}
                          ></Button>
                        </Tooltip>
                      </Col>
                      <Col>
                        <Tooltip title="Remove">
                          <Popconfirm
                            title="Are you sure to delete this photo?"
                            onConfirm={() => onRemoveImageClick(photo)}
                            okText="Yes"
                            cancelText="No"
                          >
                            <Button icon={<DeleteOutlined />}></Button>
                          </Popconfirm>
                        </Tooltip>
                      </Col>
                    </Row>
                  </>
                ),
              }}
            />
          </>
        );
      })}
      <Modal
        open={previewOpen}
        title={previewTitle}
        footer={null}
        onCancel={handleCancel}
      >
        <img alt="example" style={{ width: "100%" }} src={previewImage} />
        {!isNullOrWhiteSpace(previewDescription) && (
          <>
            <br />
            <Paragraph>Description: {previewDescription}</Paragraph>
          </>
        )}
      </Modal>
    </>
  );
};

export { TripDetailPhotoComponent };