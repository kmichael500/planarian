import React from "react";
import { Card, Steps } from "antd";

const { Step } = Steps;
interface StepWrapperProps {
  step: number;
  children: React.ReactNode;
}

const StepWrapper = ({
  step,
  children,
  setStep,
}: StepWrapperProps & { setStep: (step: number) => void }) => {
  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        maxHeight: "72vh",
        minHeight: "72vh",
        padding: "20px",
      }}
    >
      <Steps current={step - 1} style={{ marginBottom: "20px" }}>
        <Step
          title="Templates"
          onClick={() => setStep(1)}
          style={{ cursor: "pointer" }}
          className="clickable-step"
        />
        <Step
          title="Upload Caves"
          onClick={() => setStep(2)}
          style={{ cursor: "pointer" }}
          className="clickable-step"
        />
        <Step
          title="Upload Cave Entrances"
          onClick={() => setStep(3)}
          style={{ cursor: "pointer" }}
          className="clickable-step"
        />
      </Steps>
      <div
        id="step-wrapper-container"
        style={{ flex: 1, overflowY: "auto", display: "flex" }}
      >
        {children}
      </div>
    </div>
  );
};

export { StepWrapper };
