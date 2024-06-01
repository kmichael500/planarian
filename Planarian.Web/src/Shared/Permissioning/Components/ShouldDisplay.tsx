import React, { ReactNode, useCallback, useEffect, useState } from "react";
import {
  FeatureKey,
  FeatureSettingVm,
} from "../../../Modules/Account/Models/FeatureSettingVm";
import { AccountService } from "../../../Modules/Account/Services/AccountService";

interface ShouldDisplayProps {
  featureKey?: FeatureKey;
  children?: ReactNode;
}

const ShouldDisplay: React.FC<ShouldDisplayProps> = ({
  featureKey,
  children,
}) => {
  const isEnabled = useFeatureEnabled().isFeatureEnabled(featureKey);

  if (featureKey && isEnabled) {
    return <>{children}</>;
  }

  return null;
};

export { ShouldDisplay };

export interface UseFeatureEnabledProps {
  featureKey: FeatureKey | undefined;
}

export const useFeatureEnabled = () => {
  const [features, setFeatures] = useState<FeatureSettingVm[]>([]);
  const [isLoading, setLoading] = useState<boolean>(true);

  useEffect(() => {
    const fetchFeatureSettings = async () => {
      const response = await AccountService.GetFeatureSettings();
      setFeatures(response);
      setLoading(false);
    };

    fetchFeatureSettings();
  }, []);

  const isFeatureEnabled = (
    featureKey?: FeatureKey | null | undefined
  ): boolean => {
    if (!featureKey) return false;
    const feature = features.find((f) => f.key === featureKey);
    return feature?.isEnabled || false;
  };

  return { isFeatureEnabled, isLoading };
};
