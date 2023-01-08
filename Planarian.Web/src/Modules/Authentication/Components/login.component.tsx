import { Form, Card, Input, Checkbox, Button, message } from "antd";
import { Link, useNavigate } from "react-router-dom";
import { nameof } from "../../../Shared/Helpers/StringHelpers";

import { UserLoginVm } from "../Models/UserLoginVm";
import { AuthenticationService } from "../Services/authentication.service";

const LoginComponent: React.FC = () => {
  const [form] = Form.useForm<UserLoginVm>();

  const navigate = useNavigate();

  const onSubmit = async (values: UserLoginVm) => {
    try {
      await AuthenticationService.Login(values);
      navigate("../projects");
    } catch (e: any) {
      message.error(e.message);
    }
  };

  return (
    <Card
      title="Login"
      actions={[
        <Button type="primary" onClick={(e) => form.submit()}>
          Login
        </Button>,
        <Link to={"../register"}>
          <Button>Register</Button>
        </Link>,
      ]}
    >
      <Form
        form={form}
        name="basic"
        layout="vertical"
        initialValues={{ remember: true }}
        onFinish={onSubmit}
        autoComplete="off"
      >
        <Form.Item
          label="Email Address"
          name={nameof<UserLoginVm>("emailAddress")}
          rules={[
            { required: true, message: "Please enter your email address!" },
          ]}
        >
          <Input />
        </Form.Item>

        <Form.Item
          label="Password"
          name={nameof<UserLoginVm>("password")}
          rules={[{ required: true, message: "Please enter your password!" }]}
        >
          <Input.Password />
        </Form.Item>

        <Form.Item name="remember" valuePropName="checked">
          <Checkbox>Remember me</Checkbox>
        </Form.Item>

        <Form.Item></Form.Item>
      </Form>
    </Card>
  );
};

export { LoginComponent };
