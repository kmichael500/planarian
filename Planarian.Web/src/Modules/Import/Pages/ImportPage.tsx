import { useContext, useEffect } from "react";
import { AppContext } from "../../../Configuration/Context/AppContext";

const ImportPage = () => {
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);

  useEffect(() => {
    setHeaderButtons([]);
    setHeaderTitle(["Import"]);
  }, []);
  return (
    <div>
    </div>
  );
};

export { ImportPage };
