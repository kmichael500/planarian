import { useContext, useEffect, useState } from "react";
import { CaveVm } from "../Models/CaveVm";
import { Link, useParams } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { BackButtonComponent } from "../../../Shared/Components/Buttons/BackButtonComponent";
import { NotFoundError } from "../../../Shared/Exceptions/PlanarianErrors";
import { CaveService } from "../Service/CaveService";
import { CaveComponent } from "../Components/CaveComponent";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import {
  EditOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
} from "@ant-design/icons";
import { isNullOrWhiteSpace } from "../../../Shared/Helpers/StringHelpers";
import { Grid, Typography } from "antd";
import { AppService } from "../../../Shared/Services/AppService";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import FavoriteCave from "../Components/FavoriteCave";
import { ProposedChangeRequestVm } from "../Models/ProposedChangeRequestVm";
import { CaveReviewComponent } from "../Models/CaveReviewComponent";

const CaveReviewPage = () => {
  const [proposedChangeRequest, setReviewChangeRequest] =
    useState<ProposedChangeRequestVm>();

  const [isLoading, setIsLoading] = useState<boolean>(true);

  const {
    setHeaderTitle,
    setHeaderButtons,
    defaultContentStyle,
    setContentStyle,
  } = useContext(AppContext);
  const { caveChangeRequestId } = useParams();

  if (isNullOrWhiteSpace(caveChangeRequestId)) {
    throw new NotFoundError("caveChangeRequestId");
  }

  const screens = Grid.useBreakpoint();
  const isLargeScreenSize = Object.entries(screens).some(
    ([key, value]) => value && (key === "lg" || key === "xl")
  );

  useEffect(() => {
    setHeaderButtons([
      <Link to={`/caves/review/${caveChangeRequestId}/edit`}>
        <PlanarianButton icon={<EditOutlined />}>Edit</PlanarianButton>
      </Link>,
      <PlanarianButton type="primary" icon={<CheckCircleOutlined />}>
        Approve
      </PlanarianButton>,
      <PlanarianButton icon={<CloseCircleOutlined />}>Deny</PlanarianButton>,
      <BackButtonComponent to={"./.."} />,
    ]);
  }, [proposedChangeRequest]);

  if (caveChangeRequestId === undefined) {
    throw new NotFoundError("caveid");
  }

  useEffect(() => {
    if (!isNullOrWhiteSpace(proposedChangeRequest?.cave.name)) {
      setHeaderTitle([
        <>
          {isLargeScreenSize && (
            <Typography.Title level={4} ellipsis>
              {`${proposedChangeRequest?.cave.name ?? ""}`}
            </Typography.Title>
          )}
          {!isLargeScreenSize && (
            <Typography.Text ellipsis>
              {`${proposedChangeRequest?.cave.name ?? ""}`}
            </Typography.Text>
          )}
        </>,
      ]);
    }
  }, [proposedChangeRequest]);
  useEffect(() => {
    const getCave = async () => {
      const response = await CaveService.GetProposedChange(caveChangeRequestId);
      setReviewChangeRequest(response);

      setIsLoading(false);
    };
    getCave();

    setContentStyle({});

    return () => {
      setContentStyle(defaultContentStyle);
    };
  }, []);

  return (
    <CaveReviewComponent
      cave={proposedChangeRequest?.cave}
      isLoading={isLoading}
    ></CaveReviewComponent>
  );
};

export { CaveReviewPage };
