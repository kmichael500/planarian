import React, { useContext, useEffect, useState } from "react";
import { Card, Col, Form, Input, message, Row, Select } from "antd";
import { LeadClassification } from "../../Lead/Models/LeadClassification";
import { CreateLeadVm } from "../../Lead/Models/CreateLeadVm";
import { useNavigate, useParams } from "react-router-dom";
import { TextFileInputComponent } from "../../../Shared/Components/TextFileInput/TextFileInputComponent";
import { TherionService } from "../../Therion/Services/TherionService";
import { TripService } from "../Services/TripService";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { BackButtonComponent } from "../../../Shared/Components/Buttons/BackButtonComponent";
import { AddButtonComponent } from "../../../Shared/Components/Buttons/AddButtonComponent";
import { SubmitButtonComponent } from "../../../Shared/Components/Buttons/SubmitButtonComponent";
import { DeleteButtonComponent } from "../../../Shared/Components/Buttons/DeleteButtonComponent";

const { Option } = Select;

const LeadAddPage: React.FC = () => {
  const [leads, setLeads] = useState<CreateLeadVm[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(false);

  const { tripId } = useParams();
  const navigate = useNavigate();
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);

  useEffect(() => {
    setHeaderButtons([<BackButtonComponent to={"./.."} />]);
    setHeaderTitle(["Add Leads"]);
  }, []);

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
    } catch (e) {
      const error = e as ApiErrorResponse;

      message.error(error.message);
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
      <Card
        loading={isLoading}
        title="Add Leads"
        extra={[<AddButtonComponent onClick={addLead} />]}
        actions={[
          <TextFileInputComponent
            buttonText="Extract from TH2"
            onTextChange={extractFromTh2}
          ></TextFileInputComponent>,
          <SubmitButtonComponent
            onClick={handleSubmit}
            type="primary"
            htmlType="submit"
          />,
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
                    <DeleteButtonComponent
                      title={"Delete Lead"}
                      onConfirm={() => deleteLead(index)}
                    />,
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

export { LeadAddPage };
