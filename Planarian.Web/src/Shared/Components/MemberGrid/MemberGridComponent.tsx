import { Card, Space, Table } from "antd";
import { ColumnsType } from "antd/lib/table";
import { useEffect, useState } from "react";
import { TripService } from "../../../Modules/Trip/Services/TripService";
import { ProjectService } from "../../../Modules/Project/Services/ProjectService";
import { nameof } from "../../Helpers/StringHelpers";
import { SelectListItem } from "../../Models/SelectListItem";
import { MemberGridAddMemberComponent } from "./MemberGridAddMemberComponent";
import { DeleteButtonComponent } from "../Buttons/DeleteButtonComponent";

export interface MemberGridComponentProps {
  type: MemberGridType;
  projectId: string;
  tripId?: string;
}

const MemberGridComponent: React.FC<MemberGridComponentProps> = (props) => {
  const [teamMemberData, setTeamMemberData] = useState<UserTableColumn[]>();
  const [teamMembersLoading, setTeamMembersLoading] = useState(true);

  useEffect(() => {
    if (teamMemberData === undefined) {
      const getTripName = async () => {
        await refreshData();
      };
      getTripName();
    }
  });

  const refreshData = async () => {
    const getTripName = async () => {
      let members = [] as SelectListItem<string>[];
      switch (props.type) {
        case MemberGridType.Project:
          members = await ProjectService.GetProjectMembers(
            props.projectId as string
          );
          break;
        case MemberGridType.Trip:
          members = await TripService.GetTripMembers(props.tripId as string);
          break;
      }

      setTeamMemberData(
        members.map((e) => {
          return { name: e.display, id: e.value } as UserTableColumn;
        })
      );
      setTeamMembersLoading(false);
    };
    getTripName();
  };

  const handleDelete = async (userId: string) => {
    try {
      setTeamMembersLoading(true);
      switch (props.type) {
        case MemberGridType.Project:
          await ProjectService.DeleteProjectMember(
            userId,
            props.projectId as string
          );
          break;

        case MemberGridType.Trip:
          await TripService.DeleteTripMember(userId, props.tripId as string);
          break;
      }
      refreshData();
    } catch (error) {
      console.error(error);
      setTeamMembersLoading(false);
    }
  };

  const teamMemberColumns: ColumnsType<UserTableColumn> = [
    {
      title: "Name",
      dataIndex: nameof<UserTableColumn>("name"),
      render: (text) => <a>{text}</a>,
    },
    {
      title: "Action",
      render: (text: string, record: any) => (
        <Space size="middle">
          <DeleteButtonComponent
            title={`Are you sure you want to delete this ${props.type.toLowerCase()} member?`}
            onConfirm={() => handleDelete(record.id)}
            okText="Yes"
            cancelText="No"
          />
        </Space>
      ),
    },
  ];

  const onAddedSuccess = async () => {
    await refreshData();
  };

  return (
    <>
      <Card
        loading={teamMembersLoading || teamMemberData === undefined}
        title={`${props.type} Members`}
        extra={
          <>
            <MemberGridAddMemberComponent
              onAddedSuccess={onAddedSuccess}
              {...props}
            />
          </>
        }
      >
        <Table
          columns={teamMemberColumns}
          dataSource={teamMemberData}
          rowKey={(e) => {
            return e.id;
          }}
        ></Table>
      </Card>
    </>
  );
};

export enum MemberGridType {
  Project = "Project",
  Trip = "Trip",
}

interface UserTableColumn {
  name: string;
  id: string;
  action?: any;
}

export { MemberGridComponent };
