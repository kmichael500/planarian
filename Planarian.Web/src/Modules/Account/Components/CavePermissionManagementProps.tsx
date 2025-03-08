import React, { useEffect, useState } from "react";
import {
  Card,
  Switch,
  Button,
  List,
  Modal,
  Input,
  Typography,
  message,
  Form,
} from "antd";
import {
  PlusOutlined,
  DeleteOutlined,
  LeftOutlined,
  RightOutlined,
} from "@ant-design/icons";

import {
  CavePermissionManagementData,
  CavePermissionManagementVm,
  CreateUserCavePermissionsVm,
} from "../Models/UserLocationPermissionsVm";

import { CaveService } from "../../Caves/Service/CaveService";
import { CaveSearchVm } from "../../Caves/Models/CaveSearchVm";
import { SelectListItemWithData } from "../../../Shared/Models/SelectListItem";
import { PagedResult } from "../../Search/Models/PagedResult";
import { AddButtonComponent } from "../../../Shared/Components/Buttons/AddButtonComponent";

import { StateCountySelect } from "../../Tag/Components/StateCountySelect";
import { AccountUserManagerService } from "../Services/UserManagerService";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import { CountyTagComponent } from "../../../Shared/Components/Display/CountyTagComponent";
import { nameof } from "../../../Shared/Helpers/StringHelpers";

const { Text } = Typography;

interface CavePermissionManagementProps {
  userId: string;
  permissionKey: PermissionKey;
  maxCaveSelectCount?: number | null;
}

/**
 * Manages user location permissions: states/counties & individual caves, using a typed Ant Design Form.
 */
export const CavePermissionManagement: React.FC<
  CavePermissionManagementProps
> = ({ userId, maxCaveSelectCount = null, permissionKey }) => {
  const [loading, setLoading] = useState<boolean>(true);
  const [saveLoading, setSaveLoading] = useState<boolean>(false);

  // Typed Ant Design Form instance
  const [form] = Form.useForm<CavePermissionManagementVm>();

  // -------------
  // Cave searching (local state)
  // -------------
  const [isSearchModalOpen, setIsSearchModalOpen] = useState<boolean>(false);
  const [searchTerm, setSearchTerm] = useState<string>("");
  const [searchResults, setSearchResults] = useState<CaveSearchVm[]>([]);
  const [searching, setSearching] = useState<boolean>(false);
  const [currentPage, setCurrentPage] = useState<number>(1);
  const [totalPages, setTotalPages] = useState<number>(1);
  const pageSize = 10;

  // Load existing permissions from server on mount
  useEffect(() => {
    const loadPermissions = async () => {
      setLoading(true);
      try {
        const data: CavePermissionManagementVm =
          await AccountUserManagerService.GetLocationPermissions(
            userId,
            permissionKey
          );

        console.log(data);

        // Populate form fields. If we want to "reverse-engineer" states from countyIds,
        // we'd do that here. For now, we just set it empty so the user can re‐select.
        form.setFieldsValue({
          hasAllLocations: data.hasAllLocations,
          stateCountyValues: data.stateCountyValues ?? {
            states: [],
            countiesByState: {},
          },
          cavePermissions: data.cavePermissions ?? [],
        });
      } catch (error) {
        message.error("Failed to load user location permissions.");
        console.error(error);
      } finally {
        setLoading(false);
      }
    };
    loadPermissions();
  }, [userId, form]);

  /**
   * Form submit handler (called by onFinish).
   */
  const handleFormSubmit = async (values: CavePermissionManagementVm) => {
    const newPermissions: CreateUserCavePermissionsVm = {
      hasAllLocations: values.hasAllLocations,
      countyIds: Object.values(values.stateCountyValues.countiesByState).flat(),
      caveIds: values.cavePermissions.map((cave) => cave.value),
    };

    setSaveLoading(true);
    try {
      await AccountUserManagerService.UpdateLocationPermissions(
        userId,
        permissionKey,
        newPermissions
      );
      message.success("Permissions updated!");
    } catch (err) {
      message.error("Failed to save permissions.");
      console.error(err);
    } finally {
      setSaveLoading(false);
    }
  };

  const hasAllLocations = Form.useWatch(
    nameof<CavePermissionManagementVm>("hasAllLocations"),
    form
  );
  const caveIds: SelectListItemWithData<
    string,
    CavePermissionManagementData
  >[] =
    Form.useWatch(
      nameof<CavePermissionManagementVm>("cavePermissions"),
      form
    ) || [];

  const handleSearchCaves = async (pageNumber: number = 1) => {
    if (!searchTerm) {
      setSearchResults([]);
      return;
    }
    setSearching(true);
    try {
      const pagedResult: PagedResult<CaveSearchVm> =
        await CaveService.SearchCavesPaged(searchTerm, pageNumber, pageSize);

      setSearchResults(pagedResult.results);
      setCurrentPage(pagedResult.pageNumber);
      setTotalPages(pagedResult.totalPages);
    } catch (error) {
      message.error("Error searching for caves");
      console.error(error);
    } finally {
      setSearching(false);
    }
  };

  const handleAddCave = async (cave: CaveSearchVm) => {
    const existingCaveIds =
      form.getFieldValue(
        nameof<CavePermissionManagementVm>("cavePermissions")
      ) || [];

    if (
      maxCaveSelectCount !== null &&
      existingCaveIds.length >= maxCaveSelectCount
    ) {
      message.error(`Cannot add more than ${maxCaveSelectCount} caves.`);
      return;
    }

    const newCave: SelectListItemWithData<
      string,
      CavePermissionManagementData
    > = {
      value: cave.id,
      display: cave.name,
      data: {
        countyId: cave.countyId,
      },
    };

    if (!existingCaveIds.some((c: any) => c.value.caveId === cave.id)) {
      form.setFieldsValue({
        cavePermissions: [...existingCaveIds, newCave],
      });
    }
  };

  return (
    <Card title="Manage Location Permissions" loading={loading}>
      <Form<CavePermissionManagementVm>
        form={form}
        layout="vertical"
        onFinish={handleFormSubmit}
      >
        {/* Has ALL Locations */}
        <Form.Item
          label={<Text strong>Has access to ALL locations:</Text>}
          name={nameof<CavePermissionManagementVm>("hasAllLocations")}
          valuePropName="checked"
        >
          <Switch />
        </Form.Item>

        <Form.Item
          label={<Text strong>County Access (by State):</Text>}
          name={nameof<CavePermissionManagementVm>("stateCountyValues")}
        >
          <StateCountySelect disabled={hasAllLocations} />
        </Form.Item>

        <Form.Item
          label={<Text strong>Individual Caves:</Text>}
          style={{ marginBottom: 0 }}
        >
          <Form.List
            name={nameof<CavePermissionManagementVm>("cavePermissions")}
          >
            {(fields, { remove }) => {
              // If there are no caves, show a placeholder
              if (!fields.length) {
                return <Text type="secondary">No caves selected</Text>;
              }

              return (
                <List
                  style={{ marginTop: 12 }}
                  bordered
                  dataSource={fields}
                  renderItem={(field) => {
                    const cave =
                      form.getFieldValue("cavePermissions")[field.name];
                    return (
                      <List.Item
                        actions={[
                          <Button
                            danger
                            size="small"
                            icon={<DeleteOutlined />}
                            disabled={hasAllLocations}
                            onClick={() => remove(field.name)}
                          />,
                        ]}
                      >
                        <List.Item.Meta
                          title={cave?.display}
                          description={
                            <div
                              style={{ display: "flex", alignItems: "center" }}
                            >
                              <span>County:&nbsp;</span>
                              <CountyTagComponent
                                countyId={cave?.data?.countyId}
                              />
                            </div>
                          }
                        />
                      </List.Item>
                    );
                  }}
                />
              );
            }}
          </Form.List>
        </Form.Item>

        <div style={{ marginTop: 8 }}>
          <Button
            icon={<PlusOutlined />}
            onClick={() => setIsSearchModalOpen(true)}
            disabled={hasAllLocations}
          >
            Add Cave
          </Button>
        </div>

        <Form.Item style={{ textAlign: "right", marginTop: 16 }}>
          <Button type="primary" htmlType="submit" loading={saveLoading}>
            Save Changes
          </Button>
        </Form.Item>
      </Form>

      <Modal
        title="Search for Caves"
        open={isSearchModalOpen}
        onCancel={() => setIsSearchModalOpen(false)}
        footer={null}
        destroyOnClose
      >
        <Input.Search
          placeholder="Enter cave name..."
          enterButton="Search"
          onSearch={() => handleSearchCaves(1)}
          onChange={(e) => setSearchTerm(e.target.value)}
          loading={searching}
          style={{ marginBottom: 16 }}
        />

        <List
          loading={searching}
          dataSource={searchResults}
          locale={{ emptyText: "No caves found" }}
          renderItem={(cave) => {
            const existingCaveIds: SelectListItemWithData<
              string,
              CavePermissionManagementData
            >[] = form.getFieldValue("cavePermissions") || [];
            const alreadySelected = existingCaveIds.some(
              (c) => c.value === cave.id
            );

            return (
              <List.Item
                actions={[
                  <AddButtonComponent
                    disabled={alreadySelected || hasAllLocations}
                    type="link"
                    onClick={() => handleAddCave(cave)}
                  />,
                ]}
                style={{
                  opacity: alreadySelected ? 0.5 : 1,
                  pointerEvents: alreadySelected ? "none" : "auto",
                }}
              >
                <List.Item.Meta
                  title={cave.name}
                  description={
                    <div style={{ display: "flex" }}>
                      <span>County:&nbsp;</span>
                      <CountyTagComponent countyId={cave.countyId} />
                    </div>
                  }
                />
              </List.Item>
            );
          }}
        />

        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            marginTop: 16,
          }}
        >
          <Button
            icon={<LeftOutlined />}
            disabled={currentPage <= 1}
            onClick={() => handleSearchCaves(currentPage - 1)}
          >
            Previous
          </Button>
          <Text>
            Page {currentPage} of {totalPages}
          </Text>
          <Button
            icon={<RightOutlined />}
            disabled={currentPage >= totalPages}
            onClick={() => handleSearchCaves(currentPage + 1)}
          >
            Next
          </Button>
        </div>
      </Modal>
    </Card>
  );
};
