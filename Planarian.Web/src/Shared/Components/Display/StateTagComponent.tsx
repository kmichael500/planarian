import { Spin } from "antd";
import { PresetColorType, PresetStatusColorType } from "antd/es/_util/colors";
import { LiteralUnion } from "antd/es/_util/type";
import { useEffect, useState } from "react";
import { SettingsService } from "../../../Modules/Setting/Services/SettingsService";
import { SelectListItem } from "../../Models/SelectListItem";

export interface StateTagComponentProps {
  stateId?: string;
  item?: SelectListItem<string>;
  color?: LiteralUnion<PresetColorType | PresetStatusColorType>;
}

const StateTagComponent: React.FC<StateTagComponentProps> = (props) => {
  let [stateName, setStateName] = useState<string>();
  let [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    if (props.item) {
      setIsLoading(false);
      return;
    }

    if (!props.stateId) {
      setStateName(undefined);
      setIsLoading(false);
      return;
    }

    const getStateName = async () => {
      try {
        const countyNameResponse = await SettingsService.GetStateName(
          props.stateId
        );
        setStateName(countyNameResponse);
      } catch (error) {
        setStateName("Error");
      }
      setIsLoading(false);
    };
    getStateName();
  }, [props.stateId, props.item]);

  return (
    <Spin spinning={!props.item && isLoading}>
      {props.item?.display ?? stateName}
    </Spin>
  );
};

export { StateTagComponent };
