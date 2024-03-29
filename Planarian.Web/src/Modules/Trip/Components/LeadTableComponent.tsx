import React, { useEffect, useState } from "react";
import { Card, Col, message, Row, Table } from "antd";

import { CloudDownloadOutlined } from "@ant-design/icons";
import { Link } from "react-router-dom";
import { downloadCSV } from "../../../Shared/Helpers/FileHelpers";
import { nameof } from "../../../Shared/Helpers/StringHelpers";
import { LeadVm } from "../../Lead/Models/LeadVm";
import { LeadService } from "../../Lead/Services/LeadService";
import { TripService } from "../Services/TripService";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { DeleteButtonComponent } from "../../../Shared/Components/Buttons/DeleteButtonComponent";
import { AddButtonComponent } from "../../../Shared/Components/Buttons/AddButtonComponent";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";

interface LeadTableProps {
  tripId: string;
}

const LeadTableComponent: React.FC<LeadTableProps> = (
  props: LeadTableProps
) => {
  const [leads, setLeads] = useState<LeadVm[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchLeads = async () => {
      try {
        const response = await TripService.GetLeads(props.tripId as string);
        setLeads(response);
      } catch (e) {
        const error = e as ApiErrorResponse;
        message.error(error.message);
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
    },
    {
      title: "Classification",
      dataIndex: nameof<LeadVm>("classification"),
    },
    {
      title: "Closest Station",
      dataIndex: nameof<LeadVm>("closestStation"),
    },
    {
      title: "Actions",
      render: (text: string, record: LeadVm) => (
        <span>
          <DeleteButtonComponent
            title="Are you sure you want to delete this lead?"
            onConfirm={() => handleDelete(record.id)}
            okText="Yes"
            cancelText="No"
          />
        </span>
      ),
    },
  ];

  const handleDelete = async (leadId: string) => {
    try {
      setLoading(true);
      await LeadService.DeleteLead(leadId);
      // refresh the leads data
      const response = await TripService.GetLeads(props.tripId as string);
      setLeads(response);
      setLoading(false);
    } catch (error) {
      console.error(error);
      setLoading(false);
    }
  };

  return (
    <Card
      title="Leads"
      loading={loading}
      extra={[
        <Row gutter={10} key={1}>
          <Col>
            <Link to={"./addLeads"}>
              <AddButtonComponent />
            </Link>
          </Col>
          <Col>
            <PlanarianButton
              icon={<CloudDownloadOutlined />}
              onClick={() => {
                downloadCSV(leads, true);
              }}
            >
              Download
            </PlanarianButton>
          </Col>
        </Row>,
      ]}
    >
      <Table
        dataSource={leads}
        columns={columns}
        rowKey={(record) => record.id}
      />
    </Card>
  );
};

export default LeadTableComponent;
