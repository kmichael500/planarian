import { Button, Col, Form, Input, message, Modal, Row, Select } from "antd";
import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { MemberGridType } from "./MemberGridComponent";
import { MailOutlined, PlusCircleOutlined } from "@ant-design/icons";
import { SelectListItem } from "../Models/SelectListItem";
import { ProjectService } from "../../Modules/Project/Services/ProjectService";
import { SettingsService } from "../../Modules/Setting/Services/SettingsService";
import { nameof } from "../Helpers/StringHelpers";
import { AddMember } from "../Models/AddMember";
import { InviteMember as InviteMember } from "../Models/InviteMember";
import { TripService } from "../../Modules/Trip/Services/TripService";

interface MemberGridAddMemberComponentProps {
  type: MemberGridType;
  projectId?: string;
  tripId?: string;
  onAddedSuccess?: () => Promise<void>;
}

const MemberGridAddMemberComponent: React.FC<
  MemberGridAddMemberComponentProps
> = (props) => {
  const [open, setOpen] = useState(false);
  const [confirmLoading, setConfirmLoading] = useState(false);
  const [members, setMembers] = useState<SelectListItem<string>[]>();

  const [isAddingNewMember, setIsAddingNewMember] = useState<boolean>(false);

  const [selectExistingForm] = Form.useForm();
  const [addNewMemberForm] = Form.useForm();

  const navigate = useNavigate();

  useEffect(() => {
    if (members === undefined) {
      const getProjectMembers = async () => {
        const projectmembers = await ProjectService.GetProjectMembers(
          props.projectId as string
        );
        setMembers(projectmembers);
      };

      const GetUsers = async () => {
        const users = await SettingsService.GetUsers();
        setMembers(users);
      };
      if (props.type === MemberGridType.Project) {
        GetUsers();
      } else {
        getProjectMembers();
      }
    }
  });

  const showModal = (): void => {
    setOpen(true);
  };

  const addExistingMember = () => {
    showModal();
  };

  const inviteMember = () => {
    setIsAddingNewMember(true);
    selectExistingForm.resetFields();
    showModal();
  };

  const handleCancel = (): void => {
    setOpen(false);
    setConfirmLoading(false);
    selectExistingForm.resetFields();
    addNewMemberForm.resetFields();
    setIsAddingNewMember(false);
  };

  const onSubmit = async (values: AddMember): Promise<void> => {
    setConfirmLoading(true);
    const userIds = values.ids.map((x) => x.value);
    switch (props.type) {
      case MemberGridType.Project:
        await ProjectService.AddProjectMembers(
          userIds,
          props.projectId as string
        );
        break;
      case MemberGridType.Trip:
        await TripService.AddTripMembers(userIds, props.tripId as string);
        break;
    }

    message.success(`${props.type} Member added`);
    await afterSubmit();
  };

  const onInviteMember = async (values: InviteMember): Promise<void> => {
    setConfirmLoading(true);

    switch (props.type) {
      case MemberGridType.Project:
        await ProjectService.InviteProjectMembers(
          values,
          props.projectId as string
        );
        break;
      case MemberGridType.Trip:
        await TripService.InviteTripMembers(values, props.tripId as string);
        break;
    }
    message.success(`${props.type} invitation sent`);
    await afterSubmit();
  };

  const afterSubmit = async () => {
    await props.onAddedSuccess?.();

    setOpen(false);
    setConfirmLoading(false);
    addNewMemberForm.resetFields();
    selectExistingForm.resetFields();
  };

  return (
    <>
      <Row gutter={10}>
        <Col>
          <Button icon={<MailOutlined />} onClick={inviteMember}>
            Invite
          </Button>
        </Col>
        <Col>
          <Button
            type="primary"
            icon={<PlusCircleOutlined />}
            onClick={addExistingMember}
          >
            Add
          </Button>
        </Col>
      </Row>

      <Modal
        title={
          isAddingNewMember
            ? `Invite ${props.type} Member`
            : `${props.type} Member`
        }
        open={open}
        okText={isAddingNewMember ? "Invite" : "Add"}
        onOk={() => {
          if (!isAddingNewMember) {
            selectExistingForm.submit();
          } else {
            addNewMemberForm.submit();
          }
        }}
        confirmLoading={confirmLoading}
        onCancel={handleCancel}
      >
        {!isAddingNewMember && (
          <Form
            form={selectExistingForm}
            labelCol={{ span: 4 }}
            wrapperCol={{ span: 14 }}
            layout="vertical"
            onFinish={onSubmit}
          >
            <Form.Item
              required
              name={nameof<AddMember>("ids")}
              label="Name"
              rules={[{ required: true, message: "'Name' is required" }]}
            >
              <Select
                placeholder="Select"
                showSearch
                labelInValue
                mode="multiple"
                notFoundContent={
                  <Button
                    onClick={(e) => {
                      inviteMember();
                    }}
                  >
                    Invite Member
                  </Button>
                }
              >
                {members?.map((member) => {
                  return (
                    <Select.Option key={member.value} value={member.value}>
                      {member.display}
                    </Select.Option>
                  );
                })}
              </Select>
            </Form.Item>
          </Form>
        )}
        {isAddingNewMember && (
          <>
            <Form
              layout="vertical"
              form={addNewMemberForm}
              onFinish={onInviteMember}
            >
              <Form.Item
                name="firstName"
                label="First Name"
                rules={[
                  { required: true, message: "Please input your first name!" },
                ]}
              >
                <Input />
              </Form.Item>
              <Form.Item
                name="lastName"
                label="Last Name"
                rules={[
                  { required: true, message: "Please input your last name!" },
                ]}
              >
                <Input />
              </Form.Item>
              <Form.Item
                name="email"
                label="Email"
                rules={[
                  { required: true, message: "Please input your email!" },
                  {
                    type: "email",
                    message: "The input is not a valid email address!",
                  },
                ]}
              >
                <Input />
              </Form.Item>
            </Form>
          </>
        )}
      </Modal>
    </>
  );
};

export { MemberGridAddMemberComponent };
