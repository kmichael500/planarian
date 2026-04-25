import { Spin } from "antd";
import { PresetColorType, PresetStatusColorType } from "antd/es/_util/colors";
import { LiteralUnion } from "antd/es/_util/type";
import { useEffect, useState } from "react";
import { SettingsService } from "../../../Modules/Setting/Services/SettingsService";
import { SelectListItem } from "../../Models/SelectListItem";

export interface CountyTagComponentProps {
  countyId?: string;
  item?: SelectListItem<string>;
  color?: LiteralUnion<PresetColorType | PresetStatusColorType>;
}

const CountyTagComponent: React.FC<CountyTagComponentProps> = (props) => {
  let [countyName, setCountyName] = useState<string>();
  let [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    if (props.item) {
      setIsLoading(false);
      return;
    }

    if (!props.countyId) {
      setCountyName(undefined);
      setIsLoading(false);
      return;
    }

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
  }, [props.countyId, props.item]);

  return (
    <Spin spinning={!props.item && isLoading}>
      {props.item?.display ?? countyName}
    </Spin>
  );
};

export { CountyTagComponent };
