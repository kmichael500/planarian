import React, { useState, useEffect } from "react";
import { Table, Spin, Button, Popconfirm } from "antd";
import { LeadVm } from "../Leads/Models/Lead";
import { TripObjectiveService } from "../Objective/Services/trip.objective.service";
import { nameof } from "../../Shared/Helpers/StringHelpers";
import { LeadService } from "../Leads/Services/lead.service";

interface LeadTableProps {
  tripObjectiveId: string;
}
const LeadTableComponent: React.FC<LeadTableProps> = (
  props: LeadTableProps
) => {
  const [leads, setLeads] = useState<LeadVm[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchLeads = async () => {
      try {
        const response = await TripObjectiveService.GetLeads(
          props.tripObjectiveId as string
        );
        setLeads(response);
      } catch (error) {
        console.error(error);
      } finally {
        setLoading(false);
      }
    };

    fetchLeads();
  }, []);

  const columns = [
    {
      title: "Description",
      dataIndex: nameof<LeadVm>("description"),
      key: nameof<LeadVm>("description"),
    },
    {
      title: "Classification",
      dataIndex: nameof<LeadVm>("classification"),
      key: "classification",
    },
    {
      title: "Closest Station",
      dataIndex: nameof<LeadVm>("closestStation"),
      key: nameof<LeadVm>("closestStation"),
    },
    {
      title: "Actions",
      key: "actions",
      render: (text: string, record: LeadVm) => (
        <span>
          <Popconfirm
            title="Are you sure to delete this lead?"
            onConfirm={() => handleDelete(record.id)}
            okText="Yes"
            cancelText="No"
          >
            <Button type="primary" danger>
              Delete
            </Button>
          </Popconfirm>
        </span>
      ),
    },
  ];

  const handleDelete = async (leadId: string) => {
    try {
      setLoading(true);
      await LeadService.DeleteLead(leadId);
      // refresh the leads data
      const response = await TripObjectiveService.GetLeads(
        props.tripObjectiveId as string
      );
      setLeads(response);
      setLoading(false);
    } catch (error) {
      console.error(error);
      setLoading(false);
    }
  };

  return (
    <Spin spinning={loading}>
      <Table
        dataSource={leads}
        columns={columns}
        rowKey={nameof<LeadVm>("id")}
      />
    </Spin>
  );
};

export default LeadTableComponent;
