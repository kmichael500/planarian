import { useContext, useEffect } from "react";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { ImportComponent } from "../Components/ImportComponent";

const ImportPage = () => {
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);

  useEffect(() => {
    setHeaderButtons([]);
    setHeaderTitle(["Import"]);
  }, []);
  return (
    <>
      <ImportComponent />
    </>
  );
};

export { ImportPage };
