import { Checkbox, InputNumber, Slider, Space } from "antd";
import React, { useContext, useState } from "react";
import styled from "styled-components";
import { BuildOutlined } from "@ant-design/icons";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { PlanarianModal } from "../../../Shared/Components/Buttons/PlanarianModal";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { MAP_LAYER_DEFINITIONS, selectableMapLayers } from "./MapLayerDefinitions";
import { useMapLayers } from "./MapLayerContext";

interface MapLayerControlProps {
  position?: Partial<Record<"top" | "right" | "left" | "bottom", string>>;
}

export const MapLayerControl: React.FC<MapLayerControlProps> = ({ position }) => {
  const { currentAccountId } = useContext(AppContext);
  const { isVisible, opacity, toggleLayer, setOpacity, terrainEnabled, terrainExaggeration, setTerrainEnabled, setTerrainExaggeration } = useMapLayers();
  const [showMacrostratDisclaimer, setShowMacrostratDisclaimer] = useState(false);
  const disclaimerKey = `planarianMacrostratDisclaimerShown-${currentAccountId}`;

  const toggle = (id: string) => {
    if (id === "macrostrat" && !isVisible(id) && !localStorage.getItem(disclaimerKey)) {
      setShowMacrostratDisclaimer(true);
    }
    toggleLayer(id);
  };

  const closeDisclaimer = () => {
    localStorage.setItem(disclaimerKey, "true");
    setShowMacrostratDisclaimer(false);
  };

  const legendPosition = {
    top: position?.top ? `${parseInt(position.top) + 50}px` : "100px",
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

      <ControlPanel className="planarian-map-control" style={{ zIndex: 200, ...position }}>
        <HoverIcon aria-label="Map layers">
          <svg xmlns="http://www.w3.org/2000/svg" height="24px" viewBox="0 0 576 512"><path d="M264.5 5.2c14.9-6.9 32.1-6.9 47 0l218.6 101c8.5 3.9 13.9 12.4 13.9 21.8s-5.4 17.9-13.9 21.8l-218.6 101c-14.9 6.9-32.1 6.9-47 0L45.9 149.8C37.4 145.8 32 137.3 32 128s5.4-17.9 13.9-21.8L264.5 5.2zM476.9 209.6l53.2 24.6c8.5 3.9 13.9 12.4 13.9 21.8s-5.4 17.9-13.9 21.8l-218.6 101c-14.9 6.9-32.1 6.9-47 0L45.9 277.8C37.4 273.8 32 265.3 32 256s5.4-17.9 13.9-21.8l53.2-24.6 152 70.2c23.4 10.8 50.4 10.8 73.8 0l152-70.2zm-152 198.2l152-70.2 53.2 24.6c8.5 3.9 13.9 12.4 13.9 21.8s-5.4 17.9-13.9 21.8l-218.6 101c-14.9 6.9-32.1 6.9-47 0L45.9 405.8C37.4 401.8 32 393.3 32 384s5.4-17.9 13.9-21.8l53.2-24.6 152 70.2c23.4 10.8 50.4 10.8 73.8 0z" /></svg>
        </HoverIcon>
        <ContentWrapper>
          Layers
          <LayerList>
            {selectableMapLayers.map((layer) => {
              const members = layer.type === "group"
                ? MAP_LAYER_DEFINITIONS.filter((candidate) => layer.memberLayerIds.includes(candidate.id))
                : [];
              const activeCount = members.filter((member) => isVisible(member.id)).length;
              return (
                <LayerRow key={layer.id}>
                  <Checkbox
                    checked={layer.type === "group" ? activeCount === members.length && members.length > 0 : isVisible(layer.id)}
                    indeterminate={layer.type === "group" && activeCount > 0 && activeCount < members.length}
                    onChange={() => toggle(layer.id)}
                  >{layer.displayName}</Checkbox>
                  <Slider min={0} max={1} step={0.1} value={opacity(layer.id)} onChange={(value) => setOpacity(layer.id, value)} />
                  {layer.type === "group" && activeCount > 0 && (
                    <GroupMembers>
                      {members.map((member) => (
                        <Checkbox key={member.id} checked={isVisible(member.id)} onChange={() => toggle(member.id)}>
                          {member.displayName}
                        </Checkbox>
                      ))}
                    </GroupMembers>
                  )}
                </LayerRow>
              );
            })}
          </LayerList>
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

      {MAP_LAYER_DEFINITIONS.filter((layer) => layer.type === "vector" && layer.legend && isVisible(layer.id)).map((layer) => (
        <LegendPanel key={layer.id} className="planarian-map-control" style={legendPosition}>
          <HoverIcon><svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512"><path d="M256 512A256 256 0 1 0 256 0a256 256 0 1 0 0 512zM216 336h24v-64h-24c-13.3 0-24-10.7-24-24s10.7-24 24-24h48c13.3 0 24 10.7 24 24v88h8c13.3 0 24 10.7 24 24s-10.7 24-24 24h-80c-13.3 0-24-10.7-24-24s10.7-24 24-24zm40-208a32 32 0 1 1 0 64 32 32 0 1 1 0-64z" /></svg></HoverIcon>
          <ContentWrapper>{layer.type === "vector" ? layer.legend : null}</ContentWrapper>
        </LegendPanel>
      ))}
    </>
  );
};

const ControlPanel = styled.div`
  position: absolute; background: var(--surface-color); color: var(--text-color); border: 1px solid var(--border-color);
  box-shadow: 0 2px 4px rgba(0,0,0,.3); padding: 8px; margin: 20px; font-size: 13px; line-height: 2;
  border-radius: 8px; outline: none;
`;
const LegendPanel = styled(ControlPanel)`z-index: 12;`;
const HoverIcon = styled.div`
  width: 28px; height: 24px; cursor: pointer; color: var(--text-color); svg { fill: currentColor; }
  ${ControlPanel}:hover &, ${LegendPanel}:hover & { display: none; }
`;
const ContentWrapper = styled.div`
  display: none; ${ControlPanel}:hover &, ${LegendPanel}:hover & { display: block; }
`;
const LayerList = styled.div`max-height: 55vh; overflow-y: auto; overflow-x: hidden; margin-bottom: 10px;`;
const LayerRow = styled.div`margin-bottom: 15px;`;
const GroupMembers = styled.div`margin-left: 20px; margin-top: 5px; display: flex; flex-direction: column; gap: 5px;`;
