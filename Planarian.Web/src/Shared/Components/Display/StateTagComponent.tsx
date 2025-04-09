import { Spin, Tag } from "antd";
import { PresetColorType, PresetStatusColorType } from "antd/es/_util/colors";
import { LiteralUnion } from "antd/es/_util/type";
import { useEffect, useState } from "react";
import { SettingsService } from "../../../Modules/Setting/Services/SettingsService";

export interface StateTagComponentProps {
  stateId?: string;
  color?: LiteralUnion<PresetColorType | PresetStatusColorType>;
}

const StateTagComponent: React.FC<StateTagComponentProps> = (props) => {
  let [stateName, setStateName] = useState<string>();
  let [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
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
  }, [props.stateId]);

  return <Spin spinning={isLoading}>{stateName}</Spin>;
};

export { StateTagComponent };
