import { useEffect, useState } from "react";
import { Card, Checkbox, Form, Input, Select, message } from "antd";
import { AccountService } from "../Services/AccountService";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";
import { SaveButtonComponent } from "../../../Shared/Components/Buttons/SaveButtonComponent";
import { nameof } from "../../../Shared/Helpers/StringHelpers";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { PropertyLength } from "../../../Shared/Constants/PropertyLengthConstant";

const { Option } = Select;

export interface MiscAccountSettingsVm {
  accountName: string;
  countyIdDelimiter: string;
  stateIds: string[];
  defaultViewAccessAllCaves: boolean;
}

export interface MiscAccountSettingsProps {
  onStatesChange?: (stateIds: string[]) => void;
}

const MiscAccountSettings = ({
  onStatesChange: onCountiesChange,
}: MiscAccountSettingsProps) => {
  const [states, setStates] = useState<SelectListItem<string>[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [form] = Form.useForm<MiscAccountSettingsVm>();

  useEffect(() => {
    const fetchStates = async () => {
      try {
        setIsLoading(true);
        const statesData = await AccountService.GetAllStates();
        setStates(statesData);
        setIsLoading(false);
      } catch (error) {
        console.error("Error fetching states:", error);
      }
    };

    fetchStates();
  }, []);

  useEffect(() => {
    const fetchData = async () => {
      try {
        const settings = await AccountService.GetSettings();
        // Set initial form values
        form.setFieldsValue(settings);
      } catch (error) {
        message.error("Error fetching settings.");
      }
    };

    fetchData();
  }, []);

  const onFinish = async (values: MiscAccountSettingsVm) => {
    try {
      await AccountService.UpdateSettings(values);
      message.success("Settings updated successfully!");
      if (onCountiesChange) {
        onCountiesChange(values.stateIds);
      }
    } catch (err) {
      const error = err as ApiErrorResponse;
      message.error(error.message);
    }
  };

  return (
    <Card title="Account Settings" loading={isLoading}>
      <Form
        form={form}
        onFinish={onFinish}
        layout="vertical"
        initialValues={{ name: "", delimiter: "", states: [] }}
      >
        <Form.Item
          name={nameof<MiscAccountSettingsVm>("accountName")}
          label="Name"
          rules={[{ required: true, message: "Please enter the name!" }]}
          help="This is the name of the state survey."
        >
          <Input />
        </Form.Item>
        <Form.Item
          name={nameof<MiscAccountSettingsVm>("countyIdDelimiter")}
          label="Delimiter"
          help="This is the delimiter used to separate the county ID from the county cave number."
        >
          <Input maxLength={PropertyLength.DELIMITER} />
        </Form.Item>
        <Form.Item
          name={nameof<MiscAccountSettingsVm>("stateIds")}
          label="Select States"
          rules={[
            { required: true, message: "Please select at least one state!" },
          ]}
          help="These are states available for data entry in the state survey. You can only remove states that have no caves associated with them."
        >
          <Select
            mode="multiple"
            placeholder="Please select"
            filterOption={(input, option) =>
              option && option.children
                ? option.children
                    .toString()
                    .toLowerCase()
                    .includes(input.toLowerCase())
                : false
            }
          >
            {states.map((state) => (
              <Option key={state.value} value={state.value}>
                {state.display}
              </Option>
            ))}
          </Select>
        </Form.Item>

        <Form.Item
          name={nameof<MiscAccountSettingsVm>("defaultViewAccessAllCaves")}
          valuePropName="checked"
          help="If enabled, all new users will automatically be granted view access to every cave location upon invitation."
        >
          <Checkbox>Default Cave Visibility: All</Checkbox>
        </Form.Item>

        <Form.Item>
          <SaveButtonComponent type="primary" htmlType="submit">
            Save
          </SaveButtonComponent>
        </Form.Item>
      </Form>
    </Card>
  );
};

export { MiscAccountSettings };
