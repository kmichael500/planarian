import { Avatar, Spin, Tooltip, Typography } from "antd";
import { useEffect, useState } from "react";
import { StringHelpers } from "../../../Shared/Helpers/StringHelpers";
import { NameProfilePhotoVm } from "../Models/NameProfilePhotoVm";
import { AvatarSize } from "antd/lib/avatar/AvatarContext";
import { UserService } from "../UserService";

const { Text } = Typography;

export interface UserAvatarComponentProps {
  userId: string;
  size?: AvatarSize | undefined;
}

const UserAvatarComponent: React.FC<UserAvatarComponentProps> = (props) => {
  let [usersName, setUsersName] = useState<NameProfilePhotoVm>();
  let [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    let isCancelled = false;

    const getUsersName = async () => {
      setIsLoading(true);

      try {
        const tripNameResponse = await UserService.GetUsersName(props.userId);
        if (isCancelled) {
          return;
        }

        setUsersName(tripNameResponse);
      } finally {
        if (!isCancelled) {
          setIsLoading(false);
        }
      }
    };

    setUsersName(undefined);
    getUsersName();

    return () => {
      isCancelled = true;
    };
  }, [props.userId]);

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
