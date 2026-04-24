import React from "react";
import { Link } from "react-router-dom";
import {
  PlanarianButton,
  PlanarianButtonType,
} from "../Buttons/PlanarianButtton";
import "./GridCard.scss";

export interface GridCardAction {
  key: string;
  label: React.ReactNode;
  icon?: React.ReactNode;
  to?: string;
  href?: string;
  onClick?: PlanarianButtonType["onClick"];
  type?: PlanarianButtonType["type"];
  size?: PlanarianButtonType["size"];
  disabled?: boolean;
  loading?: boolean;
  target?: React.HTMLAttributeAnchorTarget;
  rel?: string;
  tooltip?: React.ReactNode;
}

interface GridCardProps {
  header?: React.ReactNode;
  headerExtra?: React.ReactNode;
  footer?: React.ReactNode;
  actions?: GridCardAction[];
  stickyHeader?: boolean;
  stickyFooter?: boolean;
  children?: React.ReactNode;
  className?: string;
  style?: React.CSSProperties;
}

const GridCard: React.FC<GridCardProps> = ({
  header,
  headerExtra,
  footer,
  actions,
  stickyHeader = false,
  stickyFooter = false,
  children,
  className,
  style,
}) => {
  const classNames = [
    "planarian-grid-card",
    stickyHeader ? "planarian-grid-card--sticky-header" : null,
    stickyFooter ? "planarian-grid-card--sticky-footer" : null,
    className,
  ]
    .filter(Boolean)
    .join(" ");
  const hasFooter = Boolean(footer) || Boolean(actions?.length);

  const renderAction = (action: GridCardAction) => {
    const button = (
      <PlanarianButton
        alwaysShowChildren
        disabled={action.disabled}
        icon={action.icon}
        loading={action.loading}
        onClick={action.onClick}
        rel={action.rel}
        size={action.size}
        target={action.target}
        tooltip={action.tooltip}
        type={action.type}
      >
        {action.label}
      </PlanarianButton>
    );

    let content = button;
    if (action.to) {
      content = (
        <Link className="planarian-grid-card__action-link" to={action.to}>
          {button}
        </Link>
      );
    } else if (action.href) {
      content = (
        <a
          className="planarian-grid-card__action-link"
          href={action.href}
          rel={action.rel}
          target={action.target}
        >
          {button}
        </a>
      );
    }

    return (
      <div
        className={`planarian-grid-card__action planarian-grid-card__action--${action.key}`}
        key={action.key}
      >
        {content}
      </div>
    );
  };

  return (
    <div className={classNames} style={style}>
      {(header || headerExtra) && (
        <div className="planarian-grid-card__header">
          <div className="planarian-grid-card__header-main">{header}</div>
          {headerExtra && (
            <div className="planarian-grid-card__header-extra">
              {headerExtra}
            </div>
          )}
        </div>
      )}
      <div className="planarian-grid-card__body">{children}</div>
      {hasFooter && (
        <div className="planarian-grid-card__footer">
          {footer && (
            <div className="planarian-grid-card__footer-content">{footer}</div>
          )}
          {actions?.length ? (
            <div className="planarian-grid-card__actions">
              {actions.map(renderAction)}
            </div>
          ) : null}
        </div>
      )}
    </div>
  );
};

export { GridCard };
