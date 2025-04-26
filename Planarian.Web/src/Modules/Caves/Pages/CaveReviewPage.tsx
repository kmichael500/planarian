import { useContext, useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { BackButtonComponent } from "../../../Shared/Components/Buttons/BackButtonComponent";
import { NotFoundError } from "../../../Shared/Exceptions/PlanarianErrors";
import { CaveService } from "../Service/CaveService";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import {
  EditOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
} from "@ant-design/icons";
import { isNullOrWhiteSpace } from "../../../Shared/Helpers/StringHelpers";
import { Grid, Typography } from "antd";
import { ProposedChangeRequestVm } from "../Models/ProposedChangeRequestVm";
import { CaveReviewComponent } from "../Components/CaveReviewComponent";
import { ReviewChangeRequest } from "../Models/ReviewChangeRequest";
import { CaveVm } from "../Models/CaveVm";

const CaveReviewPage = () => {
  const [proposedChangeRequest, setReviewChangeRequest] =
    useState<ProposedChangeRequestVm>();

  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [isApproving, setIsApproving] = useState<boolean>(false);
  const [isDenying, setIsDenying] = useState<boolean>(false);

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

  const handleApprove = async () => {
    if (!caveChangeRequestId) return;

    try {
      setIsApproving(true);

      const request: ReviewChangeRequest = {
        id: caveChangeRequestId,
        approve: true,
        cave: proposedChangeRequest!.cave,
        notes: null,
      };

      await CaveService.ReviewChange(request);
      // Redirect back or show success message
      window.history.back();
    } catch (error) {
      console.error("Error approving change request:", error);
      // Could add error notification here
    } finally {
      setIsApproving(false);
    }
  };

  const handleDeny = async () => {
    if (!caveChangeRequestId) return;

    try {
      setIsDenying(true);
      // await CaveService.DenyChangeRequest(caveChangeRequestId);
      // Redirect back or show success message
      window.history.back();
    } catch (error) {
      console.error("Error denying change request:", error);
      // Could add error notification here
    } finally {
      setIsDenying(false);
    }
  };

  useEffect(() => {
    setHeaderButtons([
      <Link to={`/caves/review/${caveChangeRequestId}/edit`}>
        <PlanarianButton icon={<EditOutlined />}>Edit</PlanarianButton>
      </Link>,
      <PlanarianButton
        type="primary"
        icon={<CheckCircleOutlined />}
        onClick={handleApprove}
        loading={isApproving}
      >
        Approve
      </PlanarianButton>,
      <PlanarianButton
        icon={<CloseCircleOutlined />}
        onClick={handleDeny}
        loading={isDenying}
      >
        Deny
      </PlanarianButton>,
      <BackButtonComponent to={"./.."} />,
    ]);
  }, [proposedChangeRequest, isApproving, isDenying, caveChangeRequestId]);

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

      if (!response.cave) {
        throw new NotFoundError("cave");
      }
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
      originalCave={proposedChangeRequest?.originalCave}
      isLoading={isLoading}
      changes={proposedChangeRequest?.changes}
    ></CaveReviewComponent>
  );
};

export { CaveReviewPage };
