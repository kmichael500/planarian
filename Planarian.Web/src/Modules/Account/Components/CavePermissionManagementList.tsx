import React, { useEffect, useState } from "react";
import { Card, List, Typography, message } from "antd";
import { Link } from "react-router-dom";

import { EditButtonComponentt } from "../../../Shared/Components/Buttons/EditButtonComponent";
import { SelectListItemDescriptionData } from "../../../Shared/Models/SelectListItem";
import { PlanarianError } from "../../../Shared/Exceptions/PlanarianErrors";
import { PermissionSelectListData } from "../Models/PermissionSelectListData";
import { AccountUserManagerService } from "../Services/UserManagerService";
import { PermissionType } from "../../Authentication/Models/PermissionType";

const { Text } = Typography;

interface PermissionManagementListProps {
  userId: string;
}

export const CavePermissionManagementList: React.FC<
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
        const data = await AccountUserManagerService.GetPermissionSelectList(
          PermissionType.Cave
        );
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
    <Card title="Cave Permissions">
      <List
        loading={loading}
        dataSource={permissions.sort(
          (a, b) => a.data.sortOrder - b.data.sortOrder
        )}
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
