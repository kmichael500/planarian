import { Row, Col, Card } from "antd";
import Paragraph from "antd/lib/skeleton/Paragraph";
import { Link } from "react-router-dom";
interface CardGridComponentProps {
  items: any[] | undefined;
}
const CardGridComponent: React.FC<CardGridComponentProps> = ({ items }) => {
  return (
    <Row
      gutter={[
        { xs: 8, sm: 8, md: 24, lg: 32 },
        { xs: 8, sm: 8, md: 24, lg: 32 },
      ]}
    >
      {items?.map((item) => {
        return (
          <>
            <Col xs={24} sm={12} md={8} lg={6}>
              {item}
            </Col>
          </>
        );
      })}
    </Row>
  );
};

export { CardGridComponent };
