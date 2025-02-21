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
import { AddButtonComponent } from "../../../Shared/Components/Buttons/AddButtonComponent";
import {
  StateCountySelect,
  StateCountyValue,
} from "../../Tag/Components/StateCountySelect";

const { Text } = Typography;

interface LocationPermissionManagementProps {
  userId: string;
  maxCaveSelectCount?: number | null;
}

/**
 * Manages user location permissions: states/counties & individual caves.
 */
export const LocationPermissionManagement: React.FC<
  LocationPermissionManagementProps
> = ({ userId, maxCaveSelectCount = null }) => {
  const [permissions, setPermissions] = useState<UserLocationPermissionsVm>({
    hasAllLocations: false,
    countyIds: [],
    caveIds: [],
  });

  const [loading, setLoading] = useState<boolean>(true);
  const [saveLoading, setSaveLoading] = useState<boolean>(false);

  // -------------
  // Caves searching
  // -------------
  const [isSearchModalOpen, setIsSearchModalOpen] = useState<boolean>(false);
  const [searchTerm, setSearchTerm] = useState<string>("");
  const [searchResults, setSearchResults] = useState<CaveSearchVm[]>([]);
  const [searching, setSearching] = useState<boolean>(false);
  const [currentPage, setCurrentPage] = useState<number>(1);
  const [totalPages, setTotalPages] = useState<number>(1);
  const pageSize = 10;

  // -------------
  // State + County selections
  // -------------
  const [stateCountyValue, setStateCountyValue] = useState<StateCountyValue>({
    states: [],
    countiesByState: {},
  });

  // Load existing permissions on mount
  useEffect(() => {
    const loadPermissions = async () => {
      setLoading(true);
      try {
        const data = await UserLocationPermissionService.getLocationPermissions(
          userId
        );
        setPermissions(data);

        // If you want to also reflect existing countyIds
        // back into StateCountySelect, we can't do that
        // unless we also have stored states. So for now,
        // we only set the counties. If needed, you'd have to
        // figure out which states those counties belong to
        // and populate `states` + `countiesByState`.
        //
        // For demonstration, we won't try to 'reverse engineer'
        // which states the user had. We'll keep the input empty.
      } catch (error) {
        message.error("Failed to load user location permissions.");
        console.error(error);
      } finally {
        setLoading(false);
      }
    };
    void loadPermissions();
  }, [userId]);

  /**
   * Called when the user changes states/counties in StateCountySelect.
   * We flatten all selected counties into a single array for permissions.countyIds.
   */
  const handleStateCountyChange = (newValue: StateCountyValue) => {
    setStateCountyValue(newValue);
    const allSelectedCountyIds = Object.values(newValue.countiesByState).flat();
    setPermissions((prev) => ({
      ...prev,
      countyIds: allSelectedCountyIds,
    }));
  };

  // -------------
  // Cave Searching
  // -------------
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

  /**
   * User adds a cave from the search modal
   */
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

    // Add if not already present
    if (
      !permissions.caveIds.some((c) => c.value.caveId === newCave.value.caveId)
    ) {
      setPermissions((prev) => ({
        ...prev,
        caveIds: [...prev.caveIds, newCave],
      }));
    }
  };

  /**
   * Remove a cave from the existing list
   */
  const handleRemoveCave = (caveId: string) => {
    setPermissions((prev) => ({
      ...prev,
      caveIds: prev.caveIds.filter((c) => c.value.caveId !== caveId),
    }));
  };

  /**
   * Save permissions to server
   */
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
      {/* Has ALL Locations */}
      <div style={{ marginBottom: 16 }}>
        <Text strong>Has access to ALL locations: </Text>
        <Switch
          checked={permissions.hasAllLocations}
          onChange={(checked) =>
            setPermissions((prev) => ({
              ...prev,
              hasAllLocations: checked,
              // If user toggles ON "All Locations", clear counties + caves
              ...(checked ? { countyIds: [], caveIds: [] } : {}),
            }))
          }
        />
      </div>

      {/* State/County Access using StateCountySelect */}
      <div style={{ marginBottom: 16 }}>
        <Text strong>County Access (by State):</Text>
        <div style={{ marginTop: 8 }}>
          <StateCountySelect
            disabled={permissions.hasAllLocations}
            value={stateCountyValue}
            onChange={handleStateCountyChange}
          />
        </div>
      </div>

      {/* Individual Caves */}
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

      {/* Save button */}
      <div style={{ textAlign: "right" }}>
        <Button type="primary" onClick={handleSave} loading={saveLoading}>
          Save Changes
        </Button>
      </div>

      {/* Modal for searching caves */}
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
            const alreadySelected = permissions.caveIds.some(
              (c) => c.value.caveId === cave.id
            );

            return (
              <List.Item
                actions={[
                  <AddButtonComponent
                    disabled={alreadySelected}
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
                  description={`County: ${cave.countyId}`}
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
