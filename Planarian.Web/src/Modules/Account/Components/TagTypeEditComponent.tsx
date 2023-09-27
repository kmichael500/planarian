import React, { useEffect, useMemo, useRef, useState } from "react";
import {
  Table,
  Button,
  Input,
  Modal,
  message,
  Form,
  Space,
  InputRef,
  Select,
  Col,
  Row,
} from "antd";
import Highlighter from "react-highlight-words";
import { ColumnType, ColumnsType } from "antd/lib/table";
import {
  FilterDropdownProps,
  TableRowSelection,
} from "antd/lib/table/interface";
import { SearchOutlined } from "@ant-design/icons";
import { ConfirmationModalComponent } from "../../../Shared/Components/Validation/ConfirmationModalComponent";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { SettingsService } from "../../Setting/Services/SettingsService";
import { TagType } from "../../Tag/Models/TagType";
import { AccountService } from "../Services/AccountService";
import { CreateEditTagTypeVm } from "./CreateEditTagTypeVm";
import { nameof } from "../../../Shared/Helpers/StringHelpers";
import { searchFilterSelectListItems } from "../../../Shared/Helpers/ArrayHelpers";
export interface TagTypeTableVm {
  tagTypeId: string;
  name: string;
  isUserModifiable: boolean;
  occurrences: number;
}

interface MergeForm {
  destinationTag: string;
}

interface TagTypeEditComponentProps {
  tagType: TagType;
}

const TagTypeEditComponent: React.FC<TagTypeEditComponentProps> = ({
  tagType,
}) => {
  const [editedTagType, setEditedTagType] = useState<TagTypeTableVm | null>(
    null
  );

  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(15); // Assuming default page size is 10

  const [isLoading, setIsLoading] = useState<boolean>(true);

  const [tagTypes, setTagTypes] = useState<TagTypeTableVm[]>([]);
  useEffect(() => {
    const getTags = async () => {
      var tags = await AccountService.GetTagsForTable(tagType);
      setTagTypes(tags);
      setIsLoading(false);
    };
    getTags();
  }, []);

  const [localTagTypes, setLocalTagTypes] =
    useState<TagTypeTableVm[]>(tagTypes);
  const [form] = Form.useForm<CreateEditTagTypeVm>();
  const [mergeForm] = Form.useForm<MergeForm>();

  const [searchText, setSearchText] = useState<string>("");
  const [singleMergeTagTypeId, setSingleMergeTagTypeId] = useState<
    string | null
  >(null);

  const [searchedColumn, setSearchedColumn] = useState<string>("");
  const searchInput = useRef<InputRef>(null);
  const [isMergeModalOpen, setIsMergeModalOpen] = useState(false);

  const [selectedRowKeys, setSelectedRowKeys] = useState<string[]>([]); // Add this state to manage selected rows
  const onSelectChange = (selectedKeys: string[]) => {
    setSelectedRowKeys(selectedKeys);
  };

  useEffect(() => {
    setLocalTagTypes(tagTypes);
  }, [tagTypes]);

  const onAdd = async () => {
    try {
      const values = await form.validateFields();
      setIsLoading(true);
      const newTagType = await AccountService.AddTagType(values);
      const insertionIndex = (currentPage - 1) * pageSize;
      setLocalTagTypes((prev) => [
        ...prev.slice(0, insertionIndex),
        newTagType,
        ...prev.slice(insertionIndex),
      ]);
      form.resetFields();
      message.success("Tag Type added successfully!");
    } catch (error) {
      message.error("Failed to add Tag Type!");
    } finally {
      setIsLoading(false);
    }
  };

  const rowSelection = {
    selectedRowKeys,
    onChange: onSelectChange,
  } as TableRowSelection<TagTypeTableVm>;

  const hasSelected = selectedRowKeys.length > 0;

  const onMassDeleteConfirm = async () => {
    try {
      setIsLoading(true);
      await AccountService.DeleteTagTypes(selectedRowKeys);
      setLocalTagTypes((prev) =>
        prev.filter((t) => !selectedRowKeys.includes(t.tagTypeId))
      );
      message.success("Tag Types deleted successfully!");
    } catch (error) {
      message.error("Failed to delete some Tag Types!");
    } finally {
      setIsLoading(false);
      setSelectedRowKeys([]); // Clear the selection
    }
  };

  const onSave = async (tagType: TagTypeTableVm) => {
    try {
      setIsLoading(true);
      await AccountService.UpdateTagTypes(tagType.tagTypeId, {
        name: tagType.name,
      });
      setLocalTagTypes((prev) =>
        prev.map((t) => (t.tagTypeId === tagType.tagTypeId ? tagType : t))
      );
      message.success("Tag Type saved successfully!");
    } catch (error) {
      message.error("Failed to save Tag Type!");
    } finally {
      setIsLoading(false);
      setEditedTagType(null);
    }
  };

  const onDelete = async (tagTypeId: string) => {
    try {
      setIsLoading(true);
      await AccountService.DeleteTagTypes([tagTypeId]);
      setLocalTagTypes((prev) => prev.filter((t) => t.tagTypeId !== tagTypeId));
      message.success("Tag Type deleted successfully!");
    } catch (error) {
      message.error("Failed to delete Tag Type!");
    } finally {
      setIsLoading(false);
    }
  };

  const getColumnSearchProps = (
    dataIndex: string
  ): ColumnType<TagTypeTableVm> => ({
    filterDropdown: ({
      setSelectedKeys,
      selectedKeys,
      confirm,
      clearFilters,
    }: FilterDropdownProps) => (
      <div style={{ padding: 8 }}>
        <Input
          ref={searchInput}
          placeholder={`Search ${dataIndex}`}
          value={selectedKeys[0]}
          onChange={(e) =>
            setSelectedKeys(e.target.value ? [e.target.value] : [])
          }
          onPressEnter={() => handleSearch(selectedKeys, confirm!, dataIndex)}
          style={{ marginBottom: 8, display: "block" }}
        />
        <Space>
          <PlanarianButton
            type="primary"
            onClick={() => handleSearch(selectedKeys, confirm!, dataIndex)}
            icon={<SearchOutlined />}
            size="small"
            style={{ width: 90 }}
          >
            Search
          </PlanarianButton>
          <Button
            onClick={() => {
              handleReset(clearFilters!); // reset the filter
              confirm(); // immediately apply the reset
            }}
            size="small"
            style={{ width: 90 }}
          >
            Reset
          </Button>
        </Space>
      </div>
    ),
    filterIcon: (filtered: boolean) => (
      <SearchOutlined style={{ color: filtered ? "#1677ff" : undefined }} />
    ),
    onFilter: (value, record) =>
      !!record[dataIndex as keyof TagTypeTableVm]
        ? record[dataIndex as keyof TagTypeTableVm]
            .toString()
            .toLowerCase()
            .includes((value as string).toLowerCase())
        : false,

    onFilterDropdownVisibleChange: (visible: boolean) => {
      if (visible) {
        setTimeout(() => searchInput.current?.select(), 100);
      }
    },
    render: (text: string) =>
      searchedColumn === dataIndex ? (
        <Highlighter
          highlightStyle={{ backgroundColor: "#ffc069", padding: 0 }}
          searchWords={[searchText]}
          autoEscape
          textToHighlight={text ? text.toString() : ""}
        />
      ) : (
        text
      ),
  });

  const handleSearch = (
    selectedKeys: React.Key[],
    confirm: () => void,
    dataIndex: string
  ) => {
    confirm();
    setSearchText(selectedKeys[0] as string);
    setSearchedColumn(dataIndex);
  };

  const handleReset = (clearFilters: () => void) => {
    clearFilters();
    setSearchText("");
  };
  const columns = [
    {
      title: "Name",
      dataIndex: "name",
      key: "name",
      ...getColumnSearchProps("name"), // Apply the search props to this column
      sorter: (a: TagTypeTableVm, b: TagTypeTableVm) =>
        a.name.localeCompare(b.name), // Enable sorting

      render: (text: string, record: TagTypeTableVm) => {
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
    },
    {
      title: "Occurrences",
      dataIndex: "occurrences",
      key: "occurrences",
      sorter: (a: TagTypeTableVm, b: TagTypeTableVm) =>
        a.occurrences - b.occurrences,
      // defaultSortOrder: "descend",
    },
    {
      title: "Action",
      key: "action",
      render: (text: string, record: TagTypeTableVm) => (
        <span>
          <Space>
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
            {record.tagTypeId !== editedTagType?.tagTypeId && (
              <Button
                onClick={() => {
                  setSingleMergeTagTypeId(record.tagTypeId);
                  setIsMergeModalOpen(true);
                }}
              >
                Merge
              </Button>
            )}
            {record.isUserModifiable && (
              <>
                <ConfirmationModalComponent
                  okText="Yes"
                  cancelText="No"
                  modalMessage={
                    <>
                      Are you positive you want to delete <b>ALL</b> '
                      {record.name}' tags?! It will remove the tag from{" "}
                      {record.occurrences} caves. This is an irreversible
                      action!
                    </>
                  }
                  confirmationWord={`DELETE ALL ${record.name.toUpperCase()} TAGS`}
                  onConfirm={() => onDelete(record.tagTypeId)}
                >
                  Delete
                </ConfirmationModalComponent>
              </>
            )}
          </Space>
        </span>
      ),
    },
  ] as ColumnsType<TagTypeTableVm>;

  const onMassMerge = async () => {
    try {
      const values = await mergeForm.validateFields();
      const destinationTagTypeId = values.destinationTag;
      let sourceTagTypeIds = selectedRowKeys;
      if (singleMergeTagTypeId) sourceTagTypeIds = [singleMergeTagTypeId];

      if (!destinationTagTypeId || sourceTagTypeIds.length === 0) {
        message.error("Please select tags and a target tag for merging!");
        return;
      }

      setIsMergeModalOpen(false);
      mergeForm.resetFields();
      setIsLoading(true);

      // Perform the merge operation on the server
      await AccountService.MergeTagTypes(
        sourceTagTypeIds,
        destinationTagTypeId
      );

      // Manually calculate the new occurrences for the merged tag type and adjust the local state
      setLocalTagTypes((prev) => {
        let newOccurrence = 0;

        // First map to adjust sourceTagType occurrences and calculate newOccurrence
        const intermediateTagTypes = prev.map((t) => {
          if (sourceTagTypeIds.includes(t.tagTypeId)) {
            newOccurrence += t.occurrences;
            return { ...t, occurrences: 0 }; // Return a new object with occurrences set to 0
          }
          return t; // Return the original object if no change is needed
        });

        // Second map to adjust the destinationTagType occurrences
        const updatedTagTypes = intermediateTagTypes.map((t) => {
          if (t.tagTypeId === destinationTagTypeId) {
            return { ...t, occurrences: t.occurrences + newOccurrence }; // Return a new object with updated occurrences
          }
          return t; // Return the original object if no change is needed
        });

        return updatedTagTypes;
      });

      message.success("Tag Types merged successfully!");
    } catch (error) {
      message.error("Failed to merge Tag Types!");
    } finally {
      setIsLoading(false);
      setSelectedRowKeys([]); // Clear the selection
      setSingleMergeTagTypeId(null); // Clear the single merge tag type id
    }
  };

  const sortedAndFilteredTagTypes = useMemo(() => {
    return localTagTypes
      .filter((tagType) => !selectedRowKeys.includes(tagType.tagTypeId)) // Exclude selected items
      .filter((tagType) => tagType.tagTypeId !== singleMergeTagTypeId) // Exclude the single merge tag type
      .sort((a, b) => a.name.localeCompare(b.name));
  }, [localTagTypes, selectedRowKeys, singleMergeTagTypeId]);

  return (
    <div>
      <Space
        direction="vertical"
        // style={{ width: "100%" }}
      >
        {!hasSelected && (
          <Row>
            {" "}
            <Col>
              {" "}
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
                <Form.Item
                  name={nameof<CreateEditTagTypeVm>("key")}
                  initialValue={tagType}
                  style={{ display: "none" }}
                >
                  <Input type="hidden" />
                </Form.Item>
                <Form.Item shouldUpdate>
                  <Button type="primary" onClick={onAdd}>
                    Add New
                  </Button>
                </Form.Item>
              </Form>
            </Col>
          </Row>
        )}
        {hasSelected && (
          <Space>
            <Button type="primary" onClick={() => setIsMergeModalOpen(true)}>
              Merge Selected
            </Button>
            <ConfirmationModalComponent
              okText="Yes"
              cancelText="No"
              modalMessage={
                <>
                  Are you sure you want to delete these tag types? This is an
                  irreversible action!
                </>
              }
              confirmationWord={`DELETE SELECTED TAGS`}
              onConfirm={onMassDeleteConfirm}
            >
              Delete Selected
            </ConfirmationModalComponent>
          </Space>
        )}
        <Table
          columns={columns}
          dataSource={localTagTypes}
          rowKey="tagTypeId"
          loading={isLoading}
          scroll={{ y: 400 }}
          rowSelection={rowSelection}
          pagination={{
            current: currentPage,
            pageSize: pageSize,
            onChange: (page, pageSize) => {
              setCurrentPage(page);
              setPageSize(pageSize);
            },
          }}
        />
      </Space>
      <Modal
        title="Merge Selected Items"
        open={isMergeModalOpen}
        onOk={onMassMerge}
        onCancel={
          () => {
            setIsMergeModalOpen(false);
          } // Clear the single merge tag type id
        }
      >
        <Form form={mergeForm}>
          <Form.Item
            name={nameof<MergeForm>("destinationTag")}
            rules={[
              { required: true, message: "Please select the destination tag!" },
            ]}
          >
            <Select
              showSearch
              filterOption={searchFilterSelectListItems}
              placeholder="Select Destination Tag "
            >
              {sortedAndFilteredTagTypes.map((tagType) => (
                <Select.Option
                  key={tagType.tagTypeId}
                  value={tagType.tagTypeId}
                >
                  {tagType.name}
                </Select.Option>
              ))}
            </Select>
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
};

export default TagTypeEditComponent;
