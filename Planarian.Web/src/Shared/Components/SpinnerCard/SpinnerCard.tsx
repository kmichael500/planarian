import { Card } from "antd";
import { CardGridComponent } from "../CardGrid/CardGridComponent";

interface SpinnerCardProps {
  children?: React.ReactNode;
  className?: string;
  spinning: boolean;
  numberOfCards?: number;
}

const SpinnerCardComponent: React.FC<SpinnerCardProps> = ({
  spinning,
  children,
  className,
  numberOfCards = 8,
  ...props
}) => {
  return (
    <>
      {spinning && (
        <CardGridComponent
          items={[...Array(numberOfCards).keys()].map((_, index) => {
            return {
              item: (
                <Card loading={spinning} className={className} {...props} />
              ),
              key: index,
            };
          })}
        />
      )}
      {!spinning && children}
    </>
  );
};

export { SpinnerCardComponent };
