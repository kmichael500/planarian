import { Button, Col, Input, Row } from "antd";
import {
  QueryBuilder,
  QueryOperator,
} from "../../Search/Services/QueryBuilder";
import { TripVm } from "../Models/TripVm";
interface TripSearchComponentProps {
  onSearch: (queryString: string) => void;
  queryBuilder: QueryBuilder<TripVm>;
}
const TripSearchComponent: React.FC<TripSearchComponentProps> = (props) => {
  const onSearch = (value: string) => {
    props.queryBuilder.filterBy("name", QueryOperator.Contains, value);

    props.onSearch(props.queryBuilder.buildAsQueryString());
  };
  return (
    <Row style={{ marginBottom: 10 }} gutter={5}>
      <Col>
        <Input.Search onSearch={onSearch} />
      </Col>
      <Col>
        <Button>Advanced Search</Button>
      </Col>
    </Row>
  );
};

export { TripSearchComponent };
