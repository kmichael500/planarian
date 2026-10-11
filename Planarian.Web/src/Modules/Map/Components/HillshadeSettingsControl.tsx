import { ColorPicker, InputNumber, Slider, Tabs } from "antd";
import React, { useState } from "react";
import styled from "styled-components";
import {
  getHillshadePaletteColors,
  HillshadePaletteColorKey,
  MapTheme,
} from "./HillshadeStyle";
import { useMapLayers } from "./MapLayerContext";

const DIRECTIONS = [
  { label: "NW", value: 315, column: 1, row: 1 },
  { label: "N", value: 0, column: 2, row: 1 },
  { label: "NE", value: 45, column: 3, row: 1 },
  { label: "W", value: 270, column: 1, row: 2 },
  { label: "E", value: 90, column: 3, row: 2 },
  { label: "SW", value: 225, column: 1, row: 3 },
  { label: "S", value: 180, column: 2, row: 3 },
  { label: "SE", value: 135, column: 3, row: 3 },
] as const;

const COLOR_FIELDS: Array<{
  key: HillshadePaletteColorKey;
  label: string;
}> = [
  { key: "backgroundColor", label: "Background" },
  { key: "shadowColor", label: "Shadow" },
  { key: "highlightColor", label: "Highlight" },
  { key: "accentColor", label: "Accent" },
];

export const HillshadeSettingsControl: React.FC = () => {
  const {
    hillshadePreferences,
    setHillshadeExaggeration,
    setHillshadeDirection,
    setHillshadeContrast,
    setHillshadePaletteColor,
    resetHillshadePalette,
    resetHillshadeSettings,
  } = useMapLayers();
  const [showAdvancedColors, setShowAdvancedColors] = useState(false);

  const renderPalette = (theme: MapTheme) => {
    const colors = getHillshadePaletteColors(theme, hillshadePreferences);
    return (
      <PalettePanel>
        {COLOR_FIELDS.map(({ key, label }) => (
          <ColorRow key={key}>
            <span>{label}</span>
            <ColorPicker
              aria-label={`${theme} hillshade ${label.toLowerCase()} color`}
              value={colors[key]}
              onChange={(color) =>
                setHillshadePaletteColor(theme, key, color.toHexString())
              }
            />
          </ColorRow>
        ))}
        <TextButton type="button" onClick={() => resetHillshadePalette(theme)}>
          Reset {theme} colors
        </TextButton>
      </PalettePanel>
    );
  };

  return (
    <SettingsContainer>
      <SettingsPanel>
        <SettingLabel>
          <span>Relief strength</span>
          <Value>{hillshadePreferences.exaggeration.toFixed(2)}</Value>
        </SettingLabel>
        <Slider
          aria-label="Hillshade relief strength"
          min={0}
          max={1}
          step={0.05}
          value={hillshadePreferences.exaggeration}
          onChange={setHillshadeExaggeration}
        />

        <SettingLabel>
          <span>Contrast</span>
          <Value>{hillshadePreferences.contrast.toFixed(2)}×</Value>
        </SettingLabel>
        <Slider
          aria-label="Hillshade contrast"
          min={0.5}
          max={1.5}
          step={0.05}
          value={hillshadePreferences.contrast}
          onChange={setHillshadeContrast}
        />

        <SettingLabel>
          <span>Light direction</span>
          <Value>
            {Math.round(hillshadePreferences.illuminationDirection)}°
          </Value>
        </SettingLabel>

        <DirectionControl aria-label="Hillshade light direction presets">
          {DIRECTIONS.map((direction) => (
            <DirectionButton
              key={direction.label}
              type="button"
              $active={
                Math.round(hillshadePreferences.illuminationDirection) ===
                direction.value
              }
              style={{
                gridColumn: direction.column,
                gridRow: direction.row,
              }}
              onClick={() => setHillshadeDirection(direction.value)}
              aria-label={`Light from ${direction.label}`}
            >
              {direction.label}
            </DirectionButton>
          ))}
          <DirectionReadout>
            <InputNumber
              aria-label="Hillshade light direction degrees"
              min={0}
              max={359}
              step={1}
              value={Math.round(hillshadePreferences.illuminationDirection)}
              onChange={(value) =>
                value !== null && setHillshadeDirection(value)
              }
              controls={false}
            />
            <span>°</span>
          </DirectionReadout>
        </DirectionControl>

        <AdvancedToggle
          type="button"
          aria-expanded={showAdvancedColors}
          onClick={() => setShowAdvancedColors((value) => !value)}
        >
          {showAdvancedColors ? "▾" : "▸"} Advanced colors
        </AdvancedToggle>

        {showAdvancedColors && (
          <Tabs
            size="small"
            items={[
              {
                key: "light",
                label: "Light",
                children: renderPalette("light"),
              },
              {
                key: "dark",
                label: "Dark",
                children: renderPalette("dark"),
              },
            ]}
          />
        )}

        <ResetButton type="button" onClick={resetHillshadeSettings}>
          Reset hillshade defaults
        </ResetButton>
      </SettingsPanel>
    </SettingsContainer>
  );
};

const SettingsContainer = styled.div`
  margin-top: 6px;
`;

const SettingsPanel = styled.div`
  margin-top: 8px;
  padding: 10px;
  border: 1px solid var(--border-color);
  border-radius: 6px;
  background: var(--background-color);
`;

const SettingLabel = styled.div`
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  line-height: 1.4;
  margin-top: 8px;

  &:first-child {
    margin-top: 0;
  }
`;

const Value = styled.span`
  color: var(--text-color-secondary, var(--text-color));
  font-variant-numeric: tabular-nums;
`;

const DirectionControl = styled.div`
  width: 164px;
  margin: 8px auto 12px;
  display: grid;
  grid-template-columns: repeat(3, 48px);
  grid-template-rows: repeat(3, 38px);
  gap: 4px;
  justify-content: center;
  align-items: center;
`;

const DirectionButton = styled.button<{ $active: boolean }>`
  height: 34px;
  border: 1px solid var(--border-color);
  border-radius: 50%;
  background: ${(props) =>
    props.$active ? "var(--primary-color, #1677ff)" : "var(--surface-color)"};
  color: ${(props) => (props.$active ? "#fff" : "var(--text-color)")};
  cursor: pointer;
  font-size: 10px;
  font-weight: 600;
`;

const DirectionReadout = styled.div`
  grid-column: 2;
  grid-row: 2;
  display: flex;
  align-items: center;
  gap: 2px;

  .ant-input-number {
    width: 46px;
  }

  .ant-input-number-input {
    padding: 0 4px;
    text-align: center;
  }
`;

const AdvancedToggle = styled.button`
  width: 100%;
  border: 0;
  border-top: 1px solid var(--border-color);
  background: transparent;
  color: var(--text-color);
  cursor: pointer;
  margin-top: 4px;
  padding: 10px 0 4px;
  text-align: left;
  font: inherit;
`;

const PalettePanel = styled.div`
  display: flex;
  flex-direction: column;
  gap: 8px;
`;

const ColorRow = styled.div`
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
`;

const TextButton = styled.button`
  align-self: flex-start;
  border: 0;
  background: transparent;
  color: var(--primary-color, #1677ff);
  cursor: pointer;
  padding: 2px 0;
  font: inherit;
`;

const ResetButton = styled.button`
  width: 100%;
  border: 1px solid var(--border-color);
  border-radius: 6px;
  background: var(--surface-color);
  color: var(--text-color);
  cursor: pointer;
  margin-top: 10px;
  padding: 5px 8px;
  font: inherit;
`;
