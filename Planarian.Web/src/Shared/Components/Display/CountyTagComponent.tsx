import { Spin, Tag } from "antd";
import { PresetColorType, PresetStatusColorType } from "antd/es/_util/colors";
import { LiteralUnion } from "antd/es/_util/type";
import { useEffect, useState } from "react";
import { SettingsService } from "../../../Modules/Setting/Services/SettingsService";

export interface CountyTagComponentProps {
  countyId?: string;
  color?: LiteralUnion<PresetColorType | PresetStatusColorType, string>;
}

const CountyTagComponent: React.FC<CountyTagComponentProps> = (props) => {
  let [countyName, setCountyName] = useState<string>();
  let [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const getCountyName = async () => {
      try {
        const countyNameResponse = await SettingsService.GetCountyName(
          props.countyId
        );
        setCountyName(countyNameResponse);
      } catch (error) {
        setCountyName("Error");
      }
      setIsLoading(false);
    };
    getCountyName();
  }, [props.countyId]);

  return <Spin spinning={isLoading}>{countyName}</Spin>;
};

export { CountyTagComponent };
