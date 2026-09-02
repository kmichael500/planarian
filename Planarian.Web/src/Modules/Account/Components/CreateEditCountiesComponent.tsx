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
import { AccountService } from "../Services/AccountService";
import {
  isNullOrWhiteSpace,
  nameof,
} from "../../../Shared/Helpers/StringHelpers";
import { searchFilterSelectListItems } from "../../../Shared/Helpers/ArrayHelpers";
import { TagTypeTableCountyVm } from "../Models/TagTypeTableCountyVm";
import { CreateCountyVm } from "../Models/CreateEditCountyVm";
import { PropertyLength } from "../../../Shared/Constants/PropertyLengthConstant";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";

interface CreateEditCountiesComponentProps {
  stateId: string;
}

interface CountyMergeForm {
  destinationCounty: string;
}

const CreateEditCountiesComponent = (
  props: CreateEditCountiesComponentProps
) => {
  const [editedCounty, setEditedCounty] = useState<TagTypeTableCountyVm | null>(
    null
  );

  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(15);

  const [isLoading, setIsLoading] = useState<boolean>(true);

  const [counties, setCounties] = useState<TagTypeTableCountyVm[]>([]);
  useEffect(() => {
    const getCounties = async () => {
      const result = await AccountService.GetCountiesForTable(props.stateId);
      setCounties(result);
      setIsLoading(false);
    };
    getCounties();
  }, [props.stateId]);

  const [localCounties, setLocalCounties] =
    useState<TagTypeTableCountyVm[]>(counties);
  const [form] = Form.useForm<CreateCountyVm>();
  const [mergeForm] = Form.useForm<CountyMergeForm>();

  const [searchText, setSearchText] = useState<string>("");
  const [searchedColumn, setSearchedColumn] = useState<string>("");
  const searchInput = useRef<InputRef>(null);
  const [isMergeModalOpen, setIsMergeModalOpen] = useState(false);

  const [selectedRowKeys, setSelectedRowKeys] = useState<string[]>([]);
  const [canDeleteSelected, setCanDeleteSelected] = useState(false);
  const onSelectChange = (selectedKeys: string[]) => {
    setSelectedRowKeys(selectedKeys);

    const allSelectedAreUnused = selectedKeys.every(
      (key) =>
        localCounties.find((county) => county.tagTypeId === key)
          ?.isUserModifiable
    );

    setCanDeleteSelected(allSelectedAreUnused);
  };

  useEffect(() => {
    setLocalCounties(counties);
  }, [counties]);

  const onAdd = async () => {
    let values: CreateCountyVm;
    try {
      try {
        values = await form.validateFields();
      } catch (error) {
        return;
      }

      setIsLoading(true);
      const newCounty = await AccountService.AddCounty(values, props.stateId);
      const insertionIndex = (currentPage - 1) * pageSize;
      setLocalCounties((prev) => [
        ...prev.slice(0, insertionIndex),
        newCounty,
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
      await AccountService.DeleteCounties(selectedRowKeys, props.stateId);
      setLocalCounties((prev) =>
        prev.filter((t) => !selectedRowKeys.includes(t.tagTypeId))
      );
      message.success("Counties deleted successfully!");
    } catch (error) {
      message.error("Failed to delete some Counties!");
    } finally {
      setIsLoading(false);
      setSelectedRowKeys([]); // Clear the selection
    }
  };

  const onSave = async (county: TagTypeTableCountyVm) => {
    try {
      if (isNullOrWhiteSpace(county.name)) {
        message.error("Name is required");
        return;
      }
      if (isNullOrWhiteSpace(county.countyDisplayId)) {
        message.error("County code is required");
        return;
      }
      if (county.countyDisplayId.length > PropertyLength.SMALL_TEXT) {
        message.error(
          `County code must be less than ${PropertyLength.SMALL_TEXT} characters!`
        );
        return;
      }
      if (county.name.length > PropertyLength.NAME) {
        message.error(
          `County name must be less than ${PropertyLength.NAME} characters!`
        );
        return;
      }
      setIsLoading(true);
      await AccountService.UpdateCounties(
        county.tagTypeId,
        props.stateId,
        county
      );
      setLocalCounties((prev) =>
        prev.map((t) => (t.tagTypeId === county.tagTypeId ? county : t))
      );
      message.success("County saved successfully!");
      setEditedCounty(null);
    } catch (err) {
      const error = err as ApiErrorResponse;
      message.error(error.message);
    } finally {
      setIsLoading(false);
    }
  };

  const onDelete = async (countyId: string) => {
    try {
      setIsLoading(true);
      await AccountService.DeleteCounties([countyId], props.stateId);
      setLocalCounties((prev) => prev.filter((t) => t.tagTypeId !== countyId));
      message.success("County deleted successfully!");
    } catch (error) {
      message.error("Failed to delete County!");
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
        if (editedCounty && editedCounty.tagTypeId === record.tagTypeId) {
          return (
            <Form onFinish={() => onSave(editedCounty)}>
              <Form.Item
                name={nameof<TagTypeTableCountyVm>("name")}
                initialValue={text}
                noStyle
              >
                <Input
                  autoFocus
                  onChange={(e) =>
                    setEditedCounty({ ...editedCounty, name: e.target.value })
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
        if (editedCounty && editedCounty.tagTypeId === record.tagTypeId) {
          return (
            <Form onFinish={() => onSave(editedCounty)}>
              <Form.Item
                name={nameof<TagTypeTableCountyVm>("countyDisplayId")}
                initialValue={text}
                noStyle
              >
                <Input
                  autoFocus
                  onChange={(e) =>
                    setEditedCounty({
                      ...editedCounty,
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
            {editedCounty && editedCounty.tagTypeId === record.tagTypeId ? (
              <>
                <Button onClick={() => onSave(editedCounty)} type="primary">
                  Save
                </Button>
                <Button onClick={() => setEditedCounty(null)}>Cancel</Button>
              </>
            ) : (
              <Button onClick={() => setEditedCounty(record)}>Edit</Button>
            )}
            {record.isUserModifiable && (
              <ConfirmationModalComponent
                okText="Yes"
                cancelText="No"
                modalMessage={
                  <>
                    Are you sure you want to delete <b>{record.name}</b>? This
                    County is unused and deleting it is irreversible.
                  </>
                }
                confirmationWord={`DELETE ${record.name.toUpperCase()}`}
                onConfirm={() => onDelete(record.tagTypeId)}
              >
                Delete
              </ConfirmationModalComponent>
            )}
          </Space>
        </span>
      ),
    },
  ] as ColumnsType<TagTypeTableCountyVm>;

  const onMassMerge = async () => {
    try {
      const values = await mergeForm.validateFields();
      const destinationCountyId = values.destinationCounty;
      const sourceCountyIds = selectedRowKeys;

      if (!destinationCountyId || sourceCountyIds.length === 0) {
        message.error("Please select Counties and a destination County.");
        return;
      }

      setIsMergeModalOpen(false);
      mergeForm.resetFields();
      setIsLoading(true);

      await AccountService.MergeCounties(
        sourceCountyIds,
        destinationCountyId,
        props.stateId
      );
      const counties = await AccountService.GetCountiesForTable(props.stateId);
      setCounties(counties);
      setLocalCounties(counties);
      message.success("Counties merged successfully!");
    } catch (err) {
      const error = err as ApiErrorResponse;
      message.error(error.message || "Failed to merge Counties.");
    } finally {
      setIsLoading(false);
      setSelectedRowKeys([]);
    }
  };

  const sortedAndFilteredCounties = useMemo(() => {
    return localCounties
      .filter((county) => !selectedRowKeys.includes(county.tagTypeId)) // Exclude selected Counties
      .sort((a, b) => a.name.localeCompare(b.name));
  }, [localCounties, selectedRowKeys]);

  return (
    <div>
      <Space direction="vertical" style={{ width: "100%" }}>
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
            {canDeleteSelected && (
              <ConfirmationModalComponent
                okText="Yes"
                cancelText="No"
                modalMessage={
                  <>
                    Are you sure you want to delete these unused Counties? This
                    is an irreversible action!
                  </>
                }
                confirmationWord={`DELETE SELECTED COUNTIES`}
                onConfirm={onMassDeleteConfirm}
              >
                Delete Selected
              </ConfirmationModalComponent>
            )}
          </Space>
        )}
        <Table
          columns={columns}
          dataSource={localCounties}
          rowKey="tagTypeId"
          loading={isLoading}
          scroll={{ x: "max-content" }}
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
        title="Merge Selected Counties"
        open={isMergeModalOpen}
        onOk={onMassMerge}
        onCancel={() => {
          setIsMergeModalOpen(false);
        }}
      >
        <Form form={mergeForm}>
          <Form.Item
            name={nameof<CountyMergeForm>("destinationCounty")}
            rules={[
              {
                required: true,
                message: "Please select the destination County!",
              },
            ]}
          >
            <Select
              showSearch
              filterOption={searchFilterSelectListItems}
              placeholder="Select Destination County"
            >
              {sortedAndFilteredCounties.map((county) => (
                <Select.Option key={county.tagTypeId} value={county.tagTypeId}>
                  {county.name}
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
