import { message, Spin } from "antd";
import { useContext, useEffect, useRef, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { UserService } from "../../User/UserService";

function ConfirmEmailPage() {
  const [isVerifing, setIsVerifing] = useState(true);
  const navigate = useNavigate();
  const location = useLocation();
  const { refreshSession } = useContext(AppContext);
  const code = new URLSearchParams(location.search).get("code");
  // Confirmation is intentionally automatic on page load. React StrictMode re-runs mount
  // effects in development, so remember the attempted code for this mounted page to avoid
  // submitting the same one-time confirmation code twice and showing contradictory feedback.
  const attemptedCodeRef = useRef<string | null | undefined>(undefined);

  useEffect(() => {
    if (attemptedCodeRef.current === code) return;
    attemptedCodeRef.current = code;

    const confirmEmail = async () => {
      try {
        if (code == null) {
          throw new Error("Invalid code");
        }
        await UserService.ConfirmEmail(code);
        message.success("Your email has been verified!");
      } catch (e) {
        const error = e as ApiErrorResponse;
        message.error(error.message);
        setIsVerifing(false);
        navigate("/login", { replace: true });
        return;
      }

      try {
        await refreshSession();
        setIsVerifing(false);
        navigate("/", { replace: true });
      } catch {
        setIsVerifing(false);
        navigate("/login", { replace: true });
      }
    };

    void confirmEmail();
  }, [code, navigate, refreshSession]);

  return <Spin spinning={isVerifing}></Spin>;
}

export { ConfirmEmailPage };
