import React, { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from "react";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { MAP_LAYER_DEFINITIONS, getLayerDefinition } from "./MapLayerDefinitions";

type LayerPreference = { visible: boolean; opacity: number };
type LayerPreferences = Record<string, LayerPreference>;

interface MapLayerContextValue {
  isVisible: (id: string) => boolean;
  opacity: (id: string) => number;
  toggleLayer: (id: string) => void;
  setOpacity: (id: string, opacity: number) => void;
  terrainEnabled: boolean;
  terrainExaggeration: number;
  setTerrainEnabled: React.Dispatch<React.SetStateAction<boolean>>;
  setTerrainExaggeration: React.Dispatch<React.SetStateAction<number>>;
}

const MapLayerContext = createContext<MapLayerContextValue | null>(null);

const defaultPreferences = (): LayerPreferences =>
  Object.fromEntries(
    MAP_LAYER_DEFINITIONS.filter((layer) => layer.type !== "group").map((layer) => [
      layer.id,
      { visible: layer.defaultVisible, opacity: layer.defaultOpacity },
    ])
  );

const loadPreferences = (accountId: string | null): LayerPreferences => {
  const defaults = defaultPreferences();
  if (!accountId) return defaults;

  const raw = localStorage.getItem(`planarianMapLayerStates-${accountId}`);
  if (!raw) return defaults;

  try {
    const saved = JSON.parse(raw) as Array<{ id: string; isActive: boolean; opacity: number }>;
    saved.forEach(({ id, isActive, opacity }) => {
      if (defaults[id]) defaults[id] = { visible: isActive, opacity };
    });
  } catch (error) {
    console.error("Failed to parse saved map layer states", error);
  }
  return defaults;
};

const loadPreviousGroupMembers = (accountId: string | null): Record<string, string[]> => {
  if (!accountId) return {};
  const raw = localStorage.getItem(`planarianPrevGroupMembers-${accountId}`);
  if (!raw) return {};
  try {
    return JSON.parse(raw) as Record<string, string[]>;
  } catch (error) {
    console.error("Failed to parse saved map layer group members", error);
    return {};
  }
};

const savePreviousGroupMembers = (accountId: string | null, value: Record<string, string[]>) => {
  if (!accountId) return;
  localStorage.setItem(`planarianPrevGroupMembers-${accountId}`, JSON.stringify(value));
};

export const MapLayerProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const { currentAccountId } = useContext(AppContext);
  const [preferences, setPreferences] = useState<LayerPreferences>(() => loadPreferences(currentAccountId));
  const previousGroupMembers = useRef<Record<string, string[]>>(loadPreviousGroupMembers(currentAccountId));
  const [terrainEnabled, setTerrainEnabled] = useState(false);
  const [terrainExaggeration, setTerrainExaggeration] = useState(1);

  useEffect(() => {
    setPreferences(loadPreferences(currentAccountId));
    previousGroupMembers.current = loadPreviousGroupMembers(currentAccountId);
    setTerrainEnabled(false);
    setTerrainExaggeration(1);
  }, [currentAccountId]);

  useEffect(() => {
    if (!currentAccountId) return;
    const serializable = Object.entries(preferences).map(([id, value]) => ({
      id,
      isActive: value.visible,
      opacity: value.opacity,
    }));
    localStorage.setItem(`planarianMapLayerStates-${currentAccountId}`, JSON.stringify(serializable));
  }, [currentAccountId, preferences]);

  const isVisible = useCallback(
    (id: string) => {
      const definition = getLayerDefinition(id);
      if (definition?.type === "group") {
        return definition.memberLayerIds.some((memberId) => preferences[memberId]?.visible);
      }
      return preferences[id]?.visible ?? false;
    },
    [preferences]
  );

  const opacity = useCallback(
    (id: string) => {
      const definition = getLayerDefinition(id);
      if (definition?.type === "group") {
        const firstMember = definition.memberLayerIds[0];
        return preferences[firstMember]?.opacity ?? definition.defaultOpacity;
      }
      return preferences[id]?.opacity ?? definition?.defaultOpacity ?? 1;
    },
    [preferences]
  );

  const toggleLayer = useCallback((id: string) => {
    setPreferences((current) => {
      const definition = getLayerDefinition(id);
      if (!definition) return current;

      if (definition.type === "group") {
        const activeMemberIds = definition.memberLayerIds.filter((memberId) => current[memberId]?.visible);
        let membersToActivate: string[] = [];

        if (activeMemberIds.length > 0) {
          const nextRemembered = { ...previousGroupMembers.current, [id]: activeMemberIds };
          previousGroupMembers.current = nextRemembered;
          savePreviousGroupMembers(currentAccountId, nextRemembered);
        } else {
          const remembered = previousGroupMembers.current[id]?.filter((memberId) =>
            definition.memberLayerIds.includes(memberId)
          );
          membersToActivate = remembered?.length ? remembered : definition.memberLayerIds;
        }

        return Object.fromEntries(
          Object.entries(current).map(([layerId, value]) => [
            layerId,
            definition.memberLayerIds.includes(layerId)
              ? { ...value, visible: membersToActivate.includes(layerId) }
              : value,
          ])
        );
      }

      const turnOn = !current[id]?.visible;
      const next = Object.fromEntries(
        Object.entries(current).map(([layerId, value]) => [
          layerId,
          layerId === id ? { ...value, visible: turnOn } : value,
        ])
      ) as LayerPreferences;

      if (definition.groupId) {
        const parent = getLayerDefinition(definition.groupId);
        if (
          parent?.type === "group" &&
          !parent.memberLayerIds.some((memberId) => next[memberId]?.visible)
        ) {
          const nextRemembered = { ...previousGroupMembers.current };
          delete nextRemembered[parent.id];
          previousGroupMembers.current = nextRemembered;
          savePreviousGroupMembers(currentAccountId, nextRemembered);
        }
      }

      return next;
    });
  }, [currentAccountId]);

  const setOpacity = useCallback((id: string, nextOpacity: number) => {
    setPreferences((current) => {
      const definition = getLayerDefinition(id);
      if (!definition) return current;
      const ids = definition.type === "group" ? definition.memberLayerIds : [id];
      return Object.fromEntries(
        Object.entries(current).map(([layerId, value]) => [
          layerId,
          ids.includes(layerId) ? { ...value, opacity: nextOpacity } : value,
        ])
      );
    });
  }, []);

  const value = useMemo<MapLayerContextValue>(
    () => ({
      isVisible,
      opacity,
      toggleLayer,
      setOpacity,
      terrainEnabled,
      terrainExaggeration,
      setTerrainEnabled,
      setTerrainExaggeration,
    }),
    [isVisible, opacity, setOpacity, terrainEnabled, terrainExaggeration, toggleLayer]
  );

  return <MapLayerContext.Provider value={value}>{children}</MapLayerContext.Provider>;
};

export const useMapLayers = () => {
  const context = useContext(MapLayerContext);
  if (!context) throw new Error("useMapLayers must be used inside MapLayerProvider");
  return context;
};
