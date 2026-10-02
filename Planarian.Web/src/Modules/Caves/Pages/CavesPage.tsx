import { useContext, useEffect } from "react";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { CavesComponent } from "../Components/CavesComponent";
import { CaveCreateButtonComponent } from "../Components/CaveCreateButtonComponent";
import { ShouldDisplay } from "../../../Shared/Permissioning/Components/ShouldDisplay";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import "./CavesPage.scss";

const CavesPage: React.FC = () => {
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);

  useEffect(() => {
    setHeaderButtons([
      <ShouldDisplay permissionKey={PermissionKey.Manager}>
        <CaveCreateButtonComponent />
      </ShouldDisplay>,
    ]);
    setHeaderTitle([`Caves`]);
  }, [setHeaderButtons, setHeaderTitle]);

  return (
    <div className="caves-page">
      <CavesComponent />
    </div>
  );
};

export { CavesPage };
