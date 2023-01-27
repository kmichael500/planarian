import React, { useState } from "react";
import {
  Form,
  Input,
  Select,
  Button,
  Card,
  Row,
  Col,
  Divider,
  message,
  Upload,
} from "antd";

import { DeleteOutlined, PlusOutlined } from "@ant-design/icons";
import { LeadClassification } from "../Leads/Models/LeadClassification";
import { CreateLeadVm } from "../Leads/Models/CreateLeadVm";
import { Link, useNavigate, useParams } from "react-router-dom";
import { TextFileInputComponent } from "../../Shared/Components/TextFileInputComponent";
import { TherionService } from "../Therion/Services/TherionService";
import { TripService } from "../Trips/Services/TripService";

const { Option } = Select;

const LeadAddComponent: React.FC = () => {
  const [leads, setLeads] = useState<CreateLeadVm[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(false);

  const { tripId } = useParams();
  const navigate = useNavigate();

  const addLead = () => {
    setLeads([
      ...leads,
      {
        description: "",
        classification: LeadClassification.UNKNOWN,
        closestStation: "",
      },
    ]);
  };

  const deleteLead = (index: number) => {
    const updatedLeads = [...leads];
    updatedLeads.splice(index, 1);
    setLeads(updatedLeads);
  };

  const handleDescriptionChange = (index: number, value: string) => {
    const updatedLeads = [...leads];
    updatedLeads[index].description = value;
    setLeads(updatedLeads);
  };

  const handleClassificationChange = (
    index: number,
    value: LeadClassification
  ) => {
    const updatedLeads = [...leads];
    updatedLeads[index].classification = value;
    setLeads(updatedLeads);
  };

  const handleClosestStationChange = (index: number, value: string) => {
    const updatedLeads = [...leads];
    updatedLeads[index].closestStation = value;
    setLeads(updatedLeads);
  };

  const handleSubmit = async (values: any) => {
    let hasErrors = false;
    let errorMessage = "";
    leads.forEach((lead) => {
      if (!lead.description || !lead.classification || !lead.closestStation) {
        hasErrors = true;
        errorMessage = "Please fill in all fields";
      }
    });

    if (hasErrors) {
      message.error(errorMessage);
      return;
    }

    try {
      setIsLoading(true);
      await TripService.AddLeads(leads, tripId as string);
      message.success("Leads added successfully");
      navigate("./../");
    } catch (e: any) {
      message.error(e);
    }
  };

  const extractFromTh2 = (text: string) => {
    let continuationPoints = TherionService.GetContinuationPoints(text);

    const th2Leads = continuationPoints.map((lead) => {
      return {
        closestStation: lead.closestStation?.name,
        description: lead.description,
        classification: lead.classification,
      } as CreateLeadVm;
    });

    if (th2Leads.length === 0) {
      message.error("No leads found in file");
      return;
    } else {
      message.success("Leads extracted successfully");
    }

    setLeads([...leads, ...th2Leads]);
  };

  return (
    <>
      <Row align="middle" gutter={10}>
        <Col>{/* <Title level={2}>{project?.name}</Title> */}</Col>
        {/* take up rest of space to push others to right and left side */}
        <Col flex="auto"></Col>
        <Col>
          <Link to={"./../"}>
            <Button>Back</Button>
          </Link>
        </Col>
        <Col> </Col>
      </Row>
      <Divider />
      <Card
        loading={isLoading}
        title="Add Leads"
        extra={[
          <Button onClick={addLead} icon={<PlusOutlined />}>
            Add
          </Button>,
        ]}
        actions={[
          <TextFileInputComponent
            buttonText="Extract from TH2"
            onTextChange={extractFromTh2}
          ></TextFileInputComponent>,
          <Button onClick={handleSubmit} type="primary" htmlType="submit">
            Submit
          </Button>,
        ]}
      >
        <Form layout="vertical">
          <Row gutter={{ xs: 8, sm: 8, md: 24, lg: 32 }}>
            {leads.map((lead, index) => (
              <Col key={index} xs={24} sm={12} md={8} lg={6}>
                <Card
                  title={`Lead ${index + 1}`}
                  style={{ marginBottom: 16 }}
                  extra={[
                    <Button
                      onClick={() => deleteLead(index)}
                      icon={<DeleteOutlined />}
                    ></Button>,
                  ]}
                  actions={[]}
                >
                  <Form.Item label="Description" rules={[{ required: true }]}>
                    <Input.TextArea
                      value={lead.description}
                      onChange={(event) =>
                        handleDescriptionChange(index, event.target.value)
                      }
                      placeholder="Enter lead description"
                    />
                  </Form.Item>
                  <Form.Item
                    label="Classification"
                    rules={[{ required: true }]}
                  >
                    <Select
                      value={lead.classification}
                      onChange={(value) =>
                        handleClassificationChange(index, value)
                      }
                    >
                      <Option value={LeadClassification.GOOD}>Good</Option>
                      <Option value={LeadClassification.DECENT}>Decent</Option>
                      <Option value={LeadClassification.BAD}>Bad</Option>
                      <Option value={LeadClassification.UNKNOWN}>
                        Unknown
                      </Option>
                    </Select>
                  </Form.Item>
                  <Form.Item
                    label="Closest Station"
                    rules={[{ required: true }]}
                  >
                    <Input
                      value={lead.closestStation}
                      onChange={(event) =>
                        handleClosestStationChange(index, event.target.value)
                      }
                      placeholder="Enter closest station"
                    />
                  </Form.Item>
                </Card>
              </Col>
            ))}
          </Row>
        </Form>
      </Card>
    </>
  );
};

export { LeadAddComponent };
