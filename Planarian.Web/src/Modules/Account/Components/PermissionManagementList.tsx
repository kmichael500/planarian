import React, { useEffect, useState } from "react";
import { Card, List, Typography, message } from "antd";
import { Link } from "react-router-dom";

// Adjust these imports to match your project’s file structure
import { EditButtonComponentt } from "../../../Shared/Components/Buttons/EditButtonComponent";
// or wherever your HttpClient is located
import { SelectListItemDescriptionData } from "../../../Shared/Models/SelectListItem";
import { PlanarianError } from "../../../Shared/Exceptions/PlanarianErrors";
import { HttpClient } from "@microsoft/signalr";
import { PermissionSelectListData } from "../Models/PermissionSelectListData";
import { AccountUserManagerService } from "../Services/UserManagerService";

const { Text } = Typography;

interface PermissionManagementListProps {
  userId: string;
}

export const PermissionManagementList: React.FC<
  PermissionManagementListProps
> = ({ userId }) => {
  const [permissions, setPermissions] = useState<
    SelectListItemDescriptionData<string, PermissionSelectListData>[]
  >([]);
  const [loading, setLoading] = useState<boolean>(false);

  useEffect(() => {
    const loadPermissions = async () => {
      setLoading(true);
      try {
        const data = await AccountUserManagerService.GetPermissionSelectList();
        setPermissions(data);
      } catch (err) {
        console.error(err);
        const error = err as PlanarianError;
        message.error(error.message || "Failed to load permissions.");
      } finally {
        setLoading(false);
      }
    };

    loadPermissions();
  }, []);

  return (
    <Card title="Manage Permissions">
      <List
        loading={loading}
        dataSource={permissions}
        renderItem={(permission) => (
          <List.Item
            actions={[
              <Link to={`permissions/${permission.data?.key}`}>
                <EditButtonComponentt />
              </Link>,
            ]}
          >
            <List.Item.Meta
              title={<Text strong>{permission.display}</Text>}
              description={permission.data.description}
            />
          </List.Item>
        )}
      />
    </Card>
  );
};
