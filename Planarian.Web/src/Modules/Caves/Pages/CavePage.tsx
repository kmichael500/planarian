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

const CavePage = () => {
  const [cave, setCave] = useState<CaveVm>();

  const [isLoading, setIsLoading] = useState<boolean>(true);

  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);
  const { caveId } = useParams();

  const screens = Grid.useBreakpoint();
  const isLargeScreenSize = Object.entries(screens).some(
    ([key, value]) => value && (key === "lg" || key === "xl")
  );

  useEffect(() => {
    setHeaderButtons([
      <Link to={`/caves/${caveId}/edit`}>
        <PlanarianButton icon={<EditOutlined />}>Edit</PlanarianButton>{" "}
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
  }, []);

  return (
    <>
      <CaveComponent
        cave={cave}
        isLoading={isLoading}
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
