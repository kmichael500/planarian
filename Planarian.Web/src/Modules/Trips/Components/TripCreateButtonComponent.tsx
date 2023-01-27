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
import React, { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { PropertyLength } from "../../../Shared/Constants";
import { SettingsService } from "../../Settings/Services/settings.service";
import { ProjectService } from "../../Project/Services/project.service";
import { CreateOrEditTripVm } from "../Models/CreateOrEditTripVm";
import { TripService } from "../Services/TripService";
import { nameof } from "../../../Shared/Helpers/StringHelpers";

interface TripCreateButtonProps {
  projectId: string;
}
const TripCreateButtonComponent: React.FC<TripCreateButtonProps> = (
  props: TripCreateButtonProps
) => {
  const [open, setOpen] = useState(false);
  const [confirmLoading, setConfirmLoading] = useState(false);
  const [projectMembers, setProjectMembers] =
    useState<SelectProps["options"]>();
  const [tripTags, setTripTags] = useState<SelectProps["options"]>();

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
    if (tripTags == undefined) {
      const GetTripTags = async (): Promise<void> => {
        const response = await SettingsService.GetTripTags();

        setTripTags(
          response.map(
            (e) => ({ value: e.value, label: e.display } as DefaultOptionType)
          )
        );
      };
      GetTripTags();
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

  const onSubmit = async (values: CreateOrEditTripVm): Promise<void> => {
    values.projectId = props.projectId;

    setConfirmLoading(true);
    const trip = await TripService.AddTrip(values);

    setOpen(false);
    setConfirmLoading(false);
    navigate(`/projects/${props.projectId}/trip/${trip.id}`);
  };

  return (
    <>
      <Button type="primary" onClick={showModal}>
        New Trip
      </Button>
      <Modal
        title="New Trip"
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
            name={nameof<CreateOrEditTripVm>("name")}
            label="Name"
            rules={[{ required: true, message: "'Name' is required" }]}
          >
            <Input maxLength={PropertyLength.NAME} />
          </Form.Item>
          <Form.Item
            required
            name={nameof<CreateOrEditTripVm>("tripTagIds")}
            label="Tags"
            rules={[
              {
                required: true,
                message: "At least one 'Tag' is required",
              },
            ]}
          >
            <Select
              options={tripTags}
              mode="tags"
              placeholder="Please select"
              style={{ width: "100%" }}
            />
          </Form.Item>
          <Form.Item
            name={nameof<CreateOrEditTripVm>("tripMemberIds")}
            label="Team"
          >
            <Select
              options={projectMembers}
              mode="tags"
              placeholder="Please select"
              style={{ width: "100%" }}
            />
          </Form.Item>
          <Form.Item name="Description" label="Description">
            <TextArea
              placeholder="Write a 1-3 line summary of your trip goals. This is not the trip report."
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

export { TripCreateButtonComponent };
