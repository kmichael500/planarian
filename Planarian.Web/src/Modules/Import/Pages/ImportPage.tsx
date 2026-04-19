import { useContext, useEffect } from "react";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { ImportComponent } from "../Components/ImportComponent";

const ImportPage = () => {
  const { setHeaderTitle, setHeaderButtons, setContentStyle, defaultContentStyle } =
    useContext(AppContext);

  useEffect(() => {
    setHeaderButtons([]);
    setHeaderTitle(["Import"]);
    setContentStyle({
      ...defaultContentStyle,
      height: "calc((var(--vh, 1vh) * 100) - 102px)",
      overflow: "hidden",
      display: "flex",
      flexDirection: "column",
    });

    return () => {
      setContentStyle(defaultContentStyle);
    };
  }, [defaultContentStyle, setContentStyle, setHeaderButtons, setHeaderTitle]);
  return (
    <>
      <ImportComponent />
    </>
  );
};

export { ImportPage };
