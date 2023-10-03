import { Avatar, Spin, Tooltip, Typography } from "antd";
import { AvatarSize } from "antd/lib/avatar/SizeContext";
import { useEffect, useState } from "react";
import { StringHelpers } from "../../../Shared/Helpers/StringHelpers";
import { NameProfilePhotoVm } from "../Models/NameProfilePhotoVm";
import { SettingsService } from "../../Setting/Services/SettingsService";

const { Text } = Typography;

export interface UserAvatarComponentProps {
  userId: string;
  size?: AvatarSize | undefined;
}

const UserAvatarComponent: React.FC<UserAvatarComponentProps> = (props) => {
  let [usersName, setUsersName] = useState<NameProfilePhotoVm>();
  let [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    if (usersName === undefined) {
      const getUsersName = async () => {
        const tripNameResponse = await SettingsService.GetUsersName(
          props.userId
        );

        setUsersName(tripNameResponse);
        setIsLoading(false);
      };
      getUsersName();
    }
  });

  return (
    <Spin spinning={isLoading}>
      {usersName !== undefined && (
        <Tooltip title={usersName.name} placement="top">
          <Avatar src={usersName.profilePhotoUrl} size={props.size}>
            {StringHelpers.GenerateAbbreviation(usersName.name)}
          </Avatar>
        </Tooltip>
      )}
    </Spin>
  );
};

export { UserAvatarComponent };
