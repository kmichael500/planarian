import { Spin, Tag } from "antd";
import { PresetColorType, PresetStatusColorType } from "antd/es/_util/colors";
import { LiteralUnion } from "antd/es/_util/type";
import { useState, useEffect } from "react";
import { TripDetailComponent } from "../../Modules/Trip/Components/trip.detail.component";
import { SettingsService } from "../../Modules/Settings/Services/settings.service";

export interface TripObjectiveTagComponentProps {
  tripObjectiveId: string;
  color?: LiteralUnion<PresetColorType | PresetStatusColorType, string>;
}
const TripObjectiveTagComponent: React.FC<TripObjectiveTagComponentProps> = (
  props
) => {
  let [objectiveTypeName, setObjectiveTypeName] = useState<string>();
  let [isObjectiveLoading, setIsObjectiveLoading] = useState(true);

  useEffect(() => {
    if (objectiveTypeName === undefined) {
      const getTripObjectiveName = async () => {
        const tripObjectiveNameResponse =
          await SettingsService.GetObjectiveTypeName(props.tripObjectiveId);
        setObjectiveTypeName(tripObjectiveNameResponse);
        setIsObjectiveLoading(false);
      };
      getTripObjectiveName();
    }
  });

  return (
    <Spin spinning={isObjectiveLoading}>
      <Tag key={props.tripObjectiveId} color={props.color}>
        {objectiveTypeName}
      </Tag>
    </Spin>
  );
};

export { TripObjectiveTagComponent };
