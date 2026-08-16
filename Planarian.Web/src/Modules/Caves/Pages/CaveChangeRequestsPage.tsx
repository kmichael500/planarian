import { useContext, useEffect, useState } from "react";
import { Pagination, Spin, message } from "antd";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { CaveChangeRequestList } from "../Components/CaveChangeRequestList";
import { CaveChangeRequestSummaryVm } from "../Models/CaveChangeRequestVm";
import { CaveService } from "../Service/CaveService";

export const CaveChangeRequestsPage = ({ review = false }: { review?: boolean }) => {
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);
  const [requests, setRequests] = useState<CaveChangeRequestSummaryVm[]>([]);
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const pageSize = 10;
  const [loading, setLoading] = useState(true);
  useEffect(() => setPage(1), [review]);
  useEffect(() => {
    let active = true;
    setLoading(true);
    setHeaderTitle([review ? "Pending Cave Reviews" : "My Cave Requests"]);
    setHeaderButtons([]);
    (review ? CaveService.GetReviewQueue(page, pageSize) : CaveService.GetMyChangeRequests(page, pageSize))
      .then(result => {
        if (!active) return;
        setRequests(result.results);
        setTotal(result.totalCount);
        setPage(result.pageNumber);
      })
      .catch(() => { if (active) message.error("Requests could not be loaded."); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [review, page]);
  return <Spin spinning={loading}>
    <CaveChangeRequestList requests={requests} review={review} />
    {total > pageSize && <Pagination current={page} pageSize={pageSize} total={total}
      showSizeChanger={false} onChange={setPage} style={{ marginTop: 16 }} />}
  </Spin>;
};
