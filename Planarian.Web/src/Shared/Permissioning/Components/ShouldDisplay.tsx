import React, { ReactNode, useCallback, useContext } from "react";
import {
  ComputedFeatureKey,
  DisplayFeatureKey,
} from "../../../Modules/Account/Models/FeatureSettingVm";
import { PermissionKey } from "../../../Modules/Authentication/Models/PermissionKey";
import { AppContext } from "../../../Configuration/Context/AppContext";

interface ShouldDisplayProps {
  featureKey?: DisplayFeatureKey;
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

export const useFeatureEnabled = () => {
  const { hasPermission, permissions } = useContext(AppContext);

  const isFeatureEnabled = useCallback(
    (featureKey?: DisplayFeatureKey | null | undefined, permissionKey?: PermissionKey): boolean => {
      if (featureKey === ComputedFeatureKey.CaveDistance) {
        return true;
      }

      if (featureKey) {
        const feature = permissions.visibleFields.find(
          (f) => f.key === featureKey
        );
        const isEnabled = feature?.isEnabled || false;
        if (!isEnabled) return false;
      }

      if (permissionKey && permissionKey.length > 0) {
        return hasPermission(permissionKey);
      }

      return true;
    },
    [hasPermission, permissions]
  );

  return { isFeatureEnabled };
};
