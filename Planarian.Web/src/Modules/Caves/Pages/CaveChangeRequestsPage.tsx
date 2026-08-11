import { useContext, useEffect, useState } from "react";
import { Spin, message } from "antd";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { CaveChangeRequestList } from "../Components/CaveChangeRequestList";
import { CaveChangeRequestSummaryVm } from "../Models/CaveChangeRequestVm";
import { CaveService } from "../Service/CaveService";

export const CaveChangeRequestsPage = ({ review = false }: { review?: boolean }) => {
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);
  const [requests, setRequests] = useState<CaveChangeRequestSummaryVm[]>([]);
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    setHeaderTitle([review ? "Pending Cave Reviews" : "My Cave Requests"]);
    setHeaderButtons([]);
    (review ? CaveService.GetReviewQueue() : CaveService.GetMyChangeRequests())
      .then(setRequests).catch(() => message.error("Requests could not be loaded."))
      .finally(() => setLoading(false));
  }, [review]);
  return <Spin spinning={loading}><CaveChangeRequestList requests={requests} review={review} /></Spin>;
};
