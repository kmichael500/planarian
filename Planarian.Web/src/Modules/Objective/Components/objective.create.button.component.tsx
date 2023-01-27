import {
  Button,
  Modal,
  Input,
  Form,
  Select,
  SelectProps,
  Row,
  Col,
} from "antd";
import TextArea from "antd/lib/input/TextArea";
import { DefaultOptionType } from "antd/lib/select";
import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { PropertyLength } from "../../../Shared/Constants";
import { SettingsService } from "../../Settings/Services/settings.service";
import { ProjectService } from "../../Project/Services/project.service";
import { CreateOrEditTripObjectiveVm } from "../Models/CreateOrEditTripObjectiveVm";
import { TripObjectiveService } from "../Services/trip.objective.service";

interface IObjectiveCreateButtonPRops {
  projectId: string;
}
const TripObjectiveCreateButton: React.FC<IObjectiveCreateButtonPRops> = (
  props: IObjectiveCreateButtonPRops
) => {
  const [open, setOpen] = useState(false);
  const [confirmLoading, setConfirmLoading] = useState(false);
  const [projectMembers, setProjectMembers] =
    useState<SelectProps["options"]>();
  const [tripObjectiveTypes, setTripObjectiveTypes] =
    useState<SelectProps["options"]>();

  const [form] = Form.useForm();

  useEffect(() => {
    if (projectMembers === undefined) {
      const GetProjectMembers = async (): Promise<void> => {
        const response = await ProjectService.GetProjectMembers(
          props.projectId
        );

        setProjectMembers(
          response.map(
            (e) => ({ value: e.value, label: e.display } as DefaultOptionType)
          )
        );
      };
      GetProjectMembers();
    }
    if (tripObjectiveTypes == undefined) {
      const GetTripObjectiveTypes = async (): Promise<void> => {
        const response = await SettingsService.GetTripObjectiveTypes();

        setTripObjectiveTypes(
          response.map(
            (e) => ({ value: e.value, label: e.display } as DefaultOptionType)
          )
        );
      };
      GetTripObjectiveTypes();
    }
  });

  const navigate = useNavigate();

  const showModal = (): void => {
    setOpen(true);
  };

  const handleCancel = (): void => {
    form.resetFields();
    setOpen(false);
  };

  const onSubmit = async (
    values: CreateOrEditTripObjectiveVm
  ): Promise<void> => {
    values.projectId = props.projectId;

    setConfirmLoading(true);
    const objective = await TripObjectiveService.AddTripObjective(values);

    setOpen(false);
    setConfirmLoading(false);
    navigate(`/projects/${props.projectId}/trip/${objective.id}`);
  };

  return (
    <>
      <Button type="primary" onClick={showModal}>
        New Objective
      </Button>
      <Modal
        title="New Objective"
        open={open}
        onOk={form.submit}
        confirmLoading={confirmLoading}
        okText="Create"
        bodyStyle={{ width: "100%" }}
        onCancel={handleCancel}
        style={{ width: "100%" }}
      >
        <Form
          form={form}
          labelCol={{ span: 4 }}
          wrapperCol={{ span: 14 }}
          layout="vertical"
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
          <Form.Item
            required
            name="TripObjectiveTypeIds"
            label="Objective Type"
            rules={[
              {
                required: true,
                message: "At least one 'Objective Type' is required",
              },
            ]}
          >
            <Select
              options={tripObjectiveTypes}
              mode="tags"
              placeholder="Please select"
              style={{ width: "100%" }}
            />
          </Form.Item>
          <Form.Item name="TripObjectiveMembers" label="Team">
            <Select
              options={projectMembers}
              mode="tags"
              placeholder="Please select"
              style={{ width: "100%" }}
            />
          </Form.Item>
          <Form.Item name="Description" label="Description">
            <TextArea
              placeholder="Write a 1-3 line summary of your objective goals. This is not the trip report."
              rows={4}
              maxLength={PropertyLength.MEDIUM_TEXT}
            />
          </Form.Item>
        </Form>
      </Modal>
    </>
  );
};

function getTimezoneName() {
  const today = new Date();
  const short = today.toLocaleDateString(undefined);
  const full = today.toLocaleDateString(undefined, { timeZoneName: "long" });

  // Trying to remove date from the string in a locale-agnostic way
  const shortIndex = full.indexOf(short);
  if (shortIndex >= 0) {
    const trimmed =
      full.substring(0, shortIndex) + full.substring(shortIndex + short.length);

    // by this time `trimmed` should be the timezone's name with some punctuation -
    // trim it from both sides
    return trimmed.replace(/^[\s,.\-:;]+|[\s,.\-:;]+$/g, "");
  } else {
    // in some magic case when short representation of date is not present in the long one, just return the long one as a fallback, since it should contain the timezone's name
    return full;
  }
}

export { TripObjectiveCreateButton };
