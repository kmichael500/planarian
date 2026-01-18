import { useContext, useEffect, useState } from "react";
import { CaveVm } from "../Models/CaveVm";
import { Link, useParams } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { BackButtonComponent } from "../../../Shared/Components/Buttons/BackButtonComponent";
import { NotFoundError } from "../../../Shared/Exceptions/PlanarianErrors";
import { CaveService } from "../Service/CaveService";
import { CaveComponent } from "../Components/CaveComponent";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { EditOutlined } from "@ant-design/icons";
import { isNullOrWhiteSpace } from "../../../Shared/Helpers/StringHelpers";
import { Grid, Typography } from "antd";
import FavoriteCave from "../Components/FavoriteCave";

const CavePage = () => {
  const [cave, setCave] = useState<CaveVm>();

  const [isLoading, setIsLoading] = useState<boolean>(true);

  const {
    setHeaderTitle,
    setHeaderButtons,
    defaultContentStyle,
    setContentStyle,
  } = useContext(AppContext);
  const { caveId } = useParams();

  if (isNullOrWhiteSpace(caveId)) {
    throw new NotFoundError("caveid");
  }

  const screens = Grid.useBreakpoint();
  const isLargeScreenSize = Object.entries(screens).some(
    ([key, value]) => value && (key === "lg" || key === "xl")
  );

  useEffect(() => {
    setHeaderButtons([
      <FavoriteCave caveId={caveId} />,

      <Link to={`/caves/${caveId}/edit`}>
        <PlanarianButton
          icon={<EditOutlined />}
        >
          Edit
        </PlanarianButton>
      </Link>,
      <BackButtonComponent to={"./.."} />,
    ]);
  }, [cave]);

  if (caveId === undefined) {
    throw new NotFoundError("caveid");
  }

  useEffect(() => {
    if (!isNullOrWhiteSpace(cave?.name)) {
      setHeaderTitle([
        <>
          {isLargeScreenSize && (
            <Typography.Title level={4} ellipsis>
              {`${cave?.displayId} ${cave?.name ?? ""}`}
            </Typography.Title>
          )}
          {!isLargeScreenSize && (
            <Typography.Text ellipsis>
              {`${cave?.displayId} ${cave?.name ?? ""}`}
            </Typography.Text>
          )}
        </>,
      ]);
    }
  }, [cave]);
  useEffect(() => {
    const getCave = async () => {
      const caveResponse = await CaveService.GetCave(caveId);
      setCave(caveResponse);
      setIsLoading(false);
    };
    getCave();

    setContentStyle({});

    return () => {
      setContentStyle(defaultContentStyle);
    };
  }, []);

  return (
    <>
      <CaveComponent
        cave={cave}
        isLoading={isLoading}
        options={{
          showMap: true,
        }}
        updateCave={async () => {
          setIsLoading(true);
          const updatedCave = await CaveService.GetCave(caveId);
          setCave(updatedCave);
          setIsLoading(false);
        }}
      />
    </>
  );
};

export { CavePage };
