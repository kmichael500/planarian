import { useContext, useEffect, useState } from "react";
import { CaveVm } from "../Models/CaveVm";
import { Link, useParams } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { BackButtonComponent } from "../../../Shared/Components/Buttons/BackButtonComponent";
import { NotFoundError } from "../../../Shared/Exceptions/PlanarianErrors";
import { CaveService } from "../Service/CaveService";
import { CaveComponent } from "../Components/CaveComponent";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { EditOutlined, FormOutlined } from "@ant-design/icons";
import { isNullOrWhiteSpace } from "../../../Shared/Helpers/StringHelpers";
import { Grid, Typography } from "antd";
import { AppService } from "../../../Shared/Services/AppService";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import FavoriteCave from "../Components/FavoriteCave";
import { CaveHistoryModal } from "../Components/CaveHistoryModal";

interface CavePageContentProps {
  caveId: string;
}

const CavePageContent = ({ caveId }: CavePageContentProps) => {
  const [cave, setCave] = useState<CaveVm>();
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [hasEditPermission, setHasEditPermission] = useState<boolean>(false);
  const [isPermissionResolved, setIsPermissionResolved] = useState(false);
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);

  const screens = Grid.useBreakpoint();
  const isLargeScreenSize = Object.entries(screens).some(
    ([key, value]) => value && (key === "lg" || key === "xl")
  );

  useEffect(() => {
    setHeaderButtons([
      <FavoriteCave caveId={caveId} />,
      <CaveHistoryModal
        caveId={caveId}
        canReviewChangeRequests={hasEditPermission}
      />,
      ...(isPermissionResolved && hasEditPermission
        ? [
            <Link to={`/caves/${caveId}/edit`}>
              <PlanarianButton icon={<EditOutlined />} type="primary">
                Edit
              </PlanarianButton>
            </Link>,
          ]
        : isPermissionResolved
        ? [
            <Link to={`/caves/${caveId}/suggest`}>
              <PlanarianButton icon={<FormOutlined />} type="primary">
                Suggest Changes
              </PlanarianButton>
            </Link>,
          ]
        : []),
      <BackButtonComponent
        to={"./.."}
        type="text"
        tooltip="Back to Caves"
      />,
    ]);
  }, [
    caveId,
    hasEditPermission,
    isPermissionResolved,
    setHeaderButtons,
  ]);

  useEffect(() => {
    if (!cave || isNullOrWhiteSpace(cave.name)) return;

    setHeaderTitle([
      <>
        {isLargeScreenSize ? (
          <Typography.Title level={4} ellipsis>
            {`${cave.displayId} ${cave.name}`}
          </Typography.Title>
        ) : (
          <Typography.Text ellipsis>
            {`${cave.displayId} ${cave.name}`}
          </Typography.Text>
        )}
      </>,
    ]);
  }, [cave, isLargeScreenSize, setHeaderTitle]);

  useEffect(() => {
    let isCurrent = true;

    const getCave = async () => {
      const [caveResponse, editPermission] = await Promise.all([
        CaveService.GetCave(caveId),
        AppService.HasCavePermission(PermissionKey.Manager, caveId),
      ]);

      if (!isCurrent) return;

      setCave(caveResponse);
      setHasEditPermission(editPermission);
      setIsPermissionResolved(true);
      setIsLoading(false);
    };

    void getCave();

    return () => {
      isCurrent = false;
    };
  }, [caveId]);

  return (
    <CaveComponent
      cave={cave}
      isLoading={isLoading}
      hasEditPermission={hasEditPermission}
      options={{ showMap: true }}
      updateCave={async () => {
        setIsLoading(true);
        const updatedCave = await CaveService.GetCave(caveId);
        setCave(updatedCave);
        setIsLoading(false);
      }}
    />
  );
};

const CavePage = () => {
  const { caveId } = useParams();

  if (isNullOrWhiteSpace(caveId)) {
    throw new NotFoundError("caveid");
  }

  return <CavePageContent key={caveId} caveId={caveId} />;
};

export { CavePage };
