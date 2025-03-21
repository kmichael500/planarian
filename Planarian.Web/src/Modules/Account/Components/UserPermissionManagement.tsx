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
import {
  GetPermissionKeyDisplay,
  PermissionKey,
} from "../../Authentication/Models/PermissionKey";
import { CountyTagComponent } from "../../../Shared/Components/Display/CountyTagComponent";
import { nameof } from "../../../Shared/Helpers/StringHelpers";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { AppService } from "../../../Shared/Services/AppService";

const { Text } = Typography;

interface UserPermissionManagementProps {
  userId: string;
  permissionKey: PermissionKey;
  maxCaveSelectCount?: number | null;
}

export const UserPermissionManagement: React.FC<
  UserPermissionManagementProps
> = ({ userId, maxCaveSelectCount = null, permissionKey }) => {
  const [loading, setLoading] = useState<boolean>(true);
  const [saveLoading, setSaveLoading] = useState<boolean>(false);

  const [everythingDisabled, setEverythingDisabled] = useState<boolean>(true);

  const isAdminManager = AppService.HasPermission(PermissionKey.AdminManager);

  const [form] = Form.useForm<CavePermissionManagementVm>();

  const [isSearchModalOpen, setIsSearchModalOpen] = useState<boolean>(false);
  const [searchTerm, setSearchTerm] = useState<string>("");
  const [searchResults, setSearchResults] = useState<CaveSearchVm[]>([]);
  const [searching, setSearching] = useState<boolean>(false);
  const [currentPage, setCurrentPage] = useState<number>(1);
  const [totalPages, setTotalPages] = useState<number>(1);
  const pageSize = 10;

  useEffect(() => {
    const loadPermissions = async () => {
      setLoading(true);
      try {
        const data: CavePermissionManagementVm =
          await AccountUserManagerService.GetCavePermissions(
            userId,
            permissionKey
          );

        await handleSearchCaves(1);

        form.setFieldsValue({
          hasAllLocations: data.hasAllLocations,
          stateCountyValues: data.stateCountyValues ?? {
            states: [],
            countiesByState: {},
          },
          cavePermissions: data.cavePermissions ?? [],
        });

        if (
          data.hasAllLocations &&
          AppService.HasPermission(PermissionKey.Admin)
        ) {
          setEverythingDisabled(false);
        } else {
          setEverythingDisabled(false);
        }
      } catch (error) {
        message.error("Failed to load user location permissions.");
        console.error(error);
      } finally {
        setLoading(false);
      }
    };
    loadPermissions();
  }, [userId, form]);

  const handleFormSubmit = async (values: CavePermissionManagementVm) => {
    const newPermissions: CreateUserCavePermissionsVm = {
      hasAllLocations: values.hasAllLocations,
      countyIds: Object.values(values.stateCountyValues.countiesByState).flat(),
      caveIds: values.cavePermissions.map((cave) => cave.value),
    };

    setSaveLoading(true);
    try {
      await AccountUserManagerService.UpdateCavePermissions(
        userId,
        permissionKey,
        newPermissions
      );
      message.success("Permissions updated!");
    } catch (err) {
      const error = err as ApiErrorResponse;
      message.error(error.message);
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
    setSearching(true);
    try {
      const pagedResult: PagedResult<CaveSearchVm> =
        await CaveService.SearchCavesPaged(
          searchTerm,
          pageNumber,
          pageSize,
          PermissionKey.Manager
        );

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
        requestUserHasAccess: true,
      },
    };

    if (!existingCaveIds.some((c: any) => c.value.caveId === cave.id)) {
      form.setFieldsValue({
        cavePermissions: [...existingCaveIds, newCave],
      });
    }
  };

  return (
    <Card
      title={`${GetPermissionKeyDisplay(permissionKey)} Permission`}
      loading={loading}
    >
      <Form<CavePermissionManagementVm>
        form={form}
        layout="vertical"
        onFinish={handleFormSubmit}
        disabled={everythingDisabled}
      >
        <Form.Item
          label={<Text strong>Has access to ALL locations:</Text>}
          name={nameof<CavePermissionManagementVm>("hasAllLocations")}
          valuePropName="checked"
        >
          <Switch disabled={!isAdminManager} />
        </Form.Item>

        <Form.Item
          label={<Text strong>County Access (by State):</Text>}
          name={nameof<CavePermissionManagementVm>("stateCountyValues")}
        >
          <StateCountySelect
            permissionKey={PermissionKey.Manager}
            disabled={hasAllLocations}
          />
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
                    const cave = form.getFieldValue("cavePermissions")[
                      field.name
                    ] as SelectListItemWithData<
                      string,
                      CavePermissionManagementData
                    >;
                    return (
                      <List.Item
                        actions={[
                          <Button
                            danger
                            size="small"
                            icon={<DeleteOutlined />}
                            disabled={
                              hasAllLocations || !cave.data.requestUserHasAccess
                            }
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
