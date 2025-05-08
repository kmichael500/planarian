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
  Checkbox,
} from "antd";
import { defaultIfEmpty } from "../../../Shared/Helpers/StringHelpers";
import { MapService, GeologicMapResult } from "../Services/MapService";

const { Panel } = Collapse;
const { Text, Paragraph } = Typography;
const { useBreakpoint } = Grid;

const allOptionValue = "all";

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
  openByDefault?: boolean;
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

const Macrostrat: React.FC<MacrostratProps> = ({
  lat,
  lng,
  openByDefault = true,
}) => {
  const screens = useBreakpoint();
  const descriptionLayout = screens.md ? "horizontal" : "vertical";

  const [macroData, setMacroData] = useState<MacrostratResponse | null>(null);
  const [xddData, setXddData] = useState<XDDResponse | null>(null);
  const [geologicMapsData, setGeologicMapsData] = useState<GeologicMapResult[]>(
    []
  );
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  const [geologicMapsLoading, setGeologicMapsLoading] =
    useState<boolean>(false);
  const [geologicMapsError, setGeologicMapsError] = useState<string | null>(
    null
  );
  const [selectedScales, setSelectedScales] = useState<(number | string)[]>([]); // Changed type

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
      setGeologicMapsLoading(true);
      setError(null);
      setGeologicMapsError(null);

      const macrostratUrl = `https://macrostrat.org/api/v2/mobile/map_query_v2?lng=${lng}&lat=${lat}&z=9`;
      const resp = await fetch(macrostratUrl);
      if (!resp.ok) {
        throw new Error(`Macrostrat fetch error: ${resp.statusText}`);
      }
      const data = (await resp.json()) as MacrostratResponse;
      setMacroData(data);

      try {
        let maps = await MapService.getGeologicMaps(lat, lng);

        // Sort maps:
        // 1. Primary sort by scale: ascending for positive scales, -1 (unscaled) goes to the end.
        // 2. Secondary sort by year: descending (newer first).
        maps.sort((a, b) => {
          const aIsUnscaled = a.scale === -1;
          const bIsUnscaled = b.scale === -1;

          // Rule 1: Unscaled items go to the end
          if (aIsUnscaled && !bIsUnscaled) return 1; // a (-1) comes after b (positive)
          if (!aIsUnscaled && bIsUnscaled) return -1; // a (positive) comes before b (-1)

          // Rule 2: If scales are different (applies only if both are positive and different)
          // If both are unscaled, their scales are "equal" for this check (both -1)
          if (a.scale !== b.scale && !aIsUnscaled && !bIsUnscaled) {
            return a.scale - b.scale; // Sort positive scales numerically ascending
          }

          // Rule 3: If scales are effectively the same (both -1, or same positive value), sort by year descending
          return b.year - a.year;
        });

        setGeologicMapsData(maps);
      } catch (mapErr: any) {
        setGeologicMapsError(mapErr.message || "Failed to load geologic maps.");
        setGeologicMapsData([]);
      } finally {
        setGeologicMapsLoading(false);
      }
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

  const xddSnippets = xddData?.success.data || [];

  // Get unique scales for the filter dropdown, maintaining the sort order (finest first, -1 last)
  const uniqueScales: number[] = [];
  if (geologicMapsData.length > 0) {
    const scaleSet = new Set<number>();
    geologicMapsData.forEach((map) => {
      if (!scaleSet.has(map.scale)) {
        uniqueScales.push(map.scale);
        scaleSet.add(map.scale);
      }
    });
  }

  useEffect(() => {
    if (uniqueScales.length > 0) {
      const finestScale = uniqueScales[0];
      if (uniqueScales.length === 1) {
        // If only one unique scale, select it and "All"
        setSelectedScales([finestScale, allOptionValue]);
      } else {
        // Otherwise, select only the finest scale
        setSelectedScales([finestScale]);
      }
    } else {
      setSelectedScales([]);
    }
  }, [JSON.stringify(uniqueScales)]);

  const handleScaleChange = (checkedList: (number | string)[]) => {
    const allWasPreviouslyInSelectedScales =
      selectedScales.includes(allOptionValue);
    const allIsCurrentlyInCheckedList = checkedList.includes(allOptionValue);
    const allUniqueScalesAvailable = uniqueScales.length > 0;

    let newSelectedValues: (number | string)[];

    if (allIsCurrentlyInCheckedList && !allWasPreviouslyInSelectedScales) {
      // "All" was just explicitly checked by the user
      newSelectedValues = allUniqueScalesAvailable
        ? [...uniqueScales, allOptionValue]
        : [allOptionValue];
    } else if (
      !allIsCurrentlyInCheckedList &&
      allWasPreviouslyInSelectedScales
    ) {
      // "All" was just explicitly unchecked by the user
      // Revert to selecting only the highest resolution scale (or none if no scales)
      newSelectedValues = allUniqueScalesAvailable ? [uniqueScales[0]] : [];
    } else {
      // "All" was not the item directly clicked, or its state in checkedList matches its previous state.
      // This means an individual scale was checked/unchecked.
      const currentlySelectedNumeric = checkedList.filter(
        (v) => typeof v === "number"
      ) as number[];
      newSelectedValues = [...currentlySelectedNumeric];

      if (
        allUniqueScalesAvailable &&
        currentlySelectedNumeric.length === uniqueScales.length
      ) {
        // All individual scales are now checked, so add "all" to the selection
        if (!newSelectedValues.includes(allOptionValue)) {
          newSelectedValues.push(allOptionValue);
        }
      }
    }
    setSelectedScales(newSelectedValues);
  };

  const filteredGeologicMaps =
    selectedScales.filter((s) => typeof s === "number").length > 0
      ? geologicMapsData.filter((map) => selectedScales.includes(map.scale))
      : geologicMapsData;

  const groupOptions = [
    ...uniqueScales.map((scale) => ({
      label: scale === -1 ? "N/A" : scale.toLocaleString(),
      value: scale,
    })),
    ...(uniqueScales.length > 0
      ? [{ label: "All", value: allOptionValue, style: { marginRight: "8px" } }]
      : []),
  ];

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
    return null; // Or a <Text>No data available</Text> component
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

  return (
    <div>
      <style>{highlightCSS}</style>
      <Row gutter={[16, 16]}>
        <Col span={24}>
          <Collapse defaultActiveKey={openByDefault ? ["1"] : []}>
            <Panel header="Geologic map" key="1">
              <Descriptions
                layout={descriptionLayout}
                bordered
                column={1}
                size="small"
              >
                <Descriptions.Item label="Name">{name}</Descriptions.Item>
                <Descriptions.Item label="NGMDB Geologic Maps" span={1}>
                  {geologicMapsLoading ? (
                    <Text>Loading...</Text>
                  ) : geologicMapsError ? (
                    <Text type="danger">{geologicMapsError}</Text>
                  ) : geologicMapsData.length > 0 ? (
                    <>
                      <div style={{ marginBottom: 8 }}>
                        <Text>Scale: </Text>
                        <Checkbox.Group
                          options={groupOptions}
                          value={selectedScales}
                          onChange={handleScaleChange}
                          style={{
                            display: "flex",
                            flexWrap: "wrap",
                            alignItems: "center",
                          }}
                        />
                      </div>
                      <List
                        size="small"
                        dataSource={filteredGeologicMaps} // Use filtered data
                        renderItem={(map) => (
                          <List.Item style={{ padding: "8px 0" }}>
                            <List.Item.Meta
                              description={
                                <>
                                  <a
                                    href={`https://ngmdb.usgs.gov/Prodesc/proddesc_${map.id}.htm`}
                                    target="_blank"
                                    rel="noreferrer"
                                  >
                                    {map.title}
                                  </a>
                                  <br />
                                  <Text type="secondary">
                                    Scale:{" "}
                                    {map.scale === -1 ? "N/A" : map.scale}
                                  </Text>
                                  <br />
                                  <Text type="secondary">
                                    Authors: {map.authors}
                                  </Text>
                                  <br />
                                  <Text type="secondary">
                                    Source: {map.publisher} ({map.series},{" "}
                                    {map.year})
                                  </Text>
                                </>
                              }
                            />
                            {map.thumbnail && (
                              <img
                                style={{ maxHeight: "300px" }}
                                src={`https://ngmdb.usgs.gov${map.thumbnail}`}
                                alt={map.title}
                              />
                            )}
                          </List.Item>
                        )}
                      />
                    </>
                  ) : (
                    defaultIfEmpty(null)
                  )}
                </Descriptions.Item>
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
                <Descriptions.Item label="Lithology">{lith}</Descriptions.Item>
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
                <Descriptions.Item label="Age">
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
