import { useEffect, useState } from "react";
import { CountyNumberIntent } from "../Models/CaveChangeRequestVm";
import { CaveSnapshotVm } from "../Models/CaveRevisionVm";
import { CaveService } from "../Service/CaveService";

interface CountyNumberPreview {
  base: CaveSnapshotVm;
  proposed: CaveSnapshotVm;
  countyNumberIntent: CountyNumberIntent;
  requestedCountyNumber?: number;
  likelyCountyNumber?: number;
}

export const useLikelyCountyNumber = (preview?: CountyNumberPreview) => {
  const [fallback, setFallback] = useState<number>();

  useEffect(() => {
    let active = true;
    setFallback(undefined);
    if (!preview || preview.likelyCountyNumber != null) return () => { active = false; };
    if (preview.countyNumberIntent === "Manual") {
      setFallback(preview.requestedCountyNumber);
      return () => { active = false; };
    }
    if (preview.proposed.county.id === preview.base.county.id) {
      setFallback(preview.base.countyNumber);
      return () => { active = false; };
    }

    void CaveService.GetNextCountyNumber(
      preview.proposed.county.id,
      preview.countyNumberIntent === "FirstAvailable"
    ).then(value => {
      if (active) setFallback(value);
    }).catch(() => {
      if (active) setFallback(undefined);
    });

    return () => { active = false; };
  }, [preview]);

  return preview?.likelyCountyNumber ?? fallback;
};
