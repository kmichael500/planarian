import { Result } from "antd";
import { useContext, useEffect } from "react";
import { AppContext } from "../../Configuration/Context/AppContext";
import { BackButtonComponent } from "../Components/Buttons/BackButtonComponent";

const UnauthorizedPage = () => {
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);

  useEffect(() => {
    setHeaderButtons([]);
    setHeaderTitle(["Unauthorizedd"]);
  }, []);

  return (
    <Result
      status="403"
      title="403"
      subTitle="Sorry, you are not authorized to view this page."
      extra={<BackButtonComponent to={"/caves"}>Back Home</BackButtonComponent>}
    />
  );
};

export { UnauthorizedPage };
