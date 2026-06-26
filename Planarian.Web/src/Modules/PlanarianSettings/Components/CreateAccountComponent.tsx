import { Card, Checkbox, Form, Input, Select, Space, message } from "antd";
import { useEffect, useState } from "react";
import { SaveButtonComponent } from "../../../Shared/Components/Buttons/SaveButtonComponent";
import { PropertyLength } from "../../../Shared/Constants/PropertyLengthConstant";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";
import { AccountService } from "../../Account/Services/AccountService";
import { AuthenticationService } from "../../Authentication/Services/AuthenticationService";
import { CreateAccountVm } from "../Models/CreateAccountVm";
import { PlanarianSettingsService } from "../Services/PlanarianSettingsService";

const CreateAccountComponent = () => {
  const [form] = Form.useForm<CreateAccountVm>();
  const [states, setStates] = useState<SelectListItem<string>[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    const loadStates = async () => {
      setIsLoading(true);
      try {
        setStates(await AccountService.GetAllStates());
      } catch (error: any) {
        message.error(error?.message ?? "Failed to load states.");
      } finally {
        setIsLoading(false);
      }
    };

    loadStates();
  }, []);

  const onFinish = async (values: CreateAccountVm) => {
    setIsSubmitting(true);
    try {
      const accountId = await PlanarianSettingsService.CreateAccount({
        name: values.name.trim(),
        countyIdDelimiter: values.countyIdDelimiter?.trim(),
        stateIds: values.stateIds,
        defaultViewAccessAllCaves: values.defaultViewAccessAllCaves,
        exportEnabled: values.exportEnabled,
      });
      AuthenticationService.SwitchAccount(accountId, "/caves");
      message.success("Account created.");
      form.resetFields();
    } catch (error: any) {
      message.error(error?.message ?? "Failed to create account.");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Card title="Create Account" loading={isLoading}>
      <Form
        form={form}
        layout="vertical"
        onFinish={onFinish}
        initialValues={{
          stateIds: [],
          defaultViewAccessAllCaves: false,
          exportEnabled: false,
        }}
        style={{ maxWidth: 520 }}
      >
        <Form.Item
          label="Name"
          name="name"
          rules={[
            { required: true, whitespace: true, message: "Name is required." },
            {
              max: PropertyLength.NAME,
              message: `Name cannot be longer than ${PropertyLength.NAME} characters.`,
            },
          ]}
        >
          <Input maxLength={PropertyLength.NAME} />
        </Form.Item>
        <Form.Item
          label="Delimiter"
          name="countyIdDelimiter"
          rules={[
            {
              max: PropertyLength.DELIMITER,
              message: `Delimiter cannot be longer than ${PropertyLength.DELIMITER} characters.`,
            },
          ]}
        >
          <Input maxLength={PropertyLength.DELIMITER} />
        </Form.Item>
        <Form.Item
          label="States"
          name="stateIds"
          rules={[
            { required: true, message: "Please select at least one state." },
          ]}
        >
          <Select
            mode="multiple"
            placeholder="Select states"
            options={states.map((state) => ({
              label: state.display,
              value: state.value,
            }))}
          />
        </Form.Item>
        <Form.Item name="defaultViewAccessAllCaves" valuePropName="checked">
          <Checkbox>Default Cave Visibility: All</Checkbox>
        </Form.Item>
        <Form.Item name="exportEnabled" valuePropName="checked">
          <Checkbox>Export Enabled</Checkbox>
        </Form.Item>
        <Space wrap>
          <SaveButtonComponent
            htmlType="submit"
            loading={isSubmitting}
            alwaysShowChildren
          />
        </Space>
      </Form>
    </Card>
  );
};

export { CreateAccountComponent };
