import type React from "react";
import type { ReactNode } from "react";
import "./SpinnerCard.scss";

interface SpinnerCardProps {
  children?: ReactNode;
  className?: string;
  numberOfCards?: number;
  spinning: boolean;
}

const DEFAULT_CARD_COUNT = 8;

const SkeletonCard = () => (
  <div className="planarian-spinner-card__placeholder-card">
    <div className="planarian-spinner-card__placeholder-header">
      <span className="planarian-spinner-card__placeholder planarian-spinner-card__placeholder-title" />
      <span className="planarian-spinner-card__placeholder planarian-spinner-card__placeholder-badge" />
    </div>
    <div className="planarian-spinner-card__placeholder-body">
      <span className="planarian-spinner-card__placeholder planarian-spinner-card__placeholder-line planarian-spinner-card__placeholder-line--short" />
      <span className="planarian-spinner-card__placeholder planarian-spinner-card__placeholder-line" />
      <div className="planarian-spinner-card__placeholder-tags">
        <span className="planarian-spinner-card__placeholder planarian-spinner-card__placeholder-tag" />
        <span className="planarian-spinner-card__placeholder planarian-spinner-card__placeholder-tag planarian-spinner-card__placeholder-tag--short" />
      </div>
    </div>
    <div className="planarian-spinner-card__placeholder-footer">
      <span className="planarian-spinner-card__placeholder planarian-spinner-card__placeholder-button" />
      <span className="planarian-spinner-card__placeholder planarian-spinner-card__placeholder-button" />
    </div>
  </div>
);

const SpinnerCardComponent: React.FC<SpinnerCardProps> = ({
  spinning,
  children,
  className,
  numberOfCards = DEFAULT_CARD_COUNT,
}) => {
  if (!spinning) {
    return <>{children}</>;
  }

  const classNames = ["planarian-spinner-card", className]
    .filter(Boolean)
    .join(" ");

  return (
    <div className={classNames} aria-busy="true" aria-live="polite">
      <div className="planarian-spinner-card__grid" aria-hidden="true">
        {Array.from({ length: numberOfCards }, (_, index) => (
          <SkeletonCard key={index} />
        ))}
      </div>
    </div>
  );
};

export { SpinnerCardComponent };
