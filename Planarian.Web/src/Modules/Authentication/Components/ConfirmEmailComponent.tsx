import { message, Spin } from "antd";
import { useEffect, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { UserService } from "../../User/UserService";

function ConfirmEmailComponent() {
  const [isVerifing, setIsVerifing] = useState(true);
  const navigate = useNavigate();
  const location = useLocation();
  const code = new URLSearchParams(location.search).get("code");

  useEffect(() => {
    const confirmEmail = async () => {
      try {
        if (code == null) {
          throw new Error("Invalid code");
          return;
        }
        const response = await UserService.ConfirmEmail(code);
        message.success("Your email has been verified!");
      } catch (error: any) {
        message.error(error.message);
      } finally {
        setIsVerifing(false);
        navigate("/login");
      }
    };

    confirmEmail();
  }, []);

  return <Spin spinning={isVerifing}></Spin>;
}

export { ConfirmEmailComponent };
