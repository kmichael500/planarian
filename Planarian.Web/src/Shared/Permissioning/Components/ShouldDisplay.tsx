import React, { ReactNode, useContext } from "react";
import { FeatureKey } from "../../../Modules/Account/Models/FeatureSettingVm";
import { PermissionKey } from "../../../Modules/Authentication/Models/PermissionKey";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { AppService } from "../../Services/AppService";

interface ShouldDisplayProps {
  featureKey?: FeatureKey;
  permissionKey?: PermissionKey;
  children?: ReactNode;
}

const ShouldDisplay: React.FC<ShouldDisplayProps> = ({
  featureKey,
  permissionKey: permissionKeys,
  children,
}) => {
  const { isFeatureEnabled } = useFeatureEnabled();

  if (isFeatureEnabled(featureKey, permissionKeys)) {
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
    featureKey?: FeatureKey | null | undefined,
    permissionKey?: PermissionKey
  ): boolean => {
    if (featureKey) {
      const feature = permissions.visibleFields.find(
        (f) => f.key === featureKey
      );
      const isEnabled = feature?.isEnabled || false;
      if (!isEnabled) return false;
    }

    if (permissionKey && permissionKey.length > 0) {
      return AppService.HasPermission(permissionKey);
    }

    return true;
  };

  return { isFeatureEnabled };
};
