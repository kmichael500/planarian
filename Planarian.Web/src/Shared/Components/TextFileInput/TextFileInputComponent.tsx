import React, { useState } from "react";
import { Upload } from "antd";
import { UploadOutlined } from "@ant-design/icons";
import { PlanarianButton } from "../Buttons/PlanarianButtton";

interface Props {
  onTextChange: (text: string) => void;
  buttonText: string;
}

const TextFileInputComponent: React.FC<Props> = ({
  onTextChange,
  buttonText,
}) => {
  const [fileList] = useState<any[]>([]);

  const beforeUpload = (file: File) => {
    const reader = new FileReader();
    reader.onload = (e: any) => {
      onTextChange(e.target.result);
    };
    reader.readAsText(file);
    return false;
  };

  const props = {
    beforeUpload,
    multiple: false,
    fileList,
  };

  return (
    <Upload {...props}>
      <PlanarianButton icon={<UploadOutlined />}>{buttonText}</PlanarianButton>
    </Upload>
  );
};

export { TextFileInputComponent };
