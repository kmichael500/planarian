import { useContext, useEffect, useMemo, useState } from "react";
import { Pagination, Select, Space, Spin, Tabs, Typography, message } from "antd";
import { useSearchParams } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import { CaveChangeRequestList } from "../Components/CaveChangeRequestList";
import { CaveChangeRequestStatus, CaveChangeRequestSummaryVm } from "../Models/CaveChangeRequestVm";
import { CaveService } from "../Service/CaveService";

const pageSize = 10;
const statusOptions: { label: string; value: "All" | CaveChangeRequestStatus }[] = [
  { label: "All", value: "All" },
  { label: "Pending", value: "Pending" },
  { label: "Approved", value: "Approved" },
  { label: "Rejected", value: "Rejected" },
];

const isStatus = (value: string | null): value is CaveChangeRequestStatus =>
  value === "Pending" || value === "Approved" || value === "Rejected";

export const CaveChangeRequestsPage = () => {
  const { setHeaderTitle, setHeaderButtons, hasPermission } = useContext(AppContext);
  const [searchParams, setSearchParams] = useSearchParams();
  const [requests, setRequests] = useState<CaveChangeRequestSummaryVm[]>([]);
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);

  const canReview = hasPermission(PermissionKey.Manager);
  const activeTab = canReview && searchParams.get("tab") === "review" ? "review" : "mine";
  const statusParam = searchParams.get("status");
  const status = activeTab === "mine" && isStatus(statusParam) ? statusParam : undefined;

  const tabs = useMemo(() => [
    { key: "mine", label: "My submissions" },
    ...(canReview ? [{ key: "review", label: "Review queue" }] : []),
  ], [canReview]);

  useEffect(() => {
    setHeaderTitle(["Cave Changes"]);
    setHeaderButtons([]);
  }, [setHeaderButtons, setHeaderTitle]);

  useEffect(() => setPage(1), [activeTab, status]);

  useEffect(() => {
    let active = true;
    setLoading(true);
    const load = activeTab === "review"
      ? CaveService.GetReviewQueue(page, pageSize)
      : CaveService.GetMyChangeRequests(page, pageSize, status);
    load.then(result => {
      if (!active) return;
      setRequests(result.results);
      setTotal(result.totalCount);
      setPage(result.pageNumber);
    }).catch(() => {
      if (active) message.error(activeTab === "review" ? "Review queue could not be loaded." : "Submissions could not be loaded.");
    }).finally(() => {
      if (active) setLoading(false);
    });
    return () => { active = false; };
  }, [activeTab, page, status]);

  const changeTab = (key: string) => {
    const next = new URLSearchParams(searchParams);
    if (key === "review") {
      next.set("tab", "review");
      next.delete("status");
    } else {
      next.delete("tab");
    }
    setSearchParams(next, { replace: true });
  };

  const changeStatus = (value: "All" | CaveChangeRequestStatus) => {
    const next = new URLSearchParams(searchParams);
    if (value === "All") next.delete("status");
    else next.set("status", value);
    setSearchParams(next, { replace: true });
  };

  return (
    <Space direction="vertical" size={16} style={{ width: "100%" }}>
      <Tabs activeKey={activeTab} items={tabs} onChange={changeTab} />
      <div style={{ display: "flex", gap: 12, justifyContent: "space-between", alignItems: "center", flexWrap: "wrap" }}>
        {activeTab === "mine" ? (
          <Select
            aria-label="Submission status"
            value={status ?? "All"}
            options={statusOptions}
            onChange={changeStatus}
            style={{ minWidth: 160 }}
          />
        ) : <span />}
        <Typography.Text type="secondary">
          {total} {activeTab === "review" ? (total === 1 ? "item" : "items") : (total === 1 ? "submission" : "submissions")}
        </Typography.Text>
      </div>
      <Spin spinning={loading}>
        <CaveChangeRequestList requests={requests} review={activeTab === "review"} />
      </Spin>
      {total > pageSize && (
        <Pagination
          current={page}
          pageSize={pageSize}
          total={total}
          showSizeChanger={false}
          onChange={setPage}
          style={{ display: "flex", justifyContent: "flex-end" }}
        />
      )}
    </Space>
  );
};
