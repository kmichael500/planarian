import { InputNumber, InputNumberProps } from "antd";

const InputDistanceComponent = (props: InputNumberProps) => {
  return (
    <InputNumber
      addonAfter={"ft"}
      style={{ width: "100%" }}
      min={0}
      {...props}
    />
  );
};

export { InputDistanceComponent };
