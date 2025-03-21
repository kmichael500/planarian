import { Result, Button } from "antd";
import { PlanarianButton } from "../Components/Buttons/PlanarianButtton";
import { BackButtonComponent } from "../Components/Buttons/BackButtonComponent";
import { useContext, useEffect } from "react";
import { AppContext } from "../../Configuration/Context/AppContext";

const NotFoundPage = () => {
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);

  useEffect(() => {
    setHeaderButtons([]);
    setHeaderTitle(["Not Found"]);
  }, []);

  return (
    <Result
      status="error"
      title="Not Found"
      subTitle="Sorry, the page you are trying to visit does not exist."
      extra={<BackButtonComponent to={"/caves"}>Back Home</BackButtonComponent>}
    />
  );
};

export { NotFoundPage };
