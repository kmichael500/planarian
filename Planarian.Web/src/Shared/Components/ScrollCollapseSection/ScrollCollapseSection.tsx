import React from "react";
import "./ScrollCollapseSection.scss";

interface ScrollCollapseSectionProps {
  children?: React.ReactNode;
  className?: string;
  visible: boolean;
}

const ScrollCollapseSection: React.FC<ScrollCollapseSectionProps> = ({
  children,
  className,
  visible,
}) => {
  const classNames = [
    "planarian-scroll-collapse-section",
    visible
      ? "planarian-scroll-collapse-section--visible"
      : "planarian-scroll-collapse-section--hidden",
    className,
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <div className={classNames}>
      <div className="planarian-scroll-collapse-section__inner">{children}</div>
    </div>
  );
};

export { ScrollCollapseSection };
