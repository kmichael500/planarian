import { Table } from "antd";
import Papa from "papaparse";

interface CSVDisplayProps {
  data: string;
}

type TableRow = {
  [key: string]: string;
};

const CSVDisplay: React.FC<CSVDisplayProps> = ({ data }) => {
  const parsedData = Papa.parse<TableRow>(data, { header: true }).data;

  return (
    <Table
      dataSource={parsedData}
      scroll={{ x: 1500 }}
      columns={
        parsedData.length > 0
          ? Object.keys(parsedData[0]).map((key) => ({
              title: key,
              dataIndex: key,
              className: "truncate",
            }))
          : []
      }
    />
  );
};

export { CSVDisplay };
