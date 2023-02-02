import { Form, Input, Modal } from "antd";
import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { AddButtonComponent } from "../../../Shared/Components/Buttons/AddButtonComponent";
import { PropertyLength } from "../../../Shared/Constants/PropertyLengthConstant";
import { CreateOrEditProject } from "../Models/CreateOrEditProject";
import { ProjectService } from "../Services/ProjectService";

const ProjectCreateButtonComponent: React.FC = () => {
  const [open, setOpen] = useState(false);
  const [confirmLoading, setConfirmLoading] = useState(false);
  const [form] = Form.useForm();
  const navigate = useNavigate();

  const showModal = (): void => {
    setOpen(true);
  };

  const handleCancel = (): void => {
    form.resetFields();
    setOpen(false);
  };

  const onSubmit = async (values: CreateOrEditProject): Promise<void> => {
    setConfirmLoading(true);
    const newProject = await ProjectService.CreateProject(values);
    setOpen(false);
    setConfirmLoading(false);
    navigate(`${newProject.id}`);
  };

  return (
    <>
      <AddButtonComponent type="primary" onClick={showModal} />
      <Modal
        title="New Project"
        open={open}
        onOk={form.submit}
        confirmLoading={confirmLoading}
        onCancel={handleCancel}
      >
        <Form
          form={form}
          labelCol={{ span: 4 }}
          wrapperCol={{ span: 14 }}
          layout="horizontal"
          onFinish={onSubmit}
        >
          <Form.Item
            required
            name={"Name"}
            label="Name"
            rules={[{ required: true, message: "'Name' is required" }]}
          >
            <Input maxLength={PropertyLength.NAME} />
          </Form.Item>
        </Form>{" "}
      </Modal>
    </>
  );
};

export { ProjectCreateButtonComponent };
