import { useContext, useEffect } from "react";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { CavesComponent } from "../Components/CavesComponent";
import { CaveCreateButtonComponent } from "../Components/CaveCreateButtonComponent";

const CavesPage: React.FC = () => {
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);
  useEffect(() => {
    setHeaderButtons([<CaveCreateButtonComponent />]);
    setHeaderTitle([`Caves`]);
  }, []);

  return (
    <>
      <CavesComponent />
    </>
  );
};

export { CavesPage };
