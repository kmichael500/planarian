import { Typography, Form, Space } from "antd";
import { useState, useEffect } from "react";
import { Link } from "react-router-dom";
import { CardGridComponent } from "../../../Shared/Components/CardGrid/CardGridComponent";
import { SpinnerCardComponent } from "../../../Shared/Components/SpinnerCard/SpinnerCard";

import { CaveService } from "../Service/CaveService";
import { CaveSearchParamsVm } from "../Models/CaveSearchParamsVm";
import {
  formatDate,
  formatDateTime,
  formatNumber,
  isNullOrWhiteSpace,
} from "../../../Shared/Helpers/StringHelpers";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { EyeOutlined } from "@ant-design/icons";
import { GridCard } from "../../../Shared/Components/CardGrid/GridCard";
import { ChangesForReviewVm } from "../Models/ProposeChangeRequestVm";
import { UserAvatarComponent } from "../../User/Componenets/UserAvatarComponent";

const CaveReviewsComponent: React.FC = () => {
  let [proposedChanges, setProposedChanges] = useState<ChangesForReviewVm[]>();
  let [isLoading, setIsLoading] = useState(true);
  const [form] = Form.useForm<CaveSearchParamsVm>();

  useEffect(() => {
    getCaves();
  }, []);

  const getCaves = async () => {
    setIsLoading(true);
    const response = await CaveService.GetChangesToReview();
    setProposedChanges(response);
    setIsLoading(false);
  };

  return (
    <>
      <Space direction="vertical" style={{ width: "100%" }}>
        {proposedChanges && (
          <Typography.Text>
            {formatNumber(proposedChanges.length)} results found
          </Typography.Text>
        )}
        <SpinnerCardComponent spinning={isLoading}>
          <CardGridComponent
            noDataDescription={"No changes to review."}
            renderItem={(proposedChange) => (
              <GridCard
                style={{
                  height: "100%",
                  display: "flex",
                  flexDirection: "column",
                }}
                title={`${
                  !isNullOrWhiteSpace(proposedChange.caveDisplayId)
                    ? proposedChange.caveDisplayId + " "
                    : ""
                }${proposedChange.caveName}`}
                actions={[
                  <Link to={`/caves/review/${proposedChange.id}`} key="view">
                    <PlanarianButton
                      alwaysShowChildren
                      type="primary"
                      icon={<EyeOutlined />}
                    >
                      Review
                    </PlanarianButton>
                  </Link>,
                ]}
              >
                <Space direction="vertical">
                  <div>
                    <Typography.Text
                      style={{ marginRight: "8px", fontWeight: "bold" }}
                    >
                      Status:
                    </Typography.Text>
                    {proposedChange.isNew ? "New" : "Modified"}{" "}
                    <div style={{ display: "flex", alignItems: "center" }}>
                      <Typography.Text
                        style={{ marginRight: "8px", fontWeight: "bold" }}
                      >
                        Submitted By:
                      </Typography.Text>
                      <UserAvatarComponent
                        showName
                        userId={proposedChange.submittedByUserId}
                      />
                    </div>
                    <div>
                      <Typography.Text
                        style={{ marginRight: "8px", fontWeight: "bold" }}
                      >
                        Submitted On:
                      </Typography.Text>
                      {formatDateTime(proposedChange.submittedOn)}
                    </div>
                  </div>
                </Space>
              </GridCard>
            )}
            itemKey={(proposedChange) => proposedChange.id}
            items={proposedChanges}
          />
        </SpinnerCardComponent>
      </Space>
    </>
  );
};

export { CaveReviewsComponent };
