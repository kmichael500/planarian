import React from "react";
import { Result, Button, Card } from "antd";
import { CheckCircleOutlined } from "@ant-design/icons";

const ImportSuccessComponent = () => {
  return (
    <Card style={{ width: "100%" }}>
      <Result
        icon={<CheckCircleOutlined style={{ color: "#52c41a" }} />}
        title="Successfully Uploaded!"
        subTitle="Your cave and cave entrance data has been successfully uploaded and processed."
        extra={[
          <Button type="primary" key="console">
            Go to Dashboard
          </Button>,
          <Button key="buy">View Details</Button>,
        ]}
      />
    </Card>
  );
};

export { ImportSuccessComponent };
