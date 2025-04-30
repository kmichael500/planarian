import { Avatar, Spin, Tooltip, Typography } from "antd";
import { useEffect, useState } from "react";
import { StringHelpers } from "../../../Shared/Helpers/StringHelpers";
import { NameProfilePhotoVm } from "../Models/NameProfilePhotoVm";
import { SettingsService } from "../../Setting/Services/SettingsService";
import { AvatarSize } from "antd/lib/avatar/AvatarContext";

const { Text } = Typography;

export interface UserAvatarComponentProps {
  userId: string;
  size?: AvatarSize | undefined;
  showName?: boolean;
}

const UserAvatarComponent: React.FC<UserAvatarComponentProps> = (props) => {
  let [usersName, setUsersName] = useState<NameProfilePhotoVm>();
  let [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    if (usersName === undefined) {
      const getUsersName = async () => {
        const response = await SettingsService.GetUsersName(props.userId);

        setUsersName(response);
        setIsLoading(false);
      };
      getUsersName();
    }
  });

  if (!isLoading && props.showName) {
    return <>{usersName?.name}</>;
  }

  return (
    <Spin spinning={isLoading}>
      {usersName !== undefined && (
        <>
          {props.showName && usersName.name}
          {!props.showName && (
            <Tooltip title={usersName.name} placement="top">
              <Avatar src={usersName.profilePhotoUrl} size={props.size}>
                {StringHelpers.GenerateAbbreviation(usersName.name)}
              </Avatar>
            </Tooltip>
          )}
        </>
      )}
    </Spin>
  );
};

export { UserAvatarComponent };
