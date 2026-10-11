import React from "react";
import styled from "styled-components";
import { useMapLayers } from "./MapLayerContext";
import { UsgsHillshadeVariant, USGS_HILLSHADE_VARIANTS } from "./UsgsHillshade";

export const UsgsHillshadeSettingsControl: React.FC = () => {
  const { usgsHillshadeVariant, setUsgsHillshadeVariant } = useMapLayers();

  return (
    <SettingsContainer>
      <SettingsPanel>
        <SettingLabel htmlFor="usgs-hillshade-variant">Variant</SettingLabel>
        <VariantSelect
          id="usgs-hillshade-variant"
          aria-label="USGS hillshade variant"
          value={usgsHillshadeVariant}
          onChange={(event: React.ChangeEvent<HTMLSelectElement>) =>
            setUsgsHillshadeVariant(event.target.value as UsgsHillshadeVariant)
          }
        >
          {USGS_HILLSHADE_VARIANTS.map((variant) => (
            <option key={variant.value} value={variant.value}>
              {variant.label}
            </option>
          ))}
        </VariantSelect>
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

const SettingLabel = styled.label`
  display: block;
  margin-bottom: 5px;
  font-weight: 600;
  line-height: 1.4;
`;

const VariantSelect = styled.select`
  width: 100%;
  box-sizing: border-box;
  border: 1px solid var(--border-color);
  border-radius: 6px;
  background: var(--surface-color, var(--background-color));
  color: var(--text-color);
  padding: 6px 8px;
  font: inherit;
`;
