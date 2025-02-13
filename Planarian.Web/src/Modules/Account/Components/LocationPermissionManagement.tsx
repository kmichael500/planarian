import React, { useEffect, useState } from "react";
import {
  Card,
  Switch,
  Select,
  Button,
  List,
  Modal,
  Input,
  Typography,
  message,
} from "antd";
import {
  PlusOutlined,
  DeleteOutlined,
  LeftOutlined,
  RightOutlined,
} from "@ant-design/icons";
import {
  UserLocationPermissionCaveVm,
  UserLocationPermissionsVm,
} from "../Models/UserLocationPermissionsVm";
import { CaveService } from "../../Caves/Service/CaveService";
import { CaveSearchVm } from "../../Caves/Models/CaveSearchVm";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";
import { UserLocationPermissionService } from "../Services/PermissionService";
import { PagedResult } from "../../Search/Models/PagedResult";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { AddButtonComponent } from "../../../Shared/Components/Buttons/AddButtonComponent";

const { Text } = Typography;

interface LocationPermissionManagementProps {
  userId: string;
  maxCaveSelectCount?: number | null;
  counties: SelectListItem<UserLocationPermissionCaveVm>[];
}

export const LocationPermissionManagement: React.FC<
  LocationPermissionManagementProps
> = ({ userId, maxCaveSelectCount = null, counties }) => {
  const [permissions, setPermissions] = useState<UserLocationPermissionsVm>({
    hasAllLocations: false,
    countyIds: [],
    caveIds: [],
  });
  const [loading, setLoading] = useState<boolean>(true);
  const [saveLoading, setSaveLoading] = useState<boolean>(false);
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
        const data = await UserLocationPermissionService.getLocationPermissions(
          userId
        );
        setPermissions(data);
      } catch (error) {
        message.error("Failed to load user location permissions.");
        console.error(error);
      } finally {
        setLoading(false);
      }
    };
    void loadPermissions();
  }, [userId]);

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

  const handleAddCave = (cave: CaveSearchVm) => {
    if (
      maxCaveSelectCount !== null &&
      permissions.caveIds.length >= maxCaveSelectCount
    ) {
      message.warning(`Cannot add more than ${maxCaveSelectCount} caves.`);
      return;
    }

    const newCave: SelectListItem<UserLocationPermissionCaveVm> = {
      value: {
        caveId: cave.id,
        countyId: cave.countyId,
      },
      display: cave.name,
    };

    if (!permissions.caveIds.some((c) => c.value === newCave.value)) {
      setPermissions((prev) => ({
        ...prev,
        caveIds: [...prev.caveIds, newCave],
      }));
    }
  };

  const handleRemoveCave = (caveId: string) => {
    setPermissions((prev) => ({
      ...prev,
      caveIds: prev.caveIds.filter((c) => c.value.caveId !== caveId),
    }));
  };

  const handleSave = async () => {
    setSaveLoading(true);
    try {
      await UserLocationPermissionService.updateLocationPermissions(
        userId,
        permissions
      );
      message.success("Permissions updated!");
    } catch (err) {
      message.error("Failed to save permissions.");
      console.error(err);
    } finally {
      setSaveLoading(false);
    }
  };

  return (
    <Card title="Manage Location Permissions" loading={loading}>
      <div style={{ marginBottom: 16 }}>
        <Text strong>Has access to ALL locations: </Text>
        <Switch
          checked={permissions.hasAllLocations}
          onChange={(checked) =>
            setPermissions((prev) => ({
              ...prev,
              hasAllLocations: checked,
            }))
          }
        />
      </div>

      <div style={{ marginBottom: 16 }}>
        <Text strong>County Access:</Text>
        <Select
          style={{ width: "100%", marginTop: 8 }}
          mode="multiple"
          placeholder="Select counties"
          disabled={permissions.hasAllLocations}
          value={permissions.countyIds}
          onChange={(values: string[]) =>
            setPermissions((prev) => ({ ...prev, countyIds: values }))
          }
        >
          {counties.map((c) => (
            <Select.Option key={c.value.caveId} value={c.value.caveId}>
              {c.display}
            </Select.Option>
          ))}
        </Select>
      </div>

      <div style={{ marginBottom: 16 }}>
        <Text strong>Individual Caves:</Text>
        <div style={{ marginTop: 8 }}>
          <Button
            icon={<PlusOutlined />}
            onClick={() => setIsSearchModalOpen(true)}
            disabled={permissions.hasAllLocations}
          >
            Add Cave
          </Button>
        </div>
        {permissions.caveIds.length > 0 && (
          <List
            style={{ marginTop: 12 }}
            bordered
            dataSource={permissions.caveIds}
            renderItem={(cave) => (
              <List.Item
                actions={[
                  <Button
                    danger
                    size="small"
                    icon={<DeleteOutlined />}
                    onClick={() => handleRemoveCave(cave.value.caveId)}
                  />,
                ]}
              >
                <List.Item.Meta
                  title={cave.display}
                  description={`County: ${cave.value.countyId}`}
                />
              </List.Item>
            )}
          />
        )}
      </div>

      <div style={{ textAlign: "right" }}>
        <Button type="primary" onClick={handleSave} loading={saveLoading}>
          Save Changes
        </Button>
      </div>

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
          renderItem={(cave) => (
            <List.Item
              actions={[
                <AddButtonComponent
                  disabled={permissions.caveIds.some(
                    (c) => c.value.caveId === cave.id
                  )}
                  type="link"
                  onClick={() => handleAddCave(cave)}
                />,
              ]}
              style={{
                opacity: permissions.caveIds.some(
                  (c) => c.value.caveId === cave.id
                )
                  ? 0.5
                  : 1,
                pointerEvents: permissions.caveIds.some(
                  (c) => c.value.caveId === cave.id
                )
                  ? "none"
                  : "auto",
              }}
            >
              <List.Item.Meta
                title={cave.name}
                description={`County: ${cave.countyId}`}
              >
                Test?
              </List.Item.Meta>
            </List.Item>
          )}
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
