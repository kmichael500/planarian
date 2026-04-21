import { Button, Checkbox, Col, Form, Input, Row, Typography } from "antd";
import React from "react";
import { nameof } from "../../../Shared/Helpers/StringHelpers";

export interface ImportFileSettings {
  delimiter: string;
  idRegex: string;
  ignoreDuplicates: boolean;
  pauseOnFailures: boolean;
}

export interface DelimiterFormFields {
  delimiter: string;
  idRegex: string;
  ignoreDuplicates: boolean;
  pauseOnFailures: boolean;
}

interface ImportFileSettingsFormProps {
  form: ReturnType<typeof Form.useForm<DelimiterFormFields>>[0];
  onConfirm: (settings: ImportFileSettings) => void;
}

export const ImportFileSettingsForm: React.FC<ImportFileSettingsFormProps> = ({
  form,
  onConfirm,
}) => (
  <section className="import-file-settings">
    <Form
      form={form}
      layout="vertical"
      className="import-file-settings__form"
      onFinish={(values) => {
        onConfirm({
          delimiter: values.delimiter ?? "",
          idRegex: values.idRegex,
          ignoreDuplicates: values.ignoreDuplicates ?? true,
          pauseOnFailures: values.pauseOnFailures ?? true,
        });
      }}
    >
      <div className="import-file-settings__panel import-step-surface">
        <div className="import-file-settings__header">
          <div className="import-file-settings__copy">
            <Typography.Title level={4}>File Import Settings</Typography.Title>
            <Typography.Paragraph>
              Planarian reads each filename, extracts the county code and cave
              number, then looks up the cave record with that exact pair in the
              backend before attaching the file.
            </Typography.Paragraph>
            <Typography.Paragraph>
              Use the regex to capture the cave id portion of the filename. If
              the county code and cave number are separated, set a delimiter so
              the backend can split the matched value into the two lookup parts.
            </Typography.Paragraph>
            <Typography.Paragraph>
              Planarian also checks the filename for a file tag and applies it
              automatically when it finds a match. For example, filenames that
              include <strong>Map</strong> will be tagged as Map. If no known
              tag can be matched, the file defaults to <strong>Other</strong>.
            </Typography.Paragraph>
            <Typography.Paragraph className="import-file-settings__regex-note">
              Test your pattern against real filenames in{" "}
              <a href="https://regexr.com/" target="_blank" rel="noreferrer">
                Regexr
              </a>
              . Paste the full filename, verify the match returns only the cave
              id portion, then use that same regex here.
            </Typography.Paragraph>
          </div>
          <div className="import-file-settings__hint">
            <div className="import-file-settings__example">
              <Typography.Text type="secondary">
                Combined county code and number
              </Typography.Text>
              <Typography.Text>BE31_PumphouseCave.pdf</Typography.Text>
              <Typography.Text type="secondary">
                Regex: <strong>{`(?i)^[A-Z]{2}\\d+`}</strong>
              </Typography.Text>
              <Typography.Text type="secondary">
                Delimiter: not needed
              </Typography.Text>
            </div>
            <div className="import-file-settings__example">
              <Typography.Text type="secondary">
                County code and number separated
              </Typography.Text>
              <Typography.Text>BE_31_PumphouseCave.pdf</Typography.Text>
              <Typography.Text type="secondary">
                Regex: <strong>{`(?i)^[A-Z]{2}_\\d+`}</strong>
              </Typography.Text>
              <Typography.Text type="secondary">Delimiter: _</Typography.Text>
            </div>
          </div>
        </div>

        <div className="import-file-settings__body">
          <Row gutter={[16, 16]}>
            <Col xs={24} md={8}>
              <div className="import-file-settings__field-card">
                <Form.Item
                  label="Delimiter"
                  name={nameof<DelimiterFormFields>("delimiter")}
                  initialValue=""
                  extra="Leave this empty when the county code and cave number already appear together."
                >
                  <Input />
                </Form.Item>
              </div>
            </Col>
            <Col xs={24} md={8}>
              <div className="import-file-settings__field-card">
                <Form.Item
                  label="ID Regex"
                  name={nameof<DelimiterFormFields>("idRegex")}
                  rules={[{ required: true, message: "Please input an ID regex." }]}
                  extra="This should match the county code and cave number portion of the filename."
                >
                  <Input />
                </Form.Item>
              </div>
            </Col>
            <Col xs={24} md={8}>
              <div className="import-file-settings__field-card import-file-settings__field-card--toggle">
                <Form.Item
                  name={nameof<DelimiterFormFields>("ignoreDuplicates")}
                  valuePropName="checked"
                  initialValue={true}
                  extra="If enabled, files with the same name already attached to a cave will be skipped and not imported again. If disabled, files with matching names will overwrite any existing files on that cave."
                >
                  <Checkbox>Skip Duplicates</Checkbox>
                </Form.Item>
              </div>
            </Col>
            <Col xs={24} md={8}>
              <div className="import-file-settings__field-card import-file-settings__field-card--toggle">
                <Form.Item
                  name={nameof<DelimiterFormFields>("pauseOnFailures")}
                  valuePropName="checked"
                  initialValue={true}
                  extra="If enabled, the bulk upload will automatically pause after five failed files complete in a row."
                >
                  <Checkbox>Pause On Failures</Checkbox>
                </Form.Item>
              </div>
            </Col>
          </Row>
        </div>

        <div className="import-file-settings__actions">
          <Button type="primary" htmlType="submit">
            Confirm Settings
          </Button>
        </div>
      </div>
    </Form>
  </section>
);
