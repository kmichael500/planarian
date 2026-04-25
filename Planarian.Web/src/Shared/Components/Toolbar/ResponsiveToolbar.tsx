import { Button, ButtonProps, Dropdown, MenuProps, Tooltip } from "antd";
import { DownOutlined } from "@ant-design/icons";
import React, { useEffect, useMemo, useRef, useState } from "react";
import "./ResponsiveToolbar.scss";

export interface ToolbarAction {
  key: string;
  label: React.ReactNode;
  icon?: React.ReactNode;
  onClick?: () => void;
  disabled?: boolean;
  loading?: boolean;
  tooltip?: React.ReactNode;
  type?: ButtonProps["type"];
  className?: string;
}

export interface ToolbarMetric {
  key: string;
  label: React.ReactNode;
  value: React.ReactNode;
  className?: string;
}

interface ResponsiveToolbarProps {
  primary?: React.ReactNode;
  persistentContent?: React.ReactNode;
  overflowActions?: ToolbarAction[];
  metrics?: ToolbarMetric[];
  className?: string;
  moreLabel?: string;
}

const TOOLBAR_GAP = 8;

const ResponsiveToolbar: React.FC<ResponsiveToolbarProps> = ({
  primary,
  persistentContent,
  overflowActions = [],
  metrics = [],
  className,
  moreLabel = "More",
}) => {
  const [collapsedActionsCount, setCollapsedActionsCount] = useState(0);
  const containerRef = useRef<HTMLDivElement | null>(null);
  const primaryRef = useRef<HTMLDivElement | null>(null);
  const persistentRef = useRef<HTMLDivElement | null>(null);
  const actionsRef = useRef<HTMLDivElement | null>(null);
  const metricsRef = useRef<HTMLDivElement | null>(null);
  const moreMeasureRef = useRef<HTMLDivElement | null>(null);
  const actionWidthsRef = useRef<number[]>([]);

  const classNames = ["planarian-toolbar", className].filter(Boolean).join(" ");

  useEffect(() => {
    actionWidthsRef.current = Array(overflowActions.length).fill(0);
  }, [overflowActions.length]);

  useEffect(() => {
    const container = containerRef.current;
    if (!container) {
      return;
    }

    let animationFrame: number | null = null;

    const updateLayout = () => {
      if (animationFrame) {
        window.cancelAnimationFrame(animationFrame);
      }

      animationFrame = window.requestAnimationFrame(() => {
        const actionsElement = actionsRef.current;
        if (actionsElement) {
          actionsElement
            .querySelectorAll<HTMLElement>("[data-toolbar-action-index]")
            .forEach((element) => {
              const index = Number(element.dataset.toolbarActionIndex);
              if (
                Number.isFinite(index) &&
                index >= 0 &&
                index < overflowActions.length &&
                element.offsetWidth > 0
              ) {
                actionWidthsRef.current[index] = element.offsetWidth;
              }
            });
        }

        const metricsElement = metricsRef.current;
        const metricsWrapped =
          metricsElement !== null &&
          metricsElement.offsetTop > container.offsetTop + 1;
        const metricsWidth = metricsWrapped
          ? 0
          : metricsElement?.offsetWidth ?? 0;

        const primaryWidth = primaryRef.current?.offsetWidth ?? 0;
        const persistentWidth = persistentRef.current?.offsetWidth ?? 0;
        const moreWidth = moreMeasureRef.current?.offsetWidth ?? 82;

        const getActionsWidth = (collapsedCount: number) => {
          const visibleCount = Math.max(
            0,
            overflowActions.length - collapsedCount
          );
          const visibleWidth = Array.from({ length: visibleCount }, (_, index) =>
            actionWidthsRef.current[index] ?? 0
          ).reduce((total, width) => total + width, 0);
          const controlsCount = visibleCount + (collapsedCount > 0 ? 1 : 0);

          return (
            visibleWidth +
            (collapsedCount > 0 ? moreWidth : 0) +
            TOOLBAR_GAP * Math.max(0, controlsCount - 1)
          );
        };

        let nextCollapsedCount = overflowActions.length;
        for (let count = 0; count <= overflowActions.length; count += 1) {
          const actionsWidth = getActionsWidth(count);
          const leftSectionsCount = [primaryWidth, persistentWidth, actionsWidth]
            .filter((width) => width > 0).length;
          const leftWidth =
            primaryWidth +
            persistentWidth +
            actionsWidth +
            TOOLBAR_GAP * Math.max(0, leftSectionsCount - 1);
          const totalWidth =
            leftWidth +
            metricsWidth +
            (leftWidth > 0 && metricsWidth > 0 ? TOOLBAR_GAP : 0);

          if (totalWidth <= container.clientWidth - 2) {
            nextCollapsedCount = count;
            break;
          }
        }

        setCollapsedActionsCount(nextCollapsedCount);
      });
    };

    updateLayout();

    const resizeObserver = new ResizeObserver(() => {
      updateLayout();
    });

    resizeObserver.observe(container);
    primaryRef.current && resizeObserver.observe(primaryRef.current);
    persistentRef.current && resizeObserver.observe(persistentRef.current);
    actionsRef.current && resizeObserver.observe(actionsRef.current);
    metricsRef.current && resizeObserver.observe(metricsRef.current);
    moreMeasureRef.current && resizeObserver.observe(moreMeasureRef.current);

    return () => {
      if (animationFrame) {
        window.cancelAnimationFrame(animationFrame);
      }
      resizeObserver.disconnect();
    };
  }, [overflowActions]);

  const visibleActions = overflowActions.slice(
    0,
    Math.max(0, overflowActions.length - collapsedActionsCount)
  );
  const collapsedActions = overflowActions.slice(visibleActions.length);

  const overflowMenuItems = useMemo<MenuProps["items"]>(
    () =>
      collapsedActions.map((action) => ({
        key: action.key,
        icon: action.icon,
        label: action.label,
        disabled: action.disabled,
      })),
    [collapsedActions]
  );

  const renderAction = (
    action: ToolbarAction,
    index: number,
    extraClassName?: string
  ) => {
    const button = (
      <Button
        className={[
          "planarian-toolbar__action-button",
          action.className,
          extraClassName,
        ]
          .filter(Boolean)
          .join(" ")}
        data-toolbar-action-index={index}
        disabled={action.disabled}
        icon={action.icon}
        loading={action.loading}
        onClick={action.onClick}
        type={action.type}
      >
        {action.label}
      </Button>
    );

    return action.tooltip ? (
      <Tooltip key={action.key} title={action.tooltip}>
        {button}
      </Tooltip>
    ) : (
      React.cloneElement(button, { key: action.key })
    );
  };

  return (
    <div className={classNames} ref={containerRef}>
      <div className="planarian-toolbar__left">
        {primary && (
          <div className="planarian-toolbar__primary" ref={primaryRef}>
            {primary}
          </div>
        )}
        {persistentContent && (
          <div className="planarian-toolbar__persistent" ref={persistentRef}>
            {persistentContent}
          </div>
        )}
        {(overflowActions.length > 0 || collapsedActions.length > 0) && (
          <div className="planarian-toolbar__actions" ref={actionsRef}>
            {visibleActions.map((action, index) => renderAction(action, index))}
            {collapsedActions.length > 0 && (
              <Dropdown
                menu={{
                  items: overflowMenuItems,
                  onClick: ({ key }) => {
                    const action = collapsedActions.find(
                      (item) => item.key === key
                    );
                    action?.onClick?.();
                  },
                }}
                overlayClassName="planarian-toolbar__menu"
                trigger={["click"]}
              >
                <Button
                  className="planarian-toolbar__more-button"
                  icon={<DownOutlined />}
                >
                  {moreLabel}
                </Button>
              </Dropdown>
            )}
          </div>
        )}
      </div>
      <div className="planarian-toolbar__right">
        {metrics.length > 0 && (
          <div className="planarian-toolbar__metrics" ref={metricsRef}>
            {metrics.map((metric) => (
              <div
                className={[
                  "planarian-toolbar__metric",
                  metric.className,
                ]
                  .filter(Boolean)
                  .join(" ")}
                key={metric.key}
              >
                <span className="planarian-toolbar__metric-label">
                  {metric.label}
                </span>
                <span className="planarian-toolbar__metric-value">
                  {metric.value}
                </span>
              </div>
            ))}
          </div>
        )}
      </div>
      <div className="planarian-toolbar__measure" ref={moreMeasureRef}>
        <Button className="planarian-toolbar__action-button" icon={<DownOutlined />}>
          {moreLabel}
        </Button>
      </div>
    </div>
  );
};

export { ResponsiveToolbar };
