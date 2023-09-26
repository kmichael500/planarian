import React, { useEffect, useState } from "react";
import { Table, Button, Input, Modal, message, Form } from "antd";
import { ExclamationCircleOutlined } from "@ant-design/icons";
import form from "antd/lib/form";
import { ColumnsType } from "antd/lib/table";

export interface TagTypeEditVm {
  tagTypeId: string;
  name: string;
  isDeletable: boolean;
}

interface TagTypeEditComponentProps {
  tagTypes: TagTypeEditVm[];
  handleAdd: (name: string) => Promise<TagTypeEditVm>;
  handleSave: (tagType: TagTypeEditVm) => Promise<void>;
  handleDelete: (tagTypeId: string) => Promise<void>;
}

const TagTypeEditComponent: React.FC<TagTypeEditComponentProps> = ({
  tagTypes,
  handleAdd,
  handleSave,
  handleDelete,
}) => {
  const [editedTagType, setEditedTagType] = useState<TagTypeEditVm | null>(
    null
  );
  const [localTagTypes, setLocalTagTypes] = useState<TagTypeEditVm[]>(tagTypes);
  const [loading, setLoading] = useState<boolean>(false);
  const [form] = Form.useForm();

  useEffect(() => {
    setLocalTagTypes(tagTypes);
  }, [tagTypes]);

  const onAdd = async () => {
    try {
      const values = await form.validateFields();
      setLoading(true);
      const newTagType = await handleAdd(values.name);
      setLocalTagTypes((prev) => [newTagType, ...prev]); // Prepend new item to the local state
      form.resetFields();
      message.success("Tag Type added successfully!");
    } catch (error) {
      message.error("Failed to add Tag Type!");
    } finally {
      setLoading(false);
    }
  };

  const onSave = async (tagType: TagTypeEditVm) => {
    try {
      setLoading(true);
      await handleSave(tagType);
      setLocalTagTypes((prev) =>
        prev.map((t) => (t.tagTypeId === tagType.tagTypeId ? tagType : t))
      );
      message.success("Tag Type saved successfully!");
    } catch (error) {
      message.error("Failed to save Tag Type!");
    } finally {
      setLoading(false);
      setEditedTagType(null);
    }
  };

  const onDelete = async (tagTypeId: string) => {
    try {
      setLoading(true);
      await handleDelete(tagTypeId);
      setLocalTagTypes((prev) => prev.filter((t) => t.tagTypeId !== tagTypeId));
      message.success("Tag Type deleted successfully!");
    } catch (error) {
      message.error("Failed to delete Tag Type!");
    } finally {
      setLoading(false);
    }
  };

  const columns = [
    {
      title: "Name",
      dataIndex: "name",
      key: "name",
      render: (text: string, record: TagTypeEditVm) => {
        if (editedTagType && editedTagType.tagTypeId === record.tagTypeId) {
          return (
            <Form onFinish={() => onSave(editedTagType)}>
              <Form.Item name="name" initialValue={text} noStyle>
                <Input
                  autoFocus
                  onChange={(e) =>
                    setEditedTagType({ ...editedTagType, name: e.target.value })
                  }
                />
              </Form.Item>
            </Form>
          );
        }
        return text;
      },
      filterSearch: true,
      onFilter: (value: string, record: TagTypeEditVm) =>
        record.name.includes(value),
    },
    {
      title: "Action",
      key: "action",
      render: (text: string, record: TagTypeEditVm) => (
        <span>
          {editedTagType && editedTagType.tagTypeId === record.tagTypeId ? (
            <>
              <Button onClick={() => onSave(editedTagType)} type="primary">
                Save
              </Button>
              <Button onClick={() => setEditedTagType(null)}>Cancel</Button>
            </>
          ) : (
            <Button onClick={() => setEditedTagType(record)}>Edit</Button>
          )}
          {record.isDeletable && (
            <Button
              danger
              onClick={() =>
                Modal.confirm({
                  title: "Are you sure delete this tag type?",
                  icon: <ExclamationCircleOutlined />,
                  content: "This action is irreversible.",
                  onOk() {
                    onDelete(record.tagTypeId);
                  },
                })
              }
            >
              Delete
            </Button>
          )}
        </span>
      ),
    },
  ] as ColumnsType<TagTypeEditVm>;

  return (
    <div>
      <Form form={form} layout="inline" onFinish={onAdd}>
        <Form.Item
          name="name"
          rules={[
            {
              required: true,
              message: "Please input the name of the tag type!",
            },
          ]}
        >
          <Input placeholder="New Tag Type Name" />
        </Form.Item>
        <Form.Item shouldUpdate>
          <Button type="primary" onClick={onAdd}>
            Add New
          </Button>
        </Form.Item>
      </Form>
      <Table
        columns={columns}
        dataSource={localTagTypes}
        rowKey="tagTypeId"
        loading={loading}
      />
    </div>
  );
};

export default TagTypeEditComponent;
