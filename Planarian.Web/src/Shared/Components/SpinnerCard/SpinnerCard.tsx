import "./SpinnerCard.scss";

interface SpinnerCardProps {
  children?: React.ReactNode;
  className?: string;
  spinning: boolean;
}

type PlaceholderCard = {
  accentClass: string;
  duration: string;
  delay: string;
  pulseScale: string;
  pulseOpacity: string;
  pulseBrightness: string;
};

const PLACEHOLDER_CARDS: PlaceholderCard[] = [
  {
    accentClass: "planarian-spinner-card__placeholder-card--rose",
    duration: "1.2s",
    delay: "-0.8s",
    pulseScale: "1.05",
    pulseOpacity: "0.72",
    pulseBrightness: "1.08",
  },
  {
    accentClass: "planarian-spinner-card__placeholder-card--amber",
    duration: "1.35s",
    delay: "-0.3s",
    pulseScale: "1.07",
    pulseOpacity: "0.7",
    pulseBrightness: "1.09",
  },
  {
    accentClass: "planarian-spinner-card__placeholder-card--leaf",
    duration: "1.4s",
    delay: "-1.1s",
    pulseScale: "1.06",
    pulseOpacity: "0.71",
    pulseBrightness: "1.08",
  },
  {
    accentClass: "planarian-spinner-card__placeholder-card--aqua",
    duration: "1.25s",
    delay: "-0.5s",
    pulseScale: "1.08",
    pulseOpacity: "0.68",
    pulseBrightness: "1.1",
  },
  {
    accentClass: "planarian-spinner-card__placeholder-card--violet",
    duration: "1.3s",
    delay: "-0.9s",
    pulseScale: "1.05",
    pulseOpacity: "0.72",
    pulseBrightness: "1.08",
  },
  {
    accentClass: "planarian-spinner-card__placeholder-card--coral",
    duration: "1.15s",
    delay: "-0.2s",
    pulseScale: "1.07",
    pulseOpacity: "0.69",
    pulseBrightness: "1.09",
  },
  {
    accentClass: "planarian-spinner-card__placeholder-card--mint",
    duration: "1.45s",
    delay: "-1.3s",
    pulseScale: "1.06",
    pulseOpacity: "0.7",
    pulseBrightness: "1.08",
  },
  {
    accentClass: "planarian-spinner-card__placeholder-card--indigo",
    duration: "1.25s",
    delay: "-0.6s",
    pulseScale: "1.08",
    pulseOpacity: "0.68",
    pulseBrightness: "1.1",
  },
];

const SpinnerCardComponent: React.FC<SpinnerCardProps> = ({
  spinning,
  children,
  className,
}) => {
  if (!spinning) {
    return <>{children}</>;
  }

  const classNames = ["planarian-spinner-card", className]
    .filter(Boolean)
    .join(" ");

  return (
    <div className={classNames}>
      <div className="planarian-spinner-card__surface">
        <div className="planarian-spinner-card__grid" aria-hidden="true">
          {PLACEHOLDER_CARDS.map((card, index) => (
            <div
              key={`${card.accentClass}-${index}`}
              className={[
                "planarian-spinner-card__placeholder-card",
                card.accentClass,
              ].join(" ")}
            >
              <div className="planarian-spinner-card__placeholder-header">
                <div
                  className="planarian-spinner-card__pulse planarian-spinner-card__placeholder-title"
                  style={
                    {
                      "--planarian-spinner-duration": card.duration,
                      "--planarian-spinner-delay": card.delay,
                      "--planarian-spinner-pulse-scale": card.pulseScale,
                      "--planarian-spinner-pulse-opacity": card.pulseOpacity,
                      "--planarian-spinner-pulse-brightness":
                        card.pulseBrightness,
                    } as React.CSSProperties
                  }
                />
                <div
                  className="planarian-spinner-card__pulse planarian-spinner-card__placeholder-badge"
                  style={
                    {
                      "--planarian-spinner-duration": card.duration,
                      "--planarian-spinner-delay": card.delay,
                      "--planarian-spinner-pulse-scale": card.pulseScale,
                      "--planarian-spinner-pulse-opacity": card.pulseOpacity,
                      "--planarian-spinner-pulse-brightness":
                        card.pulseBrightness,
                    } as React.CSSProperties
                  }
                />
              </div>

              <div className="planarian-spinner-card__placeholder-body">
                <div
                  className="planarian-spinner-card__pulse planarian-spinner-card__placeholder-line planarian-spinner-card__placeholder-line--strong"
                  style={
                    {
                      "--planarian-spinner-duration": card.duration,
                      "--planarian-spinner-delay": card.delay,
                      "--planarian-spinner-pulse-scale": card.pulseScale,
                      "--planarian-spinner-pulse-opacity": card.pulseOpacity,
                      "--planarian-spinner-pulse-brightness":
                        card.pulseBrightness,
                    } as React.CSSProperties
                  }
                />
                <div
                  className="planarian-spinner-card__pulse planarian-spinner-card__placeholder-line"
                  style={
                    {
                      "--planarian-spinner-duration": card.duration,
                      "--planarian-spinner-delay": card.delay,
                      "--planarian-spinner-pulse-scale": card.pulseScale,
                      "--planarian-spinner-pulse-opacity": card.pulseOpacity,
                      "--planarian-spinner-pulse-brightness":
                        card.pulseBrightness,
                    } as React.CSSProperties
                  }
                />
                <div className="planarian-spinner-card__placeholder-tags">
                  <div
                    className="planarian-spinner-card__pulse planarian-spinner-card__placeholder-tag"
                    style={
                      {
                        "--planarian-spinner-duration": card.duration,
                        "--planarian-spinner-delay": card.delay,
                        "--planarian-spinner-pulse-scale": card.pulseScale,
                        "--planarian-spinner-pulse-opacity":
                          card.pulseOpacity,
                        "--planarian-spinner-pulse-brightness":
                          card.pulseBrightness,
                      } as React.CSSProperties
                    }
                  />
                  <div
                    className="planarian-spinner-card__pulse planarian-spinner-card__placeholder-tag planarian-spinner-card__placeholder-tag--short"
                    style={
                      {
                        "--planarian-spinner-duration": card.duration,
                        "--planarian-spinner-delay": card.delay,
                        "--planarian-spinner-pulse-scale": card.pulseScale,
                        "--planarian-spinner-pulse-opacity":
                          card.pulseOpacity,
                        "--planarian-spinner-pulse-brightness":
                          card.pulseBrightness,
                      } as React.CSSProperties
                    }
                  />
                </div>
              </div>

              <div className="planarian-spinner-card__placeholder-footer">
                <div
                  className="planarian-spinner-card__pulse planarian-spinner-card__placeholder-button planarian-spinner-card__placeholder-button--primary"
                  style={
                    {
                      "--planarian-spinner-duration": card.duration,
                      "--planarian-spinner-delay": card.delay,
                      "--planarian-spinner-pulse-scale": card.pulseScale,
                      "--planarian-spinner-pulse-opacity": card.pulseOpacity,
                      "--planarian-spinner-pulse-brightness":
                        card.pulseBrightness,
                    } as React.CSSProperties
                  }
                />
                <div
                  className="planarian-spinner-card__pulse planarian-spinner-card__placeholder-button"
                  style={
                    {
                      "--planarian-spinner-duration": card.duration,
                      "--planarian-spinner-delay": card.delay,
                      "--planarian-spinner-pulse-scale": card.pulseScale,
                      "--planarian-spinner-pulse-opacity": card.pulseOpacity,
                      "--planarian-spinner-pulse-brightness":
                        card.pulseBrightness,
                    } as React.CSSProperties
                  }
                />
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
};

export { SpinnerCardComponent };
