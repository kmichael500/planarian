import React, { useState } from "react";
import { Upload, message, Button } from "antd";
import { UploadOutlined } from "@ant-design/icons";

interface Props {
  onTextChange: (text: string) => void;
  buttonText: string;
}

const TextFileInput: React.FC<Props> = ({ onTextChange, buttonText }) => {
  const [fileList, setFileList] = useState<any[]>([]);

  const handleChange = (info: any) => {
    let fileList = [...info.fileList];

    // 1. Limit the number of uploaded files
    // Only to show one recent uploaded file, and old ones will be replaced by the new
    fileList = fileList.slice(-1);

    // 2. Read from the file object and set the state
    setFileList(fileList);
    const file = info.file;
    if (file.status !== "uploading") {
    }
    if (file.status === "done") {
      message.success(`${file.name} file uploaded successfully`);
      const reader = new FileReader();
      reader.onload = (e: any) => {
        onTextChange(e.target.result);
      };
      reader.readAsText(file.originFileObj);
    } else if (file.status === "error") {
      message.error(`${file.name} file upload failed.`);
    }
  };

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
      <Button icon={<UploadOutlined />}>{buttonText}</Button>
    </Upload>
  );
};

export { TextFileInput };
