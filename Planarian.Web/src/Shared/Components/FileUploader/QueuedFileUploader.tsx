import {
  Button,
  Tooltip,
  Typography,
} from "antd";
import {
  ClearOutlined,
  DeleteOutlined,
  EyeOutlined,
  FileAddOutlined,
  RedoOutlined,
  SettingOutlined,
} from "@ant-design/icons";
import React, { useCallback, useRef } from "react";
import "./QueuedFileUploader.scss";
import {
  getUploadDisplayState,
  VIRTUAL_UPLOAD_QUEUE_ROW_HEIGHT,
} from "./fileUploaderHelpers";
import { QueuedFileUploadItem, QueuedFileUploaderProps } from "./types";
import { VirtualFileList } from "./VirtualFileList";
import {
  ResponsiveToolbar,
  ToolbarAction,
  ToolbarMetric,
} from "../Toolbar/ResponsiveToolbar";

interface QueueCardProps<TResult> {
  item: QueuedFileUploadItem<TResult>;
  onRemove: (itemId: string) => void;
  onRetry?: (itemId: string) => void;
}

interface RecentActivityCardProps<TResult> extends QueueCardProps<TResult> {
  renderRecentActivityTooltip?: (
    item: QueuedFileUploadItem<TResult>
  ) => React.ReactNode;
}

const getQueueItemProgressVariantClass = <TResult,>(
  item: QueuedFileUploadItem<TResult>,
  isAwaitingFinalize: boolean
) => {
  if (item.status === "uploading" || item.transportStatus === "finalizing" || isAwaitingFinalize) {
    return " import-files-dashboard__queue-progress--active";
  }

  return "";
};

const getQueueItemStatusLabel = <TResult,>(
  item: QueuedFileUploadItem<TResult>,
  isAwaitingFinalize: boolean
) => {
  if (item.transportStatus === "finalizing" || isAwaitingFinalize) {
    return "Finalizing...";
  }

  if (item.status === "uploading") {
    return "Uploading";
  }

  return null;
};

const QueueCardHeader = <TResult,>({
  item,
  onRemove,
  onRetry,
}: QueueCardProps<TResult>) => (
  <div className="import-files-dashboard__queue-card-header">
    <Typography.Text
      strong
      ellipsis
      className="import-files-dashboard__queue-file-name"
    >
      {item.fileName}
    </Typography.Text>
    {onRetry &&
      item.file &&
      (item.status === "failed" || item.status === "canceled") && (
        <Tooltip title="Retry">
          <Button
            type="text"
            size="small"
            icon={<RedoOutlined />}
            aria-label={`Retry ${item.fileName}`}
            className="import-files-dashboard__queue-action"
            onClick={() => onRetry(item.id)}
          />
        </Tooltip>
      )}
    <Tooltip title="Remove">
      <Button
        type="text"
        danger
        size="small"
        icon={<DeleteOutlined />}
        aria-label={`Remove ${item.fileName}`}
        className="import-files-dashboard__queue-action"
        onClick={() => onRemove(item.id)}
      />
    </Tooltip>
  </div>
);

const CurrentUploadCard = <TResult,>({
  item,
  onRemove,
  onRetry,
}: QueueCardProps<TResult>) => {
  const displayState = getUploadDisplayState(item);
  const progressPercent = displayState.displayPercent;
  const hasDisplayProgress = displayState.hasProgress;
  const isAwaitingFinalize =
    item.status === "queued" &&
    displayState.totalBytes > 0 &&
    displayState.acknowledgedBytes >= displayState.totalBytes;
  const statusLabel = getQueueItemStatusLabel(item, isAwaitingFinalize);
  const shouldShowProgress = item.status === "uploading" || hasDisplayProgress;
  const progressLabel =
    item.transportStatus === "chunk_uploading"
      ? `${progressPercent}%`
      : statusLabel ?? `${progressPercent}%`;

  return (
    <div className="import-files-dashboard__queue-card">
      <QueueCardHeader item={item} onRemove={onRemove} onRetry={onRetry} />
      <div
        className={`import-files-dashboard__queue-progress${getQueueItemProgressVariantClass(
          item,
          isAwaitingFinalize
        )}`}
      >
        {shouldShowProgress && (
          <span
            className="import-files-dashboard__queue-progress-fill"
            style={{ width: `${progressPercent}%` }}
          />
        )}
        <span className="import-files-dashboard__queue-progress-content">
          <span>
            {shouldShowProgress ? progressLabel : displayState.sizeLabel}
          </span>
          {(shouldShowProgress || displayState.hasKnownSize) && (
            <span>{displayState.sizeLabel}</span>
          )}
        </span>
      </div>
    </div>
  );
};

const QueueFileCard = <TResult,>({
  item,
  onRemove,
  onRetry,
}: QueueCardProps<TResult>) => {
  const displayState = getUploadDisplayState(item);
  if (
    item.status === "uploading" ||
    displayState.hasProgress
  ) {
    return (
      <CurrentUploadCard
        item={item}
        onRemove={onRemove}
        onRetry={onRetry}
      />
    );
  }

  return (
    <div className="import-files-dashboard__queue-card">
      <QueueCardHeader item={item} onRemove={onRemove} onRetry={onRetry} />
      <div className="import-files-dashboard__queue-progress">
        <span className="import-files-dashboard__queue-progress-content">
          <span>{displayState.sizeLabel}</span>
        </span>
      </div>
    </div>
  );
};

const RecentActivityCard = <TResult,>({
  item,
  onRemove,
  onRetry,
  renderRecentActivityTooltip,
}: RecentActivityCardProps<TResult>) => {
  const tooltipTitle = renderRecentActivityTooltip?.(item);

  return (
    <div
      className={`import-files-dashboard__queue-card import-files-dashboard__queue-card--recent${
        item.status === "uploaded"
          ? " import-files-dashboard__queue-card--success"
          : item.status === "skipped"
          ? " import-files-dashboard__queue-card--neutral"
          : " import-files-dashboard__queue-card--failure"
      }`}
    >
      <QueueCardHeader item={item} onRemove={onRemove} onRetry={onRetry} />
      <Tooltip title={tooltipTitle}>
        <div
          className={`import-files-dashboard__queue-progress${
            item.status === "uploaded"
              ? " import-files-dashboard__queue-progress--success"
              : item.status === "skipped"
              ? " import-files-dashboard__queue-progress--neutral"
              : " import-files-dashboard__queue-progress--failure"
          }`}
        >
          <span className="import-files-dashboard__queue-progress-fill" />
          <span className="import-files-dashboard__queue-progress-content">
            <span>
              {item.status === "uploaded"
                ? "Uploaded"
                : item.status === "skipped"
                ? "Skipped"
                : "Failed"}
            </span>
            <span>{getUploadDisplayState(item).sizeLabel}</span>
          </span>
        </div>
      </Tooltip>
    </div>
  );
};

export const QueuedFileUploader = <TResult,>({
  queue,
  copy,
  onViewResults,
  onEditSettings,
  hasResults = false,
  renderRecentActivityTooltip,
}: QueuedFileUploaderProps<TResult>) => {
  const fileInputRef = useRef<HTMLInputElement | null>(null);

  const addFilesLabel = copy?.addFilesLabel ?? "Add Files";
  const queueTitle = copy?.queueTitle ?? "Queue";
  const recentActivityTitle = copy?.recentActivityTitle ?? "Recent Activity";
  const emptyQueueText = copy?.emptyQueueText ?? "Add files to get started.";
  const emptyRecentActivityText =
    copy?.emptyRecentActivityText ??
    "Finished files and problem files will appear here as the upload runs.";

  const renderQueueItem = useCallback(
    (item: QueuedFileUploadItem<TResult>) => (
      <QueueFileCard
        item={item}
        onRemove={queue.removeQueueItem}
        onRetry={queue.retryQueueItem}
      />
    ),
    [queue.removeQueueItem]
  );

  const getQueueRowHeight = useCallback(
    () => VIRTUAL_UPLOAD_QUEUE_ROW_HEIGHT,
    []
  );

  const renderRecentActivityItem = useCallback(
    (item: QueuedFileUploadItem<TResult>) => (
      <RecentActivityCard
        item={item}
        onRemove={queue.removeQueueItem}
        onRetry={queue.retryQueueItem}
        renderRecentActivityTooltip={renderRecentActivityTooltip}
      />
    ),
    [queue.removeQueueItem, queue.retryQueueItem, renderRecentActivityTooltip]
  );

  const openFilePicker = () => fileInputRef.current?.click();
  const toolbarActions: ToolbarAction[] = [
    {
      key: "upload-control",
      label: queue.uploadControl.label,
      icon: queue.uploadControl.icon,
      onClick: queue.uploadControl.onClick,
      disabled: queue.uploadControl.disabled || queue.isResettingQueue,
    },
    {
      key: "retry",
      label: "Retry Failed",
      icon: <RedoOutlined />,
      onClick: queue.retryFailed,
      disabled:
        queue.isRestoring ||
        queue.isResettingQueue ||
        queue.failedItems.length === 0,
    },
    {
      key: "results",
      label: "View Results",
      icon: <EyeOutlined />,
      onClick: onViewResults,
      disabled: queue.isResettingQueue || !hasResults || !onViewResults,
    },
    {
      key: "settings",
      label: "Settings",
      icon: <SettingOutlined />,
      onClick: onEditSettings,
      disabled: queue.isResettingQueue || !onEditSettings,
    },
    {
      key: "clear",
      label: "Clear",
      icon: <ClearOutlined />,
      onClick: () => {
        void queue.resetQueueState();
      },
      disabled: queue.isRestoring || queue.isResettingQueue,
    },
  ];
  const toolbarMetrics: ToolbarMetric[] = [
    {
      key: "added",
      label: "Added",
      value: queue.queueItems.length,
    },
    {
      key: "uploaded",
      label: "Uploaded",
      value: queue.queueStats.uploaded,
    },
    {
      key: "failed",
      label: "Failed",
      value: queue.failedItems.length,
    },
  ];

  return (
    <div
      className={`import-files-dashboard ${
        queue.isDragActive ? "import-files-dashboard--drag-active" : ""
      }`}
      style={{ position: "relative" }}
      onDrop={(event) => {
        void queue.handleDrop(event);
      }}
      onDragOver={queue.handleDragOver}
      onDragEnter={queue.handleDragEnter}
      onDragLeave={queue.handleDragLeave}
    >
      <input
        ref={fileInputRef}
        type="file"
        multiple
        style={{ display: "none" }}
        disabled={queue.isLoadingConfig || queue.isResettingQueue}
        onChange={(event) => {
          void queue.handleFileSelect(event);
        }}
      />

      <div className="import-files-dashboard__controls import-files-dashboard__controls-panel">
        <ResponsiveToolbar
          className="import-files-dashboard__toolbar"
          metrics={toolbarMetrics}
          overflowActions={toolbarActions}
          primary={
            <Button
              className="import-files-dashboard__toolbar-primary"
              disabled={
                queue.isLoadingConfig ||
                queue.isRestoring ||
                queue.isResettingQueue
              }
              icon={<FileAddOutlined />}
              onClick={openFilePicker}
            >
              {addFilesLabel}
            </Button>
          }
        />
      </div>

      <div className="import-files-dashboard__body">
        <div className="import-files-dashboard__body-grid">
          <div className="import-files-dashboard__pane-card">
            <div className="import-files-dashboard__pane-header">
              <div className="import-files-dashboard__pane-header-main">
                <Typography.Title level={4}>{queueTitle}</Typography.Title>
                <Typography.Text
                  type="secondary"
                  className="import-files-dashboard__queued-count"
                >
                  {queue.queueStats.remaining} remaining
                </Typography.Text>
              </div>
              <div className="import-files-dashboard__pane-summary">
                <div className="import-files-dashboard__queue-progress import-files-dashboard__queue-progress--active import-files-dashboard__queue-progress--overall">
                  <span
                    className="import-files-dashboard__queue-progress-fill"
                    style={{ width: `${queue.aggregateProgress}%` }}
                  />
                  <span className="import-files-dashboard__queue-progress-content">
                    <span>Overall progress</span>
                    <span>{queue.aggregateProgress}%</span>
                  </span>
                </div>
              </div>
            </div>
            <div className="import-files-dashboard__pane-scroll">
              {queue.uploadQueueItems.length === 0 ? (
                <div className="import-files-dashboard__placeholder-state">
                  <div className="import-files-dashboard__queue-placeholder">
                    <div className="import-files-dashboard__queue-placeholder-line" />
                    <div className="import-files-dashboard__queue-progress import-files-dashboard__queue-progress--placeholder" />
                  </div>
                  <Typography.Text
                    type="secondary"
                    className="import-files-dashboard__queue-placeholder-copy"
                  >
                    {emptyQueueText}
                  </Typography.Text>
                </div>
              ) : (
                <VirtualFileList
                  items={queue.uploadQueueItems}
                  rowHeight={VIRTUAL_UPLOAD_QUEUE_ROW_HEIGHT}
                  getRowHeight={getQueueRowHeight}
                  renderItem={renderQueueItem}
                  emptyState={null}
                />
              )}
            </div>
          </div>
          <div className="import-files-dashboard__pane-card">
            <div className="import-files-dashboard__pane-header">
              <Typography.Title level={4}>{recentActivityTitle}</Typography.Title>
              <Typography.Text type="secondary">
                {queue.recentActivity.length} completed
              </Typography.Text>
            </div>
            <div className="import-files-dashboard__pane-scroll">
              <VirtualFileList
                items={queue.recentActivity}
                rowHeight={VIRTUAL_UPLOAD_QUEUE_ROW_HEIGHT}
                renderItem={renderRecentActivityItem}
                emptyState={
                  <div className="import-files-dashboard__placeholder-state">
                    <div className="import-files-dashboard__queue-placeholder">
                      <div className="import-files-dashboard__queue-placeholder-line" />
                      <div className="import-files-dashboard__queue-progress import-files-dashboard__queue-progress--placeholder" />
                    </div>
                    <Typography.Paragraph
                      type="secondary"
                      className="import-files-dashboard__queue-placeholder-copy"
                    >
                      {emptyRecentActivityText}
                    </Typography.Paragraph>
                  </div>
                }
              />
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
