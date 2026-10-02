import { Checkbox, InputNumber, Slider, Space, Tooltip } from "antd";
import React, { useContext, useState } from "react";
import styled from "styled-components";
import { BuildOutlined, DatabaseOutlined } from "@ant-design/icons";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { PlanarianModal } from "../../../Shared/Components/Buttons/PlanarianModal";
import { AppContext } from "../../../Configuration/Context/AppContext";
import {
  MAP_LAYER_DEFINITIONS,
  MAP_LAYER_SECTIONS,
  selectableMapLayers,
} from "./MapLayerDefinitions";
import { useMapLayers } from "./MapLayerContext";

interface MapLayerControlProps {
  position?: Partial<Record<"top" | "right" | "left" | "bottom", string>>;
  excludedLayerIds?: string[];
}

export const MapLayerControl: React.FC<MapLayerControlProps> = ({
  position,
  excludedLayerIds = [],
}) => {
  const { currentAccountId } = useContext(AppContext);
  const { isVisible, opacity, toggleLayer, setOpacity, terrainEnabled, terrainExaggeration, setTerrainEnabled, setTerrainExaggeration } = useMapLayers();
  const [showMacrostratDisclaimer, setShowMacrostratDisclaimer] = useState(false);
  const disclaimerKey = `planarianMacrostratDisclaimerShown-${currentAccountId}`;
  const excludedLayerIdSet = new Set(excludedLayerIds);
  const visibleSelectableMapLayers = selectableMapLayers.filter(
    (layer) =>
      !excludedLayerIdSet.has(layer.id) &&
      (layer.type !== "group" ||
        layer.memberLayerIds.some((memberId) => !excludedLayerIdSet.has(memberId)))
  );
  const layerControlSectionIds = ["base-layers", "hydrology", "reference"] as const;
  const layerControlLayers = layerControlSectionIds.flatMap((sectionId) =>
    visibleSelectableMapLayers.filter((layer) => layer.sectionId === sectionId)
  );
  const mapDataLayers = visibleSelectableMapLayers.filter(
    (layer) => layer.sectionId === "cave-data"
  );

  const toggle = (id: string, visibleGroupMemberIds?: string[]) => {
    if (id === "macrostrat" && !isVisible(id) && !localStorage.getItem(disclaimerKey)) {
      setShowMacrostratDisclaimer(true);
    }

    const definition = MAP_LAYER_DEFINITIONS.find((layer) => layer.id === id);
    if (
      definition?.type === "group" &&
      visibleGroupMemberIds &&
      visibleGroupMemberIds.length !== definition.memberLayerIds.length
    ) {
      const activeMemberIds = visibleGroupMemberIds.filter((memberId) => isVisible(memberId));
      const turnOn = activeMemberIds.length === 0;
      visibleGroupMemberIds.forEach((memberId) => {
        if (isVisible(memberId) !== turnOn) toggleLayer(memberId);
      });
      return;
    }

    toggleLayer(id);
  };

  const updateOpacity = (id: string, value: number, visibleGroupMemberIds?: string[]) => {
    const definition = MAP_LAYER_DEFINITIONS.find((layer) => layer.id === id);
    if (
      definition?.type === "group" &&
      visibleGroupMemberIds &&
      visibleGroupMemberIds.length !== definition.memberLayerIds.length
    ) {
      visibleGroupMemberIds.forEach((memberId) => setOpacity(memberId, value));
      return;
    }
    setOpacity(id, value);
  };

  const closeDisclaimer = () => {
    localStorage.setItem(disclaimerKey, "true");
    setShowMacrostratDisclaimer(false);
  };

  const renderLayerList = (
    layers: typeof visibleSelectableMapLayers,
    showSectionHeadings = true
  ) => (
    <LayerList>
      {layers.map((layer, index) => {
        const members = layer.type === "group"
          ? MAP_LAYER_DEFINITIONS.filter(
              (candidate) =>
                layer.memberLayerIds.includes(candidate.id) &&
                !excludedLayerIdSet.has(candidate.id)
            )
          : [];
        const activeCount = members.filter((member) => isVisible(member.id)).length;
        const section = MAP_LAYER_SECTIONS.find((candidate) => candidate.id === layer.sectionId);
        const showSectionHeading = index === 0 || layers[index - 1].sectionId !== layer.sectionId;
        const hideFirstBaseLayerHeading = index === 0 && section?.id === "base-layers";

        return (
          <React.Fragment key={layer.id}>
            {showSectionHeadings && showSectionHeading && section && !hideFirstBaseLayerHeading && (
              <SectionHeading>{section.displayName}</SectionHeading>
            )}
            <LayerRow>
              <Checkbox
                checked={layer.type === "group" ? activeCount === members.length && members.length > 0 : isVisible(layer.id)}
                indeterminate={layer.type === "group" && activeCount > 0 && activeCount < members.length}
                onChange={() => toggle(layer.id, members.map((member) => member.id))}
              >
                {layer.description ? (
                  <Tooltip title={layer.description}><span>{layer.displayName}</span></Tooltip>
                ) : layer.displayName}
              </Checkbox>
              <Slider
                min={0}
                max={1}
                step={0.1}
                value={opacity(layer.id)}
                onChange={(value) => updateOpacity(layer.id, value, members.map((member) => member.id))}
              />
              {layer.type === "group" && activeCount > 0 && (
                <GroupMembers>
                  {members.map((member) => (
                    <Checkbox key={member.id} checked={isVisible(member.id)} onChange={() => toggle(member.id)}>
                      {member.description ? (
                        <Tooltip title={member.description}><span>{member.displayName}</span></Tooltip>
                      ) : member.displayName}
                    </Checkbox>
                  ))}
                </GroupMembers>
              )}
            </LayerRow>
          </React.Fragment>
        );
      })}
    </LayerList>
  );

  const legendPosition = {
    top: position?.top ? `${parseInt(position.top) + 100}px` : "100px",
    right: position?.right || "0",
    left: position?.left || "auto",
    bottom: position?.bottom || "auto",
  };

  return (
    <>
      <PlanarianModal
        open={showMacrostratDisclaimer}
        onClose={closeDisclaimer}
        height="auto"
        header="Macrostrat Geology Disclaimer"
        width="600px"
        footer={<PlanarianButton alwaysShowChildren icon={undefined} onClick={closeDisclaimer}>Close</PlanarianButton>}
      >
        <p>Macrostrat coverage varies by region and may be less detailed than traditional geologic maps.</p>
        <p>Typical resolution by state:</p>
        <ul><li>KY – 1:24,000</li><li>TN – 1:250,000</li><li>AL – 1:250,000</li><li>GA – 1:500,000</li></ul>
        <p>Only 1:24,000 is generally suitable for precision use.</p>
        <p>For the most detailed map available, turn on NGMDB Geology and select the smallest scale.</p>
      </PlanarianModal>

      <ControlStack className="planarian-map-control" style={{ zIndex: 200, ...position }}>
        <ControlPanel className="planarian-map-control">
          <HoverIcon aria-label="Map layers">
            <svg xmlns="http://www.w3.org/2000/svg" height="24px" viewBox="0 0 576 512"><path d="M264.5 5.2c14.9-6.9 32.1-6.9 47 0l218.6 101c8.5 3.9 13.9 12.4 13.9 21.8s-5.4 17.9-13.9 21.8l-218.6 101c-14.9 6.9-32.1 6.9-47 0L45.9 149.8C37.4 145.8 32 137.3 32 128s5.4-17.9 13.9-21.8L264.5 5.2zM476.9 209.6l53.2 24.6c8.5 3.9 13.9 12.4 13.9 21.8s-5.4 17.9-13.9 21.8l-218.6 101c-14.9 6.9-32.1 6.9-47 0L45.9 277.8C37.4 273.8 32 265.3 32 256s5.4-17.9 13.9-21.8l53.2-24.6 152 70.2c23.4 10.8 50.4 10.8 73.8 0l152-70.2zm-152 198.2l152-70.2 53.2 24.6c8.5 3.9 13.9 12.4 13.9 21.8s-5.4 17.9-13.9 21.8l-218.6 101c-14.9 6.9-32.1 6.9-47 0L45.9 405.8C37.4 401.8 32 393.3 32 384s5.4-17.9 13.9-21.8l53.2-24.6 152 70.2c23.4 10.8 50.4 10.8 73.8 0z" /></svg>
          </HoverIcon>
          <ContentWrapper>
            {renderLayerList(layerControlLayers)}
            <Space direction="vertical">
              <PlanarianButton alwaysShowChildren onClick={() => setTerrainEnabled((enabled) => !enabled)} icon={<BuildOutlined />}>
                {terrainEnabled ? "Disable 3D Terrain" : "Enable 3D Terrain"}
              </PlanarianButton>
              {terrainEnabled && (
                <div>Exaggeration: <InputNumber min={0} max={10} step={0.1} value={terrainExaggeration} onChange={(value) => value !== null && setTerrainExaggeration(value)} /></div>
              )}
            </Space>
          </ContentWrapper>
        </ControlPanel>

        {mapDataLayers.length > 0 && (
          <ControlPanel className="planarian-map-control">
            <HoverIcon aria-label="Map data"><DatabaseOutlined style={{ fontSize: 22 }} /></HoverIcon>
            <ContentWrapper>
              {renderLayerList(mapDataLayers, false)}
            </ContentWrapper>
          </ControlPanel>
        )}
      </ControlStack>

      {MAP_LAYER_DEFINITIONS.filter((layer) => layer.type === "vector" && layer.legend && isVisible(layer.id)).map((layer) => (
        <LegendPanel key={layer.id} className="planarian-map-control" style={legendPosition}>
          <HoverIcon><svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512"><path d="M256 512A256 256 0 1 0 256 0a256 256 0 1 0 0 512zM216 336h24v-64h-24c-13.3 0-24-10.7-24-24s10.7-24 24-24h48c13.3 0 24 10.7 24 24v88h8c13.3 0 24 10.7 24 24s-10.7 24-24 24h-80c-13.3 0-24-10.7-24-24s10.7-24 24-24zm40-208a32 32 0 1 1 0 64 32 32 0 1 1 0-64z" /></svg></HoverIcon>
          <ContentWrapper>{layer.type === "vector" ? layer.legend : null}</ContentWrapper>
        </LegendPanel>
      ))}
    </>
  );
};

const ControlStack = styled.div`
  position: absolute; display: flex; flex-direction: column; align-items: flex-end;
  gap: 8px; margin: 20px;
`;
const ControlPanel = styled.div`
  position: relative; width: 46px; height: 40px; box-sizing: border-box;
  background: var(--surface-color); color: var(--text-color); border: 1px solid var(--border-color);
  box-shadow: 0 2px 4px rgba(0,0,0,.3); padding: 8px; font-size: 13px; line-height: 2;
  border-radius: 8px; outline: none;
`;
const LegendPanel = styled.div`
  position: absolute; z-index: 12; background: var(--surface-color); color: var(--text-color);
  border: 1px solid var(--border-color); box-shadow: 0 2px 4px rgba(0,0,0,.3);
  padding: 8px; margin: 20px; font-size: 13px; line-height: 2; border-radius: 8px; outline: none;
`;
const HoverIcon = styled.div`
  width: 28px; height: 24px; cursor: pointer; color: var(--text-color); svg { fill: currentColor; }
  ${LegendPanel}:hover & { display: none; }
`;
const ContentWrapper = styled.div`
  display: none;
  ${ControlPanel}:hover & {
    display: block; position: absolute; top: -1px; right: calc(100% - 1px); width: 280px;
    box-sizing: border-box; background: var(--surface-color); color: var(--text-color);
    border: 1px solid var(--border-color); box-shadow: 0 2px 4px rgba(0,0,0,.3);
    padding: 8px; border-radius: 8px; z-index: 201;
  }
  ${LegendPanel}:hover & { display: block; }
`;
const SectionHeading = styled.div`
  border-top: 1px solid var(--border-color); font-size: 11px; font-weight: 600;
  letter-spacing: 0.04em; margin: 8px 0 5px; padding-top: 8px; text-transform: uppercase;
`;
const LayerList = styled.div`
  max-height: 55vh;
  overflow-y: auto;
  overflow-x: hidden;
  margin-bottom: 10px;
  padding-right: 14px;
  box-sizing: border-box;
  scrollbar-gutter: stable;
`;
const LayerRow = styled.div`margin-bottom: 15px;`;
const GroupMembers = styled.div`margin-left: 20px; margin-top: 5px; display: flex; flex-direction: column; gap: 5px;`;
