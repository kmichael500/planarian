import { useContext, useEffect, useMemo, useState } from "react";
import { Card, Pagination, Segmented, Select, Space, Spin, Typography, message, theme } from "antd";
import { useSearchParams } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import { CaveChangeRequestList } from "../Components/CaveChangeRequestList";
import { CaveChangeRequestSortBy, CaveChangeRequestStatus, CaveChangeRequestSummaryVm } from "../Models/CaveChangeRequestVm";
import { SplitSortControl } from "../../Search/Components/SplitSortControl";
import { CaveService } from "../Service/CaveService";

const pageSize = 10;
const reviewStatusOptions: { label: string; value: "All" | CaveChangeRequestStatus }[] = [
  { label: "All", value: "All" },
  { label: "Pending review", value: "Pending" },
  { label: "Approved", value: "Approved" },
  { label: "Rejected", value: "Rejected" },
];
const myStatusOptions: { label: string; value: "All" | CaveChangeRequestStatus }[] = [
  { label: "All", value: "All" },
  { label: "Pending review", value: "Pending" },
  { label: "Changes requested", value: "ChangesRequested" },
  { label: "Approved", value: "Approved" },
  { label: "Rejected", value: "Rejected" },
];

const isStatus = (value: string | null): value is CaveChangeRequestStatus =>
  value === "Pending" || value === "ChangesRequested" || value === "Approved" || value === "Rejected";

const sortOptions = [
  { display: "Updated", value: "UpdatedOn" },
  { display: "Submitted", value: "SubmittedOn" },
  { display: "Cave name", value: "CaveName" },
];

const isSortBy = (value: string | null): value is CaveChangeRequestSortBy =>
  value === "UpdatedOn" || value === "SubmittedOn" || value === "CaveName";

export const CaveChangeRequestsPage = () => {
  const { setHeaderTitle, setHeaderButtons, hasPermission } = useContext(AppContext);
  const [searchParams, setSearchParams] = useSearchParams();
  const [requests, setRequests] = useState<CaveChangeRequestSummaryVm[]>([]);
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const { token } = theme.useToken();

  const canReview = hasPermission(PermissionKey.Manager);
  const activeTab = canReview && searchParams.get("tab") !== "mine" ? "review" : "mine";
  const statusParam = searchParams.get("status");
  const parsedStatus = isStatus(statusParam) ? statusParam : undefined;
  const status = activeTab === "review" && parsedStatus === "ChangesRequested" ? undefined : parsedStatus;
  const statusOptions = activeTab === "review" ? reviewStatusOptions : myStatusOptions;
  const defaultSortBy: CaveChangeRequestSortBy = activeTab === "review" ? "SubmittedOn" : "UpdatedOn";
  const defaultSortDescending = activeTab !== "review";
  const sortParam = searchParams.get("sortBy");
  const sortBy = isSortBy(sortParam) ? sortParam : defaultSortBy;
  const sortDirectionParam = searchParams.get("sortDescending");
  const sortDescending = sortDirectionParam === "true" ? true
    : sortDirectionParam === "false" ? false : defaultSortDescending;

  const workspaceOptions = useMemo(() => canReview ? [
    { value: "review", label: "Review queue" },
    { value: "mine", label: "My submissions" },
  ] : [], [canReview]);

  useEffect(() => {
    setHeaderTitle([canReview ? "Cave Changes" : "Submissions"]);
    setHeaderButtons([]);
  }, [canReview, setHeaderButtons, setHeaderTitle]);

  useEffect(() => setPage(1), [activeTab, status, sortBy, sortDescending]);

  useEffect(() => {
    let active = true;
    setLoading(true);
    const load = activeTab === "review"
      ? CaveService.GetReviewQueue(page, pageSize, status, sortBy, sortDescending)
      : CaveService.GetMyChangeRequests(page, pageSize, status, sortBy, sortDescending);
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
  }, [activeTab, page, status, sortBy, sortDescending]);

  const changeTab = (key: string) => {
    const next = new URLSearchParams(searchParams);
    if (key === "review") {
      next.delete("tab");
      if (next.get("status") === "ChangesRequested") next.delete("status");
    } else next.set("tab", "mine");
    next.delete("sortBy");
    next.delete("sortDescending");
    setSearchParams(next, { replace: true });
  };

  const changeStatus = (value: "All" | CaveChangeRequestStatus) => {
    const next = new URLSearchParams(searchParams);
    if (value === "All") next.delete("status");
    else next.set("status", value);
    setSearchParams(next, { replace: true });
  };

  const changeSort = (value: string) => {
    if (!isSortBy(value)) return;
    const next = new URLSearchParams(searchParams);
    next.set("sortBy", value);
    next.set("sortDescending", String(sortDescending));
    setSearchParams(next, { replace: true });
  };

  const toggleSortDirection = () => {
    const next = new URLSearchParams(searchParams);
    next.set("sortBy", sortBy);
    next.set("sortDescending", String(!sortDescending));
    setSearchParams(next, { replace: true });
  };

  return (
    <Card variant="outlined" styles={{ body: { padding: 0 } }}>
      <div style={{ display: "flex", gap: token.marginSM, justifyContent: "space-between", alignItems: "center",
        flexWrap: "wrap", padding: `${token.paddingLG}px ${token.paddingLG}px ${token.paddingSM}px` }}>
        <Space wrap size="small">
          {canReview && <Segmented aria-label="Cave changes view" value={activeTab} options={workspaceOptions}
            onChange={value => changeTab(String(value))} />}
          <Select aria-label="Submission status" value={status ?? "All"} options={statusOptions}
            onChange={changeStatus} style={{ minWidth: 180 }} />
        </Space>
        <Space size="small" wrap>
          <Typography.Text type="secondary">
            {total} {total === 1 ? "submission" : "submissions"}
          </Typography.Text>
          <SplitSortControl compact sortOptions={sortOptions} selectedValue={sortBy}
            isDescending={sortDescending} onSelect={changeSort} onToggleDirection={toggleSortDirection} />
        </Space>
      </div>
      <div style={{ padding: `0 ${token.paddingLG}px ${token.paddingLG}px` }}>
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
            style={{ display: "flex", justifyContent: "flex-end", marginTop: token.marginMD }}
          />
        )}
      </div>
    </Card>
  );
};
