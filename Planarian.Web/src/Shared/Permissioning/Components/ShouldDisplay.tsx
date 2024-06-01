import React, { ReactNode, useContext } from "react";
import { FeatureKey } from "../../../Modules/Account/Models/FeatureSettingVm";
import { AppContext } from "../../../Configuration/Context/AppContext";

interface ShouldDisplayProps {
  featureKey?: FeatureKey;
  children?: ReactNode;
}

const ShouldDisplay: React.FC<ShouldDisplayProps> = ({
  featureKey,
  children,
}) => {
  const { isFeatureEnabled } = useFeatureEnabled();

  if (featureKey && isFeatureEnabled(featureKey)) {
    return <>{children}</>;
  }

  return null;
};

export { ShouldDisplay };

export interface UseFeatureEnabledProps {
  featureKey: FeatureKey | undefined;
}

export const useFeatureEnabled = () => {
  const { permissions } = useContext(AppContext);

  const isFeatureEnabled = (
    featureKey?: FeatureKey | null | undefined
  ): boolean => {
    if (!featureKey) return false;
    const feature = permissions.visibleFields.find((f) => f.key === featureKey);
    return feature?.isEnabled || false;
  };

  return { isFeatureEnabled };
};
