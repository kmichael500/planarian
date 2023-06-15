import React, { useState } from "react";
import {
  Card,
  Button,
  List,
  Form,
  Input,
  Space,
  Popconfirm,
  message,
  Upload,
} from "antd";
import { UploadOutlined } from "@ant-design/icons";
import { Parser } from "csv-parse";

interface County {
  id: string;
  name: string;
  countyId: string;
}

const AccountSettings: React.FC = () => {
  const [counties, setCounties] = useState<County[]>([]);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form] = Form.useForm();
  const [countyNameColumn, setCountyNameColumn] = useState<string>("");
  const [countyIdColumn, setCountyIdColumn] = useState<string>("");

  const handleSave = () => {
    form
      .validateFields()
      .then((values) => {
        const updatedCounties = counties.map((county) =>
          county.id === editingId
            ? { ...county, name: values.name, countyId: values.countyId }
            : county
        );
        setCounties(updatedCounties);
        setEditingId(null);
        form.resetFields();
        message.success("County saved successfully!");
      })
      .catch((error) => {
        console.error("Validation failed:", error);
      });
  };

  const handleEdit = (id: string) => {
    const county = counties.find((county) => county.id === id);
    if (county) {
      setEditingId(county.id);
      form.setFieldsValue({ name: county.name, countyId: county.countyId });
    }
  };

  const handleDelete = (id: string) => {
    const updatedCounties = counties.filter((county) => county.id !== id);
    setCounties(updatedCounties);
    message.success("County deleted successfully!");
  };

  const handleAdd = () => {
    form
      .validateFields()
      .then((values) => {
        const newCounty: County = {
          id: String(counties.length + 1),
          name: values.name,
          countyId: values.countyId,
        };
        setCounties([...counties, newCounty]);
        form.resetFields();
        message.success("County added successfully!");
      })
      .catch((error) => {
        console.error("Validation failed:", error);
      });
  };

  const handleBulkUpload = (file: File) => {
    const reader = new FileReader();

    reader.onload = () => {
      const fileData = reader.result as string;

      // Parse the CSV file
      const csvParser = new Parser({ columns: true, trim: true });
      csvParser.on("readable", () => {
        let record;
        while ((record = csvParser.read())) {
          const countyId = record[countyIdColumn];
          const countyName = record[countyNameColumn];

          if (countyId && countyName) {
            const newCounty: County = {
              id: String(counties.length + 1),
              name: countyName,
              countyId: countyId,
            };
            setCounties([...counties, newCounty]);
          }
        }
      });

      csvParser.on("end", () => {
        message.success("Counties uploaded successfully!");
      });

      csvParser.on("error", (error) => {
        console.error("Error parsing CSV:", error);
        message.error("Error parsing CSV. Please try again.");
      });

      csvParser.write(fileData);
      csvParser.end();
    };

    reader.readAsText(file);
  };

  return (
    <Card title="Account Settings">
      <List
        dataSource={counties}
        renderItem={(county) => (
          <List.Item>
            {editingId === county.id ? (
              <Form form={form} layout="inline">
                <Form.Item
                  name="countyId"
                  initialValue={county.countyId}
                  rules={[{ required: true }]}
                >
                  <Input />
                </Form.Item>
                <Form.Item
                  name="name"
                  initialValue={county.name}
                  rules={[{ required: true }]}
                >
                  <Input />
                </Form.Item>
                <Form.Item>
                  <Space>
                    <Button type="primary" onClick={handleSave}>
                      Save
                    </Button>
                    <Button onClick={() => setEditingId(null)}>Cancel</Button>
                  </Space>
                </Form.Item>
              </Form>
            ) : (
              <Space>
                <span>
                  {county.countyId}: {county.name}
                </span>
                <Button type="link" onClick={() => handleEdit(county.id)}>
                  Edit
                </Button>
                <Popconfirm
                  title="Are you sure you want to delete this county?"
                  onConfirm={() => handleDelete(county.id)}
                  okText="Yes"
                  cancelText="No"
                >
                  <Button type="link" danger>
                    Delete
                  </Button>
                </Popconfirm>
              </Space>
            )}
          </List.Item>
        )}
      />
      <Form form={form} layout="inline" onFinish={handleAdd}>
        <Form.Item
          name="countyId"
          label="County ID"
          rules={[{ required: true }]}
        >
          <Input />
        </Form.Item>
        <Form.Item name="name" label="Name" rules={[{ required: true }]}>
          <Input />
        </Form.Item>
        <Form.Item>
          <Button type="primary" htmlType="submit">
            Add
          </Button>
        </Form.Item>
      </Form>
      <Form layout="inline">
        <Form.Item label="County ID Column">
          <Input
            value={countyIdColumn}
            onChange={(e) => setCountyIdColumn(e.target.value)}
          />
        </Form.Item>
        <Form.Item label="County Name Column">
          <Input
            value={countyNameColumn}
            onChange={(e) => setCountyNameColumn(e.target.value)}
          />
        </Form.Item>
      </Form>
      <Upload.Dragger
        beforeUpload={(file) => {
          handleBulkUpload(file);
          return false; // Prevent automatic file upload
        }}
      >
        <p className="ant-upload-drag-icon">
          <UploadOutlined />
        </p>
        <p className="ant-upload-text">
          Click or drag file to this area to upload bulk counties
        </p>
      </Upload.Dragger>
    </Card>
  );
};

export { AccountSettings };
