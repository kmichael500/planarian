import { Table } from "antd";
import Papa from "papaparse";
import React from "react";

interface CSVDisplayProps {
  data: string;
}

type TableRow = {
  [key: string]: string;
};

const CSVDisplay: React.FC<CSVDisplayProps> = ({ data }) => {
  const parsedData = Papa.parse<TableRow>(data, { header: true }).data;

  const columns =
    parsedData.length > 0
      ? Object.keys(parsedData[0]).map((key) => {
          const uniqueValues = Array.from(
            new Set(parsedData.map((row) => row[key]))
          ).filter((val) => val !== undefined && val !== null);
          const filters = uniqueValues.map((value) => ({
            text: value,
            value: value,
          }));

          return {
            title: key,
            dataIndex: key,
            className: "truncate",
            sorter: (a: TableRow, b: TableRow) => {
              if (a[key] < b[key]) return -1;
              if (a[key] > b[key]) return 1;
              return 0;
            },
            filters: filters,
            onFilter: (value: string | number | boolean, record: TableRow) =>
              record[key]?.toString().includes(value.toString()),
          };
        })
      : [];

  return (
    <div style={{ flexWrap: "wrap", width: "100%" }}>
      <Table
        dataSource={parsedData}
        scroll={{ x: "max-content" }}
        columns={columns}
      />
    </div>
  );
};

export { CSVDisplay };
