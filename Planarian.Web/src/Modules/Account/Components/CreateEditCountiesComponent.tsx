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
import { CreateEditTagTypeVm } from "../Models/CreateEditTagTypeVm";
import {
  isNullOrWhiteSpace,
  nameof,
} from "../../../Shared/Helpers/StringHelpers";
import { searchFilterSelectListItems } from "../../../Shared/Helpers/ArrayHelpers";
import { TagTypeTableCountyVm } from "../Models/TagTypeTableCountyVm";
import { MergeForm } from "../Models/MergeForm";
import { CreateCountyVm } from "../Models/CreateEditCountyVm";
import { PropertyLength } from "../../../Shared/Constants/PropertyLengthConstant";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";

interface CreateEditCountiesComponentProps {
  stateId: string;
}

const CreateEditCountiesComponent = (
  props: CreateEditCountiesComponentProps
) => {
  const [editedTagType, setEditedTagType] =
    useState<TagTypeTableCountyVm | null>(null);

  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(15); // Assuming default page size is 10

  const [isLoading, setIsLoading] = useState<boolean>(true);

  const [tagTypes, setTagTypes] = useState<TagTypeTableCountyVm[]>([]);
  useEffect(() => {
    const getTags = async () => {
      var tags = await AccountService.GetCountiesForTable(props.stateId);
      setTagTypes(tags);
      setIsLoading(false);
    };
    getTags();
  }, []);

  const [localTagTypes, setLocalTagTypes] =
    useState<TagTypeTableCountyVm[]>(tagTypes);
  const [form] = Form.useForm<CreateCountyVm>();
  const [mergeForm] = Form.useForm<MergeForm>();

  const [searchText, setSearchText] = useState<string>("");
  const [singleMergeTagTypeId, setSingleMergeTagTypeId] = useState<
    string | null
  >(null);

  const [searchedColumn, setSearchedColumn] = useState<string>("");
  const searchInput = useRef<InputRef>(null);
  const [isMergeModalOpen, setIsMergeModalOpen] = useState(false);

  const [selectedRowKeys, setSelectedRowKeys] = useState<string[]>([]);
  const [isDeleteSelectedDisabled, setIsDeleteSelectedDisabled] =
    useState(false);
  const onSelectChange = (selectedKeys: string[]) => {
    setSelectedRowKeys(selectedKeys);

    const anyNonModifiable = !selectedKeys.some(
      (key) =>
        !localTagTypes.find((tagType) => tagType.tagTypeId === key)
          ?.isUserModifiable
    );

    setIsDeleteSelectedDisabled(anyNonModifiable);
  };

  useEffect(() => {
    setLocalTagTypes(tagTypes);
  }, [tagTypes]);

  const onAdd = async () => {
    let values: CreateCountyVm;
    try {
      try {
        values = await form.validateFields();
      } catch (error) {
        return;
      }

      setIsLoading(true);
      const newTagType = await AccountService.AddCounty(values, props.stateId);
      const insertionIndex = (currentPage - 1) * pageSize;
      setLocalTagTypes((prev) => [
        ...prev.slice(0, insertionIndex),
        newTagType,
        ...prev.slice(insertionIndex),
      ]);
      form.resetFields();
      message.success("County added successfully!");
    } catch (err) {
      const error = err as ApiErrorResponse;
      message.error(error.message);
    } finally {
      setIsLoading(false);
    }
  };

  const rowSelection = {
    selectedRowKeys,
    onChange: onSelectChange,
  } as TableRowSelection<TagTypeTableCountyVm>;

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

  const onSave = async (tagType: TagTypeTableCountyVm) => {
    try {
      if (isNullOrWhiteSpace(tagType.name)) {
        message.error("Name is required");
        return;
      }
      if (isNullOrWhiteSpace(tagType.countyDisplayId)) {
        message.error("County code is required");
        return;
      }
      if (tagType.countyDisplayId.length > PropertyLength.SMALL_TEXT) {
        message.error(
          `County code must be less than ${PropertyLength.SMALL_TEXT} characters!`
        );
        return;
      }
      if (tagType.name.length > PropertyLength.NAME) {
        message.error(
          `County name must be less than ${PropertyLength.NAME} characters!`
        );
        return;
      }
      setIsLoading(true);
      await AccountService.UpdateCounties(
        tagType.tagTypeId,
        props.stateId,
        tagType
      );
      setLocalTagTypes((prev) =>
        prev.map((t) => (t.tagTypeId === tagType.tagTypeId ? tagType : t))
      );
      message.success("Tag Type saved successfully!");
      setEditedTagType(null);
    } catch (err) {
      const error = err as ApiErrorResponse;
      message.error(error.message);
    } finally {
      setIsLoading(false);
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
  ): ColumnType<TagTypeTableCountyVm> => ({
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
      !!record[dataIndex as keyof TagTypeTableCountyVm]
        ? record[dataIndex as keyof TagTypeTableCountyVm]
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
      dataIndex: nameof<TagTypeTableCountyVm>("name"),
      key: nameof<TagTypeTableCountyVm>("name"),
      ...getColumnSearchProps(nameof<TagTypeTableCountyVm>("name")),
      sorter: (a: TagTypeTableCountyVm, b: TagTypeTableCountyVm) =>
        a.name.localeCompare(b.name),

      render: (text: string, record: TagTypeTableCountyVm) => {
        if (editedTagType && editedTagType.tagTypeId === record.tagTypeId) {
          return (
            <Form onFinish={() => onSave(editedTagType)}>
              <Form.Item
                name={nameof<TagTypeTableCountyVm>("name")}
                initialValue={text}
                noStyle
              >
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
      title: "County Code",
      dataIndex: nameof<TagTypeTableCountyVm>("countyDisplayId"),
      key: nameof<TagTypeTableCountyVm>("countyDisplayId"),
      ...getColumnSearchProps(nameof<TagTypeTableCountyVm>("countyDisplayId")),
      sorter: (a: TagTypeTableCountyVm, b: TagTypeTableCountyVm) =>
        a.countyDisplayId.localeCompare(b.countyDisplayId),
      render: (text: string, record: TagTypeTableCountyVm) => {
        if (editedTagType && editedTagType.tagTypeId === record.tagTypeId) {
          return (
            <Form onFinish={() => onSave(editedTagType)}>
              <Form.Item
                name={nameof<TagTypeTableCountyVm>("countyDisplayId")}
                initialValue={text}
                noStyle
              >
                <Input
                  autoFocus
                  onChange={(e) =>
                    setEditedTagType({
                      ...editedTagType,
                      countyDisplayId: e.target.value,
                    })
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
      sorter: (a: TagTypeTableCountyVm, b: TagTypeTableCountyVm) =>
        a.occurrences - b.occurrences,
      // defaultSortOrder: "descend",
    },
    {
      title: "Action",
      key: "action",
      render: (text: string, record: TagTypeTableCountyVm) => (
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
            {/* {record.tagTypeId !== editedTagType?.tagTypeId && (
              <Button
                onClick={() => {
                  setSingleMergeTagTypeId(record.tagTypeId);
                  setIsMergeModalOpen(true);
                }}
              >
                Merge
              </Button>
            )} */}
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
  ] as ColumnsType<TagTypeTableCountyVm>;

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
                      message: "County name is required",
                    },
                    {
                      max: PropertyLength.NAME,
                      message: `County name must be less than ${PropertyLength.NAME} characters!`,
                    },
                  ]}
                >
                  <Input placeholder="County Name" />
                </Form.Item>
                <Form.Item
                  name={nameof<CreateCountyVm>("countyDisplayId")}
                  rules={[
                    {
                      required: true,
                      message: "County code is required.",
                    },
                    {
                      max: PropertyLength.SMALL_TEXT,
                      message: `County code must be less than ${PropertyLength.SMALL_TEXT} characters!`,
                    },
                  ]}
                >
                  <Input placeholder="County Code" />
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
            {isDeleteSelectedDisabled && (
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
            )}
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

export default CreateEditCountiesComponent;
