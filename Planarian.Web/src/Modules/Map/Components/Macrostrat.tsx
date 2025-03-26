import React, { useState, useEffect } from "react";
import {
  Collapse,
  Descriptions,
  List,
  Spin,
  Typography,
  Row,
  Col,
  Alert,
  Tag,
  Grid,
} from "antd";
import { defaultIfEmpty } from "../../../Shared/Helpers/StringHelpers";

const { Panel } = Collapse;
const { Text, Paragraph } = Typography;
const { useBreakpoint } = Grid;

interface MacrostratResponse {
  success: {
    data: {
      elevation: number;
      mapData: any[];
      regions: any[];
      hasColumns: boolean;
    };
  };
}

interface XDDResponse {
  success: {
    data: any[];
  };
}

interface MacrostratProps {
  lat: number;
  lng: number;
}

interface ExpandableTextProps {
  text: string;
  maxChars?: number;
}

/**
 * ExpandableText splits the text into paragraphs (by newline).
 * If the total character count exceeds maxChars, it shows a truncated
 * version along with a "Show more" link. When expanded, it shows all paragraphs
 * with a "Show less" link.
 */
const ExpandableText: React.FC<ExpandableTextProps> = ({
  text,
  maxChars = 1000,
}) => {
  const [expanded, setExpanded] = useState(false);
  const paragraphs = text
    .split("\\n")
    .map((line) => line.trim())
    .filter((line) => line.length > 0);

  const totalChars = paragraphs.reduce((acc, para) => acc + para.length, 0);

  if (totalChars <= maxChars) {
    return (
      <>
        {paragraphs.map((para, idx) => (
          <Paragraph key={idx}>{para}</Paragraph>
        ))}
      </>
    );
  }

  if (!expanded) {
    let currentLength = 0;
    const truncatedParagraphs: string[] = [];
    for (const para of paragraphs) {
      if (currentLength + para.length <= maxChars) {
        truncatedParagraphs.push(para);
        currentLength += para.length;
      } else {
        const remaining = maxChars - currentLength;
        if (remaining > 0) {
          truncatedParagraphs.push(para.substring(0, remaining) + "...");
        }
        break;
      }
    }
    return (
      <>
        {truncatedParagraphs.map((para, idx) => (
          <Paragraph key={idx}>{para}</Paragraph>
        ))}
        <a onClick={() => setExpanded(true)}>Show more</a>
      </>
    );
  }

  return (
    <>
      {paragraphs.map((para, idx) => (
        <Paragraph key={idx}>{para}</Paragraph>
      ))}
      <a onClick={() => setExpanded(false)}>Show less</a>
    </>
  );
};

const Macrostrat: React.FC<MacrostratProps> = ({ lat, lng }) => {
  const screens = useBreakpoint();
  const descriptionLayout = screens.md ? "horizontal" : "vertical";

  const [macroData, setMacroData] = useState<MacrostratResponse | null>(null);
  const [xddData, setXddData] = useState<XDDResponse | null>(null);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  const isColorLight = (color: string): boolean => {
    if (!color || color[0] !== "#") return false;
    const r = parseInt(color.substr(1, 2), 16);
    const g = parseInt(color.substr(3, 2), 16);
    const b = parseInt(color.substr(5, 2), 16);
    // Calculate brightness per ITU-R BT.601
    const brightness = (r * 299 + g * 587 + b * 114) / 1000;
    return brightness > 200;
  };

  const getTagStyle = (color?: string) => {
    return color && isColorLight(color)
      ? {
          border: "1px solid rgba(0,0,0,0.2)",
          color: "rgba(0,0,0,0.7)",
        }
      : {};
  };

  const highlightCSS = `
    .hl {
      background-color: #C4EEB2;
      font-weight: 500;
      padding: 0 2px;
    }
  `;

  const fetchMacrostratData = async () => {
    try {
      setLoading(true);
      setError(null);
      const macrostratUrl = `https://macrostrat.org/api/v2/mobile/map_query_v2?lng=${lng}&lat=${lat}&z=9`;
      const resp = await fetch(macrostratUrl);
      if (!resp.ok) {
        throw new Error(`Macrostrat fetch error: ${resp.statusText}`);
      }
      const data = (await resp.json()) as MacrostratResponse;
      setMacroData(data);
    } catch (err: any) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const fetchXDDData = async (term: string) => {
    try {
      setLoading(true);
      setError(null);
      const queryTerm = encodeURIComponent(term);
      const xddUrl = `https://xdd.wisc.edu/api/v1/snippets?article_limit=20&term=${queryTerm}`;
      const resp = await fetch(xddUrl);
      if (!resp.ok) {
        throw new Error(`XDD fetch error: ${resp.statusText}`);
      }
      const data = (await resp.json()) as XDDResponse;
      setXddData(data);
    } catch (err: any) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchMacrostratData();
  }, [lat, lng]);

  useEffect(() => {
    if (macroData) {
      const firstMap = macroData.success.data.mapData[0];
      if (firstMap?.strat_name) {
        fetchXDDData(firstMap.strat_name);
      }
    }
  }, [macroData]);

  if (loading) {
    return (
      <div style={{ textAlign: "center", marginTop: 50 }}>
        <Spin tip="Loading data..." />
      </div>
    );
  }

  if (error) {
    return (
      <Alert
        message="Error fetching data"
        description={error}
        type="error"
        showIcon
      />
    );
  }

  if (!macroData) {
    return null;
  }

  const { mapData = [], regions = [] } = macroData.success.data;
  const firstMap = mapData[0] || {};

  const {
    name,
    age,
    strat_name,
    lith, // e.g., "Major:{limestone}"
    descrip,
    comments,
    liths: mainLiths = [],
    color, // main color for the map object
    b_int,
    t_int,
    ref, // source for the geologic map
  } = firstMap;

  // Macrostrat-specific fields
  const {
    strat_names = [],
    max_thick,
    min_min_thick,
    b_age,
    t_age,
    liths: macroLiths = [],
    environs = [],
  } = firstMap.macrostrat || {};

  const descriptionText = descrip ? descrip.trim() : "";
  const commentsText = comments ? comments.trim() : "";

  // Compute ranges for macrostrat age and thickness (if available)
  const macroAgeRange = b_age && t_age ? `${b_age}–${t_age} Ma` : "N/A";
  const thicknessRange =
    typeof min_min_thick === "number" && typeof max_thick === "number"
      ? `${min_min_thick} – ${max_thick} m`
      : defaultIfEmpty("");

  const xddSnippets = xddData?.success.data || [];

  return (
    <div>
      <style>{highlightCSS}</style>
      <Row gutter={[16, 16]}>
        <Col span={24}>
          <Collapse defaultActiveKey={["1"]}>
            {/* Geologic map panel */}
            <Panel header="Geologic map" key="1">
              <Descriptions
                layout={descriptionLayout}
                bordered
                column={1}
                size="small"
              >
                <Descriptions.Item label="Name">{name}</Descriptions.Item>
                <Descriptions.Item label="Age">
                  <Tag color={color || "default"} style={getTagStyle(color)}>
                    {age}
                  </Tag>
                </Descriptions.Item>
                <Descriptions.Item label="Interval">
                  {b_int && t_int ? (
                    <Tag color={b_int.color} style={getTagStyle(b_int.color)}>
                      {b_int.int_name} ({b_int.b_age}–{t_int.t_age} Ma)
                    </Tag>
                  ) : (
                    defaultIfEmpty("")
                  )}
                </Descriptions.Item>
                <Descriptions.Item label="Stratigraphic Name(s)">
                  {strat_name}
                </Descriptions.Item>
                <Descriptions.Item label="Lith">{lith}</Descriptions.Item>
                <Descriptions.Item label="Lithology Details">
                  {mainLiths.length > 0
                    ? mainLiths.map((l: any, idx: number) => (
                        <Tag
                          key={idx}
                          color={l.color || "default"}
                          style={getTagStyle(l.color)}
                        >
                          {l.lith} ({l.lith_type})
                        </Tag>
                      ))
                    : defaultIfEmpty("")}
                </Descriptions.Item>
                <Descriptions.Item label="Description">
                  <ExpandableText text={descriptionText} maxChars={300} />
                </Descriptions.Item>
                <Descriptions.Item label="Comments">
                  <ExpandableText text={commentsText} maxChars={300} />
                </Descriptions.Item>
                <Descriptions.Item label="Source">
                  {ref ? (
                    <a href={ref.url} target="_blank" rel="noreferrer">
                      {ref.name} ({ref.ref_source}, {ref.ref_year})
                    </a>
                  ) : (
                    defaultIfEmpty("")
                  )}
                </Descriptions.Item>
              </Descriptions>
            </Panel>

            {/* Macrostrat-linked data panel */}
            <Panel header="Macrostrat-linked data" key="2">
              <Descriptions
                layout={descriptionLayout}
                bordered
                column={1}
                size="small"
              >
                <Descriptions.Item label="Matched Stratigraphic Unit">
                  {strat_names.map((sn: any) => sn.rank_name).join(", ")}
                </Descriptions.Item>
                <Descriptions.Item label="Age (Macrostrat)">
                  <Tag
                    color={(b_int && b_int.color) || "default"}
                    style={getTagStyle(b_int && b_int.color)}
                  >
                    {macroAgeRange}
                  </Tag>
                </Descriptions.Item>
                <Descriptions.Item label="Thickness">
                  {thicknessRange}
                </Descriptions.Item>
                <Descriptions.Item label="Base Interval">
                  {b_int ? (
                    <Tag color={b_int.color} style={getTagStyle(b_int.color)}>
                      {b_int.int_name}
                    </Tag>
                  ) : (
                    defaultIfEmpty("")
                  )}
                </Descriptions.Item>
                <Descriptions.Item label="Top Interval">
                  {t_int ? (
                    <Tag color={t_int.color} style={getTagStyle(t_int.color)}>
                      {t_int.int_name}
                    </Tag>
                  ) : (
                    defaultIfEmpty("")
                  )}
                </Descriptions.Item>
                <Descriptions.Item label="Lithology (parsed)">
                  {macroLiths.length > 0
                    ? macroLiths.map((l: any, idx: number) => (
                        <Tag
                          key={idx}
                          color={l.color || "default"}
                          style={getTagStyle(l.color)}
                        >
                          {l.lith} ({l.lith_type})
                        </Tag>
                      ))
                    : defaultIfEmpty("")}
                </Descriptions.Item>
                <Descriptions.Item label="Environments">
                  {environs.length > 0
                    ? environs.map((env: any, idx: number) => (
                        <Tag
                          key={idx}
                          color={env.color || "default"}
                          style={getTagStyle(env.color)}
                        >
                          {env.environ}
                        </Tag>
                      ))
                    : defaultIfEmpty("")}
                </Descriptions.Item>
              </Descriptions>
            </Panel>

            {/* Primary Literature panel */}
            <Panel header="Primary Literature" key="3">
              {xddSnippets.length === 0 ? (
                <Text>No references found for "{strat_name}"</Text>
              ) : (
                <Collapse accordion>
                  {xddSnippets.map((snippet: any, idx: number) => (
                    <Panel
                      header={
                        <span>
                          {snippet.title} <br />
                          <Text type="secondary">{snippet.pubname}</Text>
                        </span>
                      }
                      key={idx}
                    >
                      <Descriptions
                        layout={descriptionLayout}
                        column={1}
                        size="small"
                        bordered
                      >
                        <Descriptions.Item label="Title">
                          {snippet.title}
                        </Descriptions.Item>
                        <Descriptions.Item label="Authors">
                          {snippet.authors}
                        </Descriptions.Item>
                        <Descriptions.Item label="DOI">
                          <a
                            href={
                              snippet.doi
                                ? `https://doi.org/${snippet.doi}`
                                : "#"
                            }
                            target="_blank"
                            rel="noreferrer"
                          >
                            {snippet.doi || "N/A"}
                          </a>
                        </Descriptions.Item>
                        <Descriptions.Item label="Highlights">
                          <List
                            dataSource={snippet.highlight}
                            renderItem={(highlight: string, hIdx) => (
                              <List.Item key={hIdx}>
                                <span
                                  dangerouslySetInnerHTML={{
                                    __html: highlight,
                                  }}
                                />
                              </List.Item>
                            )}
                          />
                        </Descriptions.Item>
                      </Descriptions>
                    </Panel>
                  ))}
                </Collapse>
              )}
            </Panel>

            {/* Regions panel */}
            {regions.length > 0 && (
              <Panel header="Physiography / Regions" key="4">
                <Collapse>
                  {regions.map((region: any, index: number) => (
                    <Panel header={region.name} key={index}>
                      <Descriptions
                        layout={descriptionLayout}
                        bordered
                        column={1}
                        size="small"
                      >
                        <Descriptions.Item label="Group">
                          {region.boundary_group}
                        </Descriptions.Item>
                        <Descriptions.Item label="Type">
                          {region.boundary_type}
                        </Descriptions.Item>
                        <Descriptions.Item label="Class">
                          {region.boundary_class}
                        </Descriptions.Item>
                        <Descriptions.Item label="Description">
                          {region.descrip}
                        </Descriptions.Item>
                        {region.wiki_link && (
                          <Descriptions.Item label="Wiki Link">
                            <a
                              href={region.wiki_link}
                              target="_blank"
                              rel="noreferrer"
                            >
                              {region.wiki_link}
                            </a>
                          </Descriptions.Item>
                        )}
                      </Descriptions>
                    </Panel>
                  ))}
                </Collapse>
              </Panel>
            )}
          </Collapse>
        </Col>
      </Row>
    </div>
  );
};

export { Macrostrat };
