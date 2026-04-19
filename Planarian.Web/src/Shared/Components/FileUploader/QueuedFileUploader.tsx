import {
  Button,
  Dropdown,
  Tooltip,
  Typography,
} from "antd";
import {
  ClearOutlined,
  ClockCircleOutlined,
  DeleteOutlined,
  DownOutlined,
  EyeOutlined,
  FileAddOutlined,
  RedoOutlined,
} from "@ant-design/icons";
import React, { useCallback, useEffect, useRef, useState } from "react";
import "./QueuedFileUploader.scss";
import {
  getUploadDisplayState,
  VIRTUAL_QUEUE_ROW_HEIGHT,
  VIRTUAL_UPLOAD_QUEUE_ROW_HEIGHT,
} from "./fileUploaderHelpers";
import { QueuedFileUploadItem, QueuedFileUploaderProps } from "./types";
import { VirtualFileList } from "./VirtualFileList";

interface QueueCardProps<TResult> {
  item: QueuedFileUploadItem<TResult>;
  onRemove: (itemId: string) => void;
  renderRecentActivityTooltip?: (
    item: QueuedFileUploadItem<TResult>
  ) => React.ReactNode;
}

const QueueCardHeader = <TResult,>({
  item,
  onRemove,
}: QueueCardProps<TResult>) => (
  <div className="import-files-dashboard__queue-card-header">
    <Typography.Text
      strong
      ellipsis
      className="import-files-dashboard__queue-file-name"
    >
      {item.fileName}
    </Typography.Text>
    <Tooltip title="Remove">
      <Button
        type="text"
        danger
        size="small"
        icon={<DeleteOutlined />}
        aria-label={`Remove ${item.fileName}`}
        className="import-files-dashboard__queue-remove"
        onClick={() => onRemove(item.id)}
      />
    </Tooltip>
  </div>
);

const CurrentUploadCard = <TResult,>({
  item,
  onRemove,
}: QueueCardProps<TResult>) => {
  const isActive = item.status === "uploading";
  const displayState = getUploadDisplayState(item);
  const progressPercent = displayState.displayPercent;
  const hasDisplayProgress = displayState.hasProgress;
  const shouldShowProgress = isActive || hasDisplayProgress;

  return (
    <div className="import-files-dashboard__queue-card">
      <QueueCardHeader item={item} onRemove={onRemove} />
      <div
        className={`import-files-dashboard__queue-progress${
          isActive ? " import-files-dashboard__queue-progress--active" : ""
        }`}
      >
        {shouldShowProgress && (
          <span
            className="import-files-dashboard__queue-progress-fill"
            style={{ width: `${progressPercent}%` }}
          />
        )}
        <span className="import-files-dashboard__queue-progress-content">
          <span>
            {shouldShowProgress
              ? `${progressPercent}%`
              : displayState.sizeLabel}
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
}: QueueCardProps<TResult>) => {
  const displayState = getUploadDisplayState(item);
  if (
    item.status === "uploading" ||
    (displayState.hasProgress && displayState.displayPercent < 100)
  ) {
    return <CurrentUploadCard item={item} onRemove={onRemove} />;
  }

  const isRetrying = item.status === "retry_wait";

  return (
    <div className="import-files-dashboard__queue-card">
      <QueueCardHeader item={item} onRemove={onRemove} />
      <div className="import-files-dashboard__queue-progress">
        <span className="import-files-dashboard__queue-progress-content">
          <span>{displayState.sizeLabel}</span>
        </span>
      </div>
      {isRetrying && item.retryAt && (
        <Typography.Text
          type="secondary"
          ellipsis
          className="import-files-dashboard__queue-note"
        >
          <ClockCircleOutlined /> Trying again at{" "}
          {new Date(item.retryAt).toLocaleTimeString()}
        </Typography.Text>
      )}
    </div>
  );
};

const RecentActivityCard = <TResult,>({
  item,
  onRemove,
  renderRecentActivityTooltip,
}: QueueCardProps<TResult>) => {
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
      <QueueCardHeader item={item} onRemove={onRemove} />
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
  hasResults = false,
  renderRecentActivityTooltip,
}: QueuedFileUploaderProps<TResult>) => {
  const [collapsedToolbarActionsCount, setCollapsedToolbarActionsCount] =
    useState(0);
  const toolbarContainerRef = useRef<HTMLDivElement | null>(null);
  const toolbarActionsRef = useRef<HTMLDivElement | null>(null);
  const toolbarStatsRef = useRef<HTMLDivElement | null>(null);
  const toolbarActionWidthsRef = useRef([0, 0, 0, 0, 0]);
  const toolbarMoreWidthRef = useRef(82);
  const fileInputRef = useRef<HTMLInputElement | null>(null);

  const addFilesLabel = copy?.addFilesLabel ?? "Add Files";
  const queueTitle = copy?.queueTitle ?? "Queue";
  const recentActivityTitle = copy?.recentActivityTitle ?? "Recent Activity";
  const emptyQueueText = copy?.emptyQueueText ?? "Add files to get started.";
  const emptyRecentActivityText =
    copy?.emptyRecentActivityText ??
    "Finished files and problem files will appear here as the upload runs.";

  useEffect(() => {
    const toolbarContainer = toolbarContainerRef.current;
    const toolbarActions = toolbarActionsRef.current;
    const toolbarStats = toolbarStatsRef.current;

    if (!toolbarContainer || !toolbarActions || !toolbarStats) return;

    let animationFrame: number | null = null;

    const updateToolbarMode = () => {
      if (animationFrame) {
        window.cancelAnimationFrame(animationFrame);
      }

      animationFrame = window.requestAnimationFrame(() => {
        toolbarActions
          .querySelectorAll<HTMLElement>("[data-toolbar-action-index]")
          .forEach((element) => {
            const index = Number(element.dataset.toolbarActionIndex);
            if (index >= 0 && index <= 4 && element.offsetWidth > 0) {
              toolbarActionWidthsRef.current[index] = element.offsetWidth;
            }
          });

        const moreButton = toolbarActions.querySelector<HTMLElement>(
          "[data-toolbar-more]"
        );
        if (moreButton && moreButton.offsetWidth > 0) {
          toolbarMoreWidthRef.current = moreButton.offsetWidth;
        }

        const statsWidth = toolbarStats.offsetWidth;
        const toolbarGap = 8;
        const actionGap = 8;
        const availableWidth = toolbarContainer.clientWidth;

        const getActionsWidth = (collapsedCount: number) => {
          const visibleActionIndexes = [0, 1, 2, 3, 4].filter(
            (index) =>
              (collapsedCount < 5 && index === 0) ||
              index < 5 - collapsedCount
          );
          const visibleActionsWidth = visibleActionIndexes.reduce(
            (total, index) => total + toolbarActionWidthsRef.current[index],
            0
          );
          const visibleControlsCount =
            visibleActionIndexes.length + (collapsedCount > 0 ? 1 : 0);

          return (
            visibleActionsWidth +
            (collapsedCount > 0 ? toolbarMoreWidthRef.current : 0) +
            actionGap * Math.max(0, visibleControlsCount - 1)
          );
        };

        let nextCollapsedCount = 0;
        for (let count = 0; count <= 5; count += 1) {
          const requiredWidth = getActionsWidth(count) + statsWidth + toolbarGap;
          if (requiredWidth <= availableWidth - 2) {
            nextCollapsedCount = count;
            break;
          }

          nextCollapsedCount = count;
        }

        setCollapsedToolbarActionsCount((current) =>
          current === nextCollapsedCount ? current : nextCollapsedCount
        );
      });
    };

    updateToolbarMode();

    const resizeObserver = new ResizeObserver(updateToolbarMode);
    resizeObserver.observe(toolbarContainer);
    resizeObserver.observe(toolbarActions);
    resizeObserver.observe(toolbarStats);

    return () => {
      resizeObserver.disconnect();
      if (animationFrame) {
        window.cancelAnimationFrame(animationFrame);
      }
    };
  }, [collapsedToolbarActionsCount]);

  const renderQueueItem = useCallback(
    (item: QueuedFileUploadItem<TResult>) => (
      <QueueFileCard item={item} onRemove={queue.removeQueueItem} />
    ),
    [queue.removeQueueItem]
  );

  const getQueueRowHeight = useCallback(
    (item: QueuedFileUploadItem<TResult>) =>
      item.status === "retry_wait" && item.retryAt
        ? VIRTUAL_QUEUE_ROW_HEIGHT
        : VIRTUAL_UPLOAD_QUEUE_ROW_HEIGHT,
    []
  );

  const renderRecentActivityItem = useCallback(
    (item: QueuedFileUploadItem<TResult>) => (
      <RecentActivityCard
        item={item}
        onRemove={queue.removeQueueItem}
        renderRecentActivityTooltip={renderRecentActivityTooltip}
      />
    ),
    [queue.removeQueueItem, renderRecentActivityTooltip]
  );

  const openFilePicker = () => fileInputRef.current?.click();

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
        disabled={queue.isResettingQueue}
        onChange={(event) => {
          void queue.handleFileSelect(event);
        }}
      />

      <div className="import-files-dashboard__controls import-files-dashboard__controls-panel">
        <div
          className="import-files-dashboard__controls-actions"
          ref={toolbarContainerRef}
        >
          <div
            className={`import-files-dashboard__toolbar-actions import-files-dashboard__toolbar-actions--collapse-${collapsedToolbarActionsCount}`}
            ref={toolbarActionsRef}
          >
            <Button
              className="import-files-dashboard__toolbar-primary"
              data-toolbar-action-index="0"
              icon={<FileAddOutlined />}
              onClick={openFilePicker}
            >
              {addFilesLabel}
            </Button>
            <Button
              className="import-files-dashboard__toolbar-secondary import-files-dashboard__toolbar-secondary--upload-control"
              data-toolbar-action-index="1"
              icon={queue.uploadControl.icon}
              onClick={queue.uploadControl.onClick}
              disabled={queue.uploadControl.disabled || queue.isResettingQueue}
            >
              {queue.uploadControl.label}
            </Button>
            <Button
              className="import-files-dashboard__toolbar-secondary import-files-dashboard__toolbar-secondary--retry"
              data-toolbar-action-index="2"
              icon={<RedoOutlined />}
              onClick={queue.retryFailed}
              disabled={
                queue.isRestoring ||
                queue.isResettingQueue ||
                queue.failedItems.length === 0
              }
            >
              Retry Failed
            </Button>
            <Button
              className="import-files-dashboard__toolbar-secondary import-files-dashboard__toolbar-secondary--results"
              data-toolbar-action-index="3"
              icon={<EyeOutlined />}
              onClick={onViewResults}
              disabled={queue.isResettingQueue || !hasResults || !onViewResults}
            >
              View Results
            </Button>
            <Button
              className="import-files-dashboard__toolbar-secondary import-files-dashboard__toolbar-secondary--reset"
              data-toolbar-action-index="4"
              icon={<ClearOutlined />}
              onClick={() => void queue.resetQueueState()}
              disabled={queue.isRestoring || queue.isResettingQueue}
            >
              Reset
            </Button>
            <Dropdown
              className="import-files-dashboard__toolbar-more"
              overlayClassName="import-files-dashboard__toolbar-menu"
              trigger={["click"]}
              menu={{
                items: [
                  ...(collapsedToolbarActionsCount >= 5
                    ? [
                        {
                          key: "add",
                          icon: <FileAddOutlined />,
                          label: addFilesLabel,
                          disabled: queue.isRestoring || queue.isResettingQueue,
                        },
                      ]
                    : []),
                  ...(collapsedToolbarActionsCount >= 4
                    ? [
                        {
                          key: "upload-control",
                          icon: queue.uploadControl.icon,
                          label: queue.uploadControl.label,
                          disabled:
                            queue.uploadControl.disabled || queue.isResettingQueue,
                        },
                      ]
                    : []),
                  ...(collapsedToolbarActionsCount >= 3
                    ? [
                        {
                          key: "retry",
                          icon: <RedoOutlined />,
                          label: "Retry Failed",
                          disabled:
                            queue.isRestoring ||
                            queue.isResettingQueue ||
                            queue.failedItems.length === 0,
                        },
                      ]
                    : []),
                  ...(collapsedToolbarActionsCount >= 2
                    ? [
                        {
                          key: "results",
                          icon: <EyeOutlined />,
                          label: "View Results",
                          disabled:
                            queue.isResettingQueue || !hasResults || !onViewResults,
                        },
                      ]
                    : []),
                  ...(collapsedToolbarActionsCount >= 1
                    ? [
                        {
                          key: "reset",
                          icon: <ClearOutlined />,
                          label: "Reset",
                          disabled: queue.isRestoring || queue.isResettingQueue,
                        },
                      ]
                    : []),
                ],
                onClick: ({ key }) => {
                  if (queue.isResettingQueue) {
                    return;
                  }

                  if (key === "add") {
                    openFilePicker();
                    return;
                  }

                  if (key === "upload-control") {
                    queue.uploadControl.onClick();
                    return;
                  }

                  if (key === "retry") {
                    queue.retryFailed();
                    return;
                  }

                  if (key === "results") {
                    onViewResults?.();
                    return;
                  }

                  if (key === "reset") {
                    void queue.resetQueueState();
                  }
                },
              }}
            >
              <Button
                data-toolbar-more
                icon={<DownOutlined />}
                aria-label="More file upload actions"
              >
                {collapsedToolbarActionsCount >= 5 ? "Actions" : "More"}
              </Button>
            </Dropdown>
          </div>
          <div
            className="import-files-dashboard__metrics import-files-dashboard__metrics--toolbar"
            ref={toolbarStatsRef}
          >
            <div className="import-files-dashboard__metric import-files-dashboard__metric--compact">
              <span className="import-files-dashboard__metric-label">Added</span>
              <span className="import-files-dashboard__metric-value">
                {queue.queueItems.length}
              </span>
            </div>
            <div className="import-files-dashboard__metric import-files-dashboard__metric--compact">
              <span className="import-files-dashboard__metric-label">
                Uploaded
              </span>
              <span className="import-files-dashboard__metric-value">
                {queue.queueStats.uploaded}
              </span>
            </div>
            <div className="import-files-dashboard__metric import-files-dashboard__metric--compact">
              <span className="import-files-dashboard__metric-label">Failed</span>
              <span className="import-files-dashboard__metric-value">
                {queue.failedItems.length}
              </span>
            </div>
          </div>
        </div>
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
