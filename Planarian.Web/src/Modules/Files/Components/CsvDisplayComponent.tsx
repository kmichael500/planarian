import React, { useEffect, useRef, useState } from "react";
import Papa from "papaparse";
import {
  GridComponent,
  ColumnsDirective,
  ColumnDirective,
  Inject,
  Toolbar,
  Page,
  Sort,
  Filter,
  Search,
  VirtualScroll,
  Resize,
  FilterSettingsModel,
} from "@syncfusion/ej2-react-grids";

interface CSVDisplayProps {
  data?: string | null;
}

type TableRow = {
  [key: string]: string;
};

const CSVDisplay: React.FC<CSVDisplayProps> = ({ data }) => {
  const gridRef = useRef<GridComponent>(null);
  const [parsedData, setParsedData] = useState<TableRow[]>([]);
  const [columnKeys, setColumnKeys] = useState<string[]>([]);
  const [isLoaded, setIsLoaded] = useState(false);

  // Set filter settings (using Excel type for enhanced filtering UI)
  const filterSettings: FilterSettingsModel = { type: "Excel" };

  useEffect(() => {
    // Parse CSV data after component mount
    if (data) {
      const result = Papa.parse<TableRow>(data, { header: true });
      const parsed = result.data;
      setParsedData(parsed);

      // Extract column keys from the first row
      const keys = parsed && parsed.length > 0 ? Object.keys(parsed[0]) : [];
      setColumnKeys(keys);

      // Set component as loaded
      setIsLoaded(true);
    }
  }, [data]);

  return (
    <>
      {isLoaded && (
        <GridComponent
          ref={gridRef}
          height="100%"
          dataSource={parsedData}
          allowSorting={true}
          allowFiltering={true}
          filterSettings={filterSettings}
          allowPaging={true}
          toolbar={["Search"]}
          pageSettings={{ pageSize: 100 }}
          allowResizing={true}
          dataBound={() => {
            if (gridRef.current) {
              gridRef.current.autoFitColumns();
            }
          }}
        >
          <ColumnsDirective>
            {columnKeys.map((key) => (
              <ColumnDirective
                key={key}
                field={key}
                headerText={key}
                allowSorting={true}
                allowFiltering={true}
              />
            ))}
          </ColumnsDirective>
          <Inject
            services={[
              Toolbar,
              Page,
              Sort,
              Filter,
              Search,
              VirtualScroll,
              Resize,
            ]}
          />
        </GridComponent>
      )}
    </>
  );
};

export { CSVDisplay };
