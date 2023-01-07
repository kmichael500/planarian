import { Avatar, Typography } from "antd";
import { AvatarSize } from "antd/lib/avatar/SizeContext";
import { UserAvatarComponent } from "./UserAvatarComponent";

const { Text } = Typography;
export interface UserAvatarComponentProps {
  userIds: string[];
  size?: AvatarSize | undefined;
  maxCount?: number;
}
const UserAvatarGroupComponent: React.FC<UserAvatarComponentProps> = (
  props
) => {
  return (
    <>
      {" "}
      <Avatar.Group maxCount={props.maxCount}>
        {props.userIds.map((userId, index) => (
          <UserAvatarComponent size={props.size} key={index} userId={userId} />
        ))}
      </Avatar.Group>
    </>
  );
};

export { UserAvatarGroupComponent };
