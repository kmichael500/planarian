import { useContext, useEffect } from "react";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { CaveReviewsComponent } from "../Components/CaveReviewComponent";

const CaveReviewsPage: React.FC = () => {
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);

  useEffect(() => {
    setHeaderButtons([]);
    setHeaderTitle([`Pending Reviews`]);
  }, []);

  return (
    <>
      <CaveReviewsComponent />
    </>
  );
};

export { CaveReviewsPage };
