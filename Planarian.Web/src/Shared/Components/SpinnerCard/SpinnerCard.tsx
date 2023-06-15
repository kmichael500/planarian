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
          items={[...Array(numberOfCards).keys()].map((index) => ({
            id: index,
          }))}
          itemKey={(index) => index.toString()}
          renderItem={(index) => (
            <Card loading={spinning} className={className} {...props} />
          )}
        />
      )}
      {!spinning && children}
    </>
  );
};

export { SpinnerCardComponent };
