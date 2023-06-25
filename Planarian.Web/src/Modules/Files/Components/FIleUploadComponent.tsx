import Dragger from "antd/lib/upload/Dragger";
import { InboxOutlined } from "@ant-design/icons";
import { UploadProps, message } from "antd";
import { CaveService } from "../../Caves/Service/CaveService";
import { AxiosProgressEvent } from "axios";
import { RcFile } from "antd/lib/upload/interface";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { UploadRequestError } from "rc-upload/lib/interface";
import { FileVm } from "../Models/FileVm";
import { useState } from "react";

const UploadComponent = ({ caveId }: UploadComponentProps) => {
  const [uploadresult, setUploadresult] = useState<FileVm[]>([]);

  const props: UploadProps = {
    name: "file",
    multiple: true,
    async customRequest(options) {
      const { onSuccess, onError, file, onProgress } = options;
      if (!caveId) throw new Error("CaveId is undefined");
      try {
        const result = await CaveService.AddCaveFile(
          file,
          caveId,
          (event: AxiosProgressEvent) => {
            const percent = Math.round(
              (100 * event.loaded) / (event.total ?? 0)
            );

            console.log((file as RcFile).name, percent);

            if (onProgress) {
              onProgress({ percent });
            }
          }
        );
        setUploadresult([...uploadresult, result]);
        if (onSuccess) {
          onSuccess({});
        }
      } catch (e) {
        const errorMessage = e as ApiErrorResponse;
        message.error(errorMessage.message);
        if (onError) {
          onError({} as UploadRequestError);
        }
      }
    },
  };

  return (
    <Dragger {...props}>
      <p className="ant-upload-drag-icon">
        <InboxOutlined />
      </p>
      <p className="ant-upload-text">
        Drag and drop files or click to select files
      </p>
    </Dragger>
  );
};

interface UploadComponentProps {
  caveId?: string | undefined;
}

export { UploadComponent };
