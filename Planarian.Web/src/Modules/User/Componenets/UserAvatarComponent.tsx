import { Avatar, Spin, Tooltip, Typography } from "antd";
import { AvatarSize } from "antd/lib/avatar/SizeContext";
import { useState, useEffect } from "react";
import { StringHelpers } from "../../../Shared/Helpers/StringHelpers";
import { NameProfilePhotoVm } from "../Models/NameProfilePhotoVm";
import { SettingsService } from "../../Settings/Services/settings.service";

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
      const getTripObjectiveName = async () => {
        const tripObjectiveNameResponse = await SettingsService.GetUsersName(
          props.userId
        );
        console.log(tripObjectiveNameResponse);
        setUsersName(tripObjectiveNameResponse);
        setIsLoading(false);
      };
      getTripObjectiveName();
    }
  });

  return (
    <Spin spinning={isLoading}>
      {usersName !== undefined && (
        <Tooltip title={usersName.name} placement="top">
          <Avatar src={usersName.profilePhotoUrl} size={props.size}>
            {StringHelpers.NameToInitials(usersName.name)}
          </Avatar>
        </Tooltip>
      )}
    </Spin>
  );
};

export { UserAvatarComponent };
