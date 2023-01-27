// import { Button, DatePicker, Form, Input, Modal } from "antd";
// import { ReactComponentElement, useState } from "react";
// import { useNavigate } from "react-router-dom";
// import { PropertyLength } from "../../../Shared/Constants";
// import { CreateOrEditTripVm } from "../Models/CreateOrEditTrip";
// import { TripSerice as TripService } from "../Services/trip.service";

// interface ITripCreateButtonPRops {
//   projectId: string;
// }
// const TripCreateButton: React.FC<ITripCreateButtonPRops> = (
//   props: ITripCreateButtonPRops
// ) => {
//   const [open, setOpen] = useState(false);
//   const [confirmLoading, setConfirmLoading] = useState(false);
//   const [form] = Form.useForm();

//   const navigate = useNavigate();

//   const showModal = (): void => {
//     setOpen(true);
//   };

//   const handleCancel = (): void => {
//     form.resetFields();
//     setOpen(false);
//   };

//   const onSubmit = async (values: CreateOrEditTripVm): Promise<void> => {
//     values.projectId = props.projectId;
//     setConfirmLoading(true);
//     const newTrip = await TripService.AddTrip(values);

//     setOpen(false);
//     setConfirmLoading(false);
//     navigate(`/projects/${props.projectId}/trip/${newTrip.id}`);
//   };

//   return (
//     <>
//       <Button type="primary" onClick={showModal}>
//         New Trip
//       </Button>
//       <Modal
//         title="New Trip"
//         open={open}
//         onOk={form.submit}
//         confirmLoading={confirmLoading}
//         onCancel={handleCancel}
//       >
//         <Form
//           form={form}
//           labelCol={{ span: 4 }}
//           wrapperCol={{ span: 14 }}
//           layout="horizontal"
//           onFinish={onSubmit}
//         >
//           <Form.Item
//             required
//             name={"Name"}
//             label="Name"
//             rules={[{ required: true, message: "'Name' is required" }]}
//           >
//             <Input maxLength={PropertyLength.NAME} />
//           </Form.Item>
//           <Form.Item
//             label="Trip Date"
//             name={"tripDate"}
//             rules={[{ required: true, message: "'Trip Date' is required" }]}
//           >
//             <DatePicker format="YYYY-MM-DD hh:mm:ss A Z" />
//           </Form.Item>
//         </Form>
//       </Modal>
//     </>
//   );
// };

// function getTimezoneName() {
//   const today = new Date();
//   const short = today.toLocaleDateString(undefined);
//   const full = today.toLocaleDateString(undefined, { timeZoneName: "long" });

//   // Trying to remove date from the string in a locale-agnostic way
//   const shortIndex = full.indexOf(short);
//   if (shortIndex >= 0) {
//     const trimmed =
//       full.substring(0, shortIndex) + full.substring(shortIndex + short.length);

//     // by this time `trimmed` should be the timezone's name with some punctuation -
//     // trim it from both sides
//     return trimmed.replace(/^[\s,.\-:;]+|[\s,.\-:;]+$/g, "");
//   } else {
//     // in some magic case when short representation of date is not present in the long one, just return the long one as a fallback, since it should contain the timezone's name
//     return full;
//   }
// }

// export { TripCreateButton };
export {};
