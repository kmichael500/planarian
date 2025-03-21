import { Result } from "antd";
import { useContext, useEffect } from "react";
import { AppContext } from "../../Configuration/Context/AppContext";
import { BackButtonComponent } from "../Components/Buttons/BackButtonComponent";

const UnauthorizedPage = () => {
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);

  useEffect(() => {
    setHeaderButtons([]);
    setHeaderTitle(["Unauthorized"]);
  }, []);

  return (
    <Result
      title="Unauthorized"
      subTitle="Sorry, you are not authorized to view this page."
      icon={
        <img
          src="https://saplanarian.blob.core.windows.net/public/unauthorized.png"
          alt="Unauthorized"
          style={{
            height: "350px",
            width: "350px",
            borderRadius: "10%",
            border: "5px solid white",
            boxShadow: "0 0 10px rgba(0, 0, 0, 0.1)",
            objectFit: "cover",
          }}
        />
      }
      extra={<BackButtonComponent to={"/caves"}>Back Home</BackButtonComponent>}
    />
  );
};

export { UnauthorizedPage };
