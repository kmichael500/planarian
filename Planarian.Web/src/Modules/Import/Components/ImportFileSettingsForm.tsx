import { Button, Card, Checkbox, Col, Form, Input, Row, Typography } from "antd";
import React from "react";
import { nameof } from "../../../Shared/Helpers/StringHelpers";

export interface ImportFileSettings {
  delimiter: string;
  idRegex: string;
  ignoreDuplicates: boolean;
}

export interface DelimiterFormFields {
  delimiter: string;
  idRegex: string;
  ignoreDuplicates: boolean;
}

interface ImportFileSettingsFormProps {
  form: ReturnType<typeof Form.useForm<DelimiterFormFields>>[0];
  onConfirm: (settings: ImportFileSettings) => void;
}

export const ImportFileSettingsForm: React.FC<ImportFileSettingsFormProps> = ({
  form,
  onConfirm,
}) => (
  <Card className="planarian-import-info-card" style={{ width: "100%" }}>
    <Typography.Title level={4}>File Import Settings</Typography.Title>
    <Typography.Paragraph>
      Tell Planarian how to find the cave id in each filename.
    </Typography.Paragraph>
    <Typography.Paragraph>
      Example: for a file like <strong>BE31_PumphouseCave.pdf</strong>, leave{" "}
      <strong>Delimiter</strong> blank and use{" "}
      <strong>{`(?i)^[A-Z]{2}\\d+`}</strong>.
    </Typography.Paragraph>
    <Form
      form={form}
      layout="vertical"
      onFinish={(values) => {
        onConfirm({
          delimiter: values.delimiter ?? "",
          idRegex: values.idRegex,
          ignoreDuplicates: values.ignoreDuplicates ?? true,
        });
      }}
    >
      <Row gutter={16}>
        <Col span={8}>
          <Form.Item
            label="Delimiter"
            name={nameof<DelimiterFormFields>("delimiter")}
            initialValue=""
            extra="Leave blank when the county code and cave number are together."
          >
            <Input placeholder="-" />
          </Form.Item>
        </Col>
        <Col span={8}>
          <Form.Item
            label="ID Regex"
            name={nameof<DelimiterFormFields>("idRegex")}
            rules={[{ required: true, message: "Please input an ID regex." }]}
            extra="Match the cave id at the start of the filename."
          >
            <Input placeholder="\\d+-\\d+" />
          </Form.Item>
        </Col>
        <Col span={8}>
          <Form.Item
            name={nameof<DelimiterFormFields>("ignoreDuplicates")}
            label="Ignore Duplicates"
            valuePropName="checked"
            initialValue={true}
          >
            <Checkbox />
          </Form.Item>
        </Col>
      </Row>
      <Form.Item>
        <Button type="primary" htmlType="submit">
          Confirm Settings
        </Button>
      </Form.Item>
    </Form>
  </Card>
);
