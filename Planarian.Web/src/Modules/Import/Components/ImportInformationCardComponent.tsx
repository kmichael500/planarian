import { Card, Typography, Space, Tooltip, Row, Col } from "antd";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import {
  downloadFile,
  createCsvWithHeaders,
} from "../../../Shared/Helpers/FileHelpers";
import { CloudDownloadOutlined } from "@ant-design/icons";

const { Title, Paragraph } = Typography;

interface ImportData {
  description: string;
  example: string;
  isRequired: boolean;
}

const caveImportHeadersData = [
  // Identification
  {
    display: "Cave Name",
    value: "CaveName",
    data: {
      description: "The name of the cave.",
      example: "Crystal Palace Cave",
      isRequired: true,
    },
  },
  {
    display: "County Cave Number",
    value: "CountyCaveNumber",
    data: {
      description:
        "The county cave number of the cave. This is unique per county.",
      example: "1",
      isRequired: true,
    },
  },

  // Location
  {
    display: "State",
    value: "State",
    data: {
      description: "The state of the cave.",
      example: "TN",
      isRequired: true,
    },
  },
  {
    display: "County Code",
    value: "CountyCode",
    data: {
      description:
        "The county code of the cave. This is unique per county and state.",
      example: "PR",
      isRequired: true,
    },
  },
  {
    display: "County Name",
    value: "CountyName",
    data: {
      description: "The county name of the cave.",
      example: "Perry County",
      isRequired: true,
    },
  },

  // Characteristics
  {
    display: "Cave Length",
    value: "CaveLengthFt",
    data: {
      description: "The length of the cave in feet.",
      example: "1000",
      isRequired: true,
    },
  },
  {
    display: "Cave Depth",
    value: "CaveDepthFt",
    data: {
      description: "The depth of the cave in feet.",
      example: "200",
      isRequired: true,
    },
  },
  {
    display: "Max Pit Depth",
    value: "MaxPitDepthFt",
    data: {
      description: "The deepest pit in the cave in feet.",
      example: "200",
      isRequired: true,
    },
  },
  {
    display: "Number of Pits",
    value: "NumberOfPits",
    data: {
      description: "The number of pits in the cave.",
      example: "2",
      isRequired: true,
    },
  },
  {
    display: "Geology",
    value: "Geology",
    data: {
      description:
        "The geological formation that the cave is in. This is a comma-separated list.",
      example: "Monteagle, Bangor",
      isRequired: false,
    },
  },

  // Reporting
  {
    display: "Reported On Date",
    value: "ReportedOnDate",
    data: {
      description: "The date the cave was reported on.",
      example: "1998-01-01",
      isRequired: false,
    },
  },
  {
    display: "Reported By Name",
    value: "ReportedByName",
    data: {
      description: "The name of the person who reported the cave.",
      example: "John Doe",
      isRequired: false,
    },
  },

  // Miscellaneous
  {
    display: "Narrative",
    value: "Narrative",
    data: {
      description: "A description of the cave.",
      example: "This is a cave",
      isRequired: false,
    },
  },
  {
    display: "Is Archived",
    value: "IsArchived",
    data: {
      description:
        "Whether or not the cave is archived. If archived, it will not show up in the main cave list.",
      example: "false",
      isRequired: false,
    },
  },
];

const entranceImportHeadersData = [
  // Identification
  {
    display: "County Code",
    value: "CountyCode",
    data: {
      description: "This must match the County Code in the Cave CSV file",
      example: "PR",
      isRequired: true,
    },
  },
  {
    display: "County Cave Number",
    value: "CountyCaveNumber",
    data: {
      description:
        "This must match the County Cave Number in the Cave CSV file.",
      example: "1",
      isRequired: true,
    },
  },
  {
    display: "Entrance Name",
    value: "EntranceName",
    data: {
      description: "The name of the entrance.",
      example: "Main Entrance",
      isRequired: false,
    },
  },

  // Location

  {
    display: "Decimal Latitude",
    value: "DecimalLatitude",
    data: {
      description: "The latitude of the entrance in WG84.",
      example: "35.12346789",
      isRequired: true,
    },
  },
  {
    display: "Decimal Longitude",
    value: "DecimalLongitude",
    data: {
      description: "The longitude of the entrance in WG84.",
      example: "-85.123456789",
      isRequired: true,
    },
  },
  {
    display: "Entrance Elevation",
    value: "EntranceElevationFt",
    data: {
      description: "The elevation of the entrance in ft.",
      example: "1000",
      isRequired: true,
    },
  },
  {
    display: "Location Quality",
    value: "LocationQuality",
    data: {
      description: "The quality of the entrance location.",
      example: "Field Confirmed",
      isRequired: true,
    },
  },

  // Characteristics
  {
    display: "Entrance Description",
    value: "EntranceDescription",
    data: {
      description: "A description of the entrance.",
      example: "This is the main entrance.",
      isRequired: false,
    },
  },
  {
    display: "Entrance Pit Depth",
    value: "EntrancePitDepth",
    data: {
      description: "The pit depth of the entrance in ft.",
      example: "20",
      isRequired: false,
    },
  },
  {
    display: "Entrance Status",
    value: "EntranceStatus",
    data: {
      description: "The status of the entrance.",
      example: "Permission Needed",
      isRequired: false,
    },
  },
  {
    display: "Entrance Hydrology",
    value: "EntranceHydrology",
    data: {
      description: "The hydrology of the entrance.",
      example: "Karst Window",
      isRequired: false,
    },
  },
  {
    display: "Entrance Hydrology Frequency",
    value: "EntranceHydrologyFrequency",
    data: {
      description: "The hydrology frequency of the entrance.",
      example: "Perennial",
      isRequired: false,
    },
  },
  {
    display: "Field Indication",
    value: "FieldIndication",
    data: {
      description: "The field indication of the entrance.",
      example: "Bluff",
      isRequired: false,
    },
  },

  // Reporting
  {
    display: "Reported On Date",
    value: "ReportedOnDate",
    data: {
      description: "The date the entrance was reported on.",
      example: "2000-01-01",
      isRequired: false,
    },
  },
  {
    display: "Reported By Name",
    value: "ReportedByName",
    data: {
      description: "The name of the person who reported the entrance.",
      example: "John Doe",
      isRequired: false,
    },
  },

  // Miscellaneous
  {
    display: "Is Primary Entrance",
    value: "IsPrimaryEntrance",
    data: {
      description:
        "Whether or not the entrance is the primary entrance. Only one entrance can be primary.",
      example: "true",
      isRequired: true,
    },
  },
];

const ImportInformationCardComponent = () => {
  return (
    <>
      <Card
        bordered={false}
        style={{
          // boxShadow: "0px 4px 12px rgba(0, 0, 0, 0.1)",
          // borderRadius: "8px",
          height: "100%",
        }}
      >
        <Typography>
          <Title level={4}>Templates</Title>
          <Paragraph>
            You will need to split your data into two files. A file containing
            entrnace data and a file containing cave data. You will find a
            sample template to download for each file as well as descriptions
            and examples (on hover) . The two files will be merged using the
            county code and county cave number. The cave record must exist
            before an entrance record can be imported.
          </Paragraph>
          <Row gutter={10}>
            <Col>
              <Title level={4}>Cave Data File</Title>
            </Col>
            <Col>
              <PlanarianButton
                icon={<CloudDownloadOutlined />}
                onClick={() => {
                  const caveHeaders = caveImportHeadersData.map(
                    (item) => item.value
                  );
                  downloadFile(
                    "cave_import.csv",
                    createCsvWithHeaders(caveHeaders)
                  );
                }}
              ></PlanarianButton>
            </Col>
          </Row>

          <Paragraph>
            <b>Required Columns:</b>{" "}
            <Space wrap>
              {caveImportHeadersData
                .filter((item) => item.data.isRequired)
                .map((item) => (
                  <Tooltip
                    title={
                      <>
                        <b>Description:</b> {item.data.description}
                        <br />
                        <b>Example:</b> {item.data.example}
                      </>
                    }
                    key={item.value}
                  >
                    <span>
                      <code style={{ whiteSpace: "normal" }}>{item.value}</code>
                    </span>
                  </Tooltip>
                ))}
            </Space>
          </Paragraph>
          <Paragraph>
            <b>Optional Columns:</b>{" "}
            <Space wrap>
              {caveImportHeadersData
                .filter((item) => !item.data.isRequired)
                .map((item) => (
                  <Tooltip
                    title={
                      <>
                        <b>Description:</b> {item.data.description}
                        <br />
                        <b>Example:</b> {item.data.example}
                      </>
                    }
                    key={item.value}
                  >
                    <span>
                      <code style={{ whiteSpace: "normal" }}>{item.value}</code>
                    </span>
                  </Tooltip>
                ))}
            </Space>
          </Paragraph>

          <Row gutter={10}>
            <Col>
              <Title level={4}>Cave Entrance File</Title>
            </Col>
            <Col>
              <PlanarianButton
                icon={<CloudDownloadOutlined />}
                onClick={() => {
                  const entranceHeaders = entranceImportHeadersData.map(
                    (item) => item.value
                  );
                  downloadFile(
                    "entrance_import.csv",
                    createCsvWithHeaders(entranceHeaders)
                  );
                }}
              ></PlanarianButton>
            </Col>
          </Row>
          <Paragraph>
            <b>Required Columns:</b>{" "}
            <Space wrap>
              {entranceImportHeadersData
                .filter((item) => item.data.isRequired)
                .map((item) => (
                  <Tooltip
                    title={
                      <>
                        <b>Description:</b> {item.data.description}
                        <br />
                        <b>Example:</b> {item.data.example}
                      </>
                    }
                    key={item.value}
                  >
                    <span>
                      <code style={{ whiteSpace: "normal" }}>{item.value}</code>
                    </span>
                  </Tooltip>
                ))}
            </Space>
          </Paragraph>
          <Paragraph>
            <b>Optional Columns:</b>{" "}
            <Space wrap>
              {entranceImportHeadersData
                .filter((item) => !item.data.isRequired)
                .map((item) => (
                  <Tooltip
                    title={
                      <>
                        <b>Description:</b> {item.data.description}
                        <br />
                        <b>Example:</b> {item.data.example}
                      </>
                    }
                    key={item.value}
                  >
                    <span>
                      <code style={{ whiteSpace: "normal" }}>{item.value}</code>
                    </span>
                  </Tooltip>
                ))}
            </Space>
          </Paragraph>

          <Paragraph>
            <span style={{ fontWeight: "bold" }}>Note:</span> Please remain
            patient during the upload (particularly with large datasets). The
            upload and processing might take a bit and it's crucial that you do
            not refresh the page or navigate away during this time. If any
            errors occur during the upload, the process will fail and provide
            reasons for each record's failure, and data will not be saved.
          </Paragraph>
        </Typography>
      </Card>
    </>
  );
};

export { ImportInformationCardComponent };
