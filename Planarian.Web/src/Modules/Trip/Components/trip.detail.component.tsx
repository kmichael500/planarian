// import {
//   Row,
//   Col,
//   Button,
//   Divider,
//   Spin,
//   Card,
//   Typography,
//   DatePicker,
// } from "antd";
// import moment from "moment";
// import { Fragment, useEffect, useState } from "react";
// import { Link, useParams } from "react-router-dom";
// import {
//   MemberGridComponent,
//   MemberGridType,
// } from "../../../Shared/Components/MemberGridComponent";
// import { TripObjectiveTagComponent } from "../../../Shared/Components/TripObjectiveTagComponent";
// import { UserAvatarGroupComponent } from "../../User/Componenets/UserAvatarGroupComponent";
// import { NotFoundException } from "../../../Shared/Exceptions/NotFoundException";
// import { isNullOrWhiteSpace } from "../../../Shared/Helpers/StringHelpers";
// import { TripObjectiveCreateButton } from "../../Objective/Components/objective.create.button.component";
// import { TripObjectiveVm } from "../../Objective/Models/TripObjectiveVm";
// import { TripVm } from "../Models/TripVm";
// import { TripSerice } from "../Services/trip.service";
// const { Title, Text } = Typography;

// const TripDetailComponent: React.FC = () => {
//   let [trip, setTrip] = useState<TripVm>();
//   const { tripId, projectId } = useParams();
//   if (tripId === undefined) {
//     throw new NotFoundException();
//   }
//   if (projectId === undefined) {
//     throw new NotFoundException();
//   }

//   let [objectives, setObjectives] = useState<TripObjectiveVm[]>();
//   let [isLoading, setIsLoading] = useState(true);

//   useEffect(() => {
//     if (trip === undefined) {
//       const getTrip = async () => {
//         const tripResponse = await TripSerice.GetTrip(tripId);
//         setTrip(tripResponse);
//         const tripObjectivesResponse = await TripSerice.GetObjectives(tripId);

//         setObjectives(tripObjectivesResponse);
//         setIsLoading(false);
//       };
//       getTrip();
//     }
//   });

//   const hanldeNameChange = async (e: string) => {
//     if (trip == null || tripId == null || isNullOrWhiteSpace(e)) return;
//     await TripSerice.UpdateTripName(e, tripId);
//     setTrip({ ...trip, name: e });
//   };

//   const handleDateChange = async (date: moment.Moment | null) => {
//     if (date === null || trip == null) {
//       return;
//     }
//     await TripSerice.UpdateDate(tripId, date.toDate());
//     setTrip({ ...trip, tripDate: date.toISOString() });
//   };

//   return (
//     <Fragment>
//       <Spin spinning={isLoading}>
//         <Row align="top" gutter={10}>
//           <Col>
//             <Title level={2}>
//               Trip {trip?.tripNumber}{" "}
//               <span>
//                 <Text
//                   type="secondary"
//                   editable={{ onChange: hanldeNameChange }}
//                 >
//                   {trip?.name}
//                 </Text>
//               </span>
//             </Title>
//             <DatePicker
//               value={moment(trip?.tripDate)}
//               onChange={handleDateChange}
//             />
//           </Col>
//           {/* take up rest of space to push others to right and left side */}
//           <Col flex="auto"></Col>
//           <Col>
//             <Link to={"./../.."}>
//               <Button>Back</Button>
//             </Link>
//           </Col>
//           <Col>
//             {" "}
//             <TripObjectiveCreateButton tripId={tripId} projectId={projectId} />
//           </Col>
//         </Row>
//         <Divider />

//         <MemberGridComponent
//           type={MemberGridType.Trip}
//           tripId={tripId}
//           projectId={projectId}
//         ></MemberGridComponent>
//         <Divider></Divider>

//         <Row align="middle">
//           <Col>
//             <Title level={3}>Objectives</Title>
//           </Col>
//           {/* take up rest of space to push others to right and left side */}
//           <Col flex="auto"></Col>
//           <Col> {/* <TripCreateButton projectId={projectId} /> */}</Col>
//         </Row>
//         <Row
//           gutter={[
//             { xs: 8, sm: 8, md: 24, lg: 32 },
//             { xs: 8, sm: 8, md: 24, lg: 32 },
//           ]}
//         >
//           {objectives?.map((objective, index) => (
//             <Col key={objective.id} xs={24} sm={12} md={8} lg={6}>
//               <Card
//                 title={
//                   <>
//                     {objective.name}{" "}
//                     <Row>
//                       <UserAvatarGroupComponent
//                         size={"small"}
//                         maxCount={4}
//                         userIds={objective.tripObjectiveMemberIds}
//                       />
//                     </Row>
//                   </>
//                 }
//                 loading={isLoading}
//                 bordered={false}
//                 actions={[
//                   <Link to={`objective/${objective.id}`}>
//                     <Button>View</Button>
//                   </Link>,
//                 ]}
//               >
//                 <>
//                   <Row>
//                     {objective.tripObjectiveTypeIds.map(
//                       (objectiveTypeId, index) => (
//                         <Col>
//                           <TripObjectiveTagComponent
//                             key={index}
//                             tripObjectiveId={objectiveTypeId}
//                           />
//                         </Col>
//                       )
//                     )}
//                   </Row>
//                   <Divider />

//                   <Row>
//                     {" "}
//                     <Text>Description: {objective.description}</Text>
//                   </Row>
//                 </>
//               </Card>
//             </Col>
//           ))}
//         </Row>
//       </Spin>
//     </Fragment>
//   );
// };

// export { TripDetailComponent };

export {};
