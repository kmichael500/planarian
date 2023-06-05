import { useContext, useEffect } from "react";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { AddCaveComponent } from "../Components/AddCaveComponent";
import { BackButtonComponent } from "../../../Shared/Components/Buttons/BackButtonComponent";

const AddCavesPage: React.FC = () => {
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);

  useEffect(() => {
    setHeaderButtons([<BackButtonComponent to={"./.."} />]);
    setHeaderTitle(["Report a Cave"]);
  }, []);

  return (
    <>
      <AddCaveComponent />
    </>
  );
};

export { AddCavesPage };
