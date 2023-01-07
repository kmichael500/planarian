import { Avatar, Spin, Tooltip, Typography } from "antd";
import { AvatarSize } from "antd/lib/avatar/SizeContext";
import { useState, useEffect } from "react";
import { StringHelpers } from "../Helpers/StringHelpers";
import { SettingsService } from "../Services/SettingsService";

const { Text } = Typography;
export interface UserAvatarComponentProps {
  userId: string;
  size?: AvatarSize | undefined;
}
const UserAvatarComponent: React.FC<UserAvatarComponentProps> = (props) => {
  let [usersName, setUsersName] = useState<string>();
  let [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    if (usersName === undefined) {
      const getTripObjectiveName = async () => {
        const tripObjectiveNameResponse = await SettingsService.GetUsersName(
          props.userId
        );
        setUsersName(tripObjectiveNameResponse);
        setIsLoading(false);
      };
      getTripObjectiveName();
    }
  });

  return (
    <Spin spinning={isLoading}>
      <Tooltip title={usersName} placement="top">
        <Avatar
          src="https://avatars.githubusercontent.com/u/4175685?v=4"
          size={props.size}
        >
          {StringHelpers.NameToInitials(usersName)}
        </Avatar>
      </Tooltip>
    </Spin>
  );
};

export { UserAvatarComponent };
