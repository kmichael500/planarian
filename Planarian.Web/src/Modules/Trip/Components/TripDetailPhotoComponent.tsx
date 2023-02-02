import {
  Button,
  Col,
  Image,
  Modal,
  Popconfirm,
  Row,
  Tooltip,
  Typography,
} from "antd";
import { DeleteOutlined, EyeOutlined } from "@ant-design/icons";

import { useEffect, useState } from "react";
import { TripService } from "../Services/TripService";
import { PhotoVm } from "../../Photo/Models/PhotoVm";
import { isNullOrWhiteSpace } from "../../../Shared/Helpers/StringHelpers";
import { PhotoService } from "../../Photo/Services/PhotoService";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { DeleteButtonComponent } from "../../../Shared/Components/Buttons/DeleteButtonComponent";

const { Title, Paragraph } = Typography;

interface TripDetailPhotoComponentProps {
  tripId: string;
}

const TripDetailPhotoComponent: React.FC<TripDetailPhotoComponentProps> = (
  props
) => {
  let [isLoading, setIsLoading] = useState(false);
  let [photos, setPhotos] = useState<PhotoVm[]>();

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

  const onRemoveImageClick = async (photo: PhotoVm): Promise<void> => {
    if (photos === undefined) return;

    await PhotoService.DeleteTripPhoto(photo.id as string);

    const index = photos.indexOf(photo);
    const newPhotos = photos?.slice();
    newPhotos.splice(index, 1);
    setPhotos(newPhotos);
  };

  const handlePreview = async (photo: PhotoVm) => {
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
                          <PlanarianButton
                            onClick={() => {
                              handlePreview(photo);
                            }}
                            ghost
                            icon={<EyeOutlined />}
                          />
                        </Tooltip>
                      </Col>
                      <Col>
                        <Tooltip title="Delete">
                          <DeleteButtonComponent
                            title="Are you sure to delete this photo?"
                            onConfirm={() => onRemoveImageClick(photo)}
                            okText="Yes"
                            cancelText="No"
                            neverShowChildren={true}
                          />
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
