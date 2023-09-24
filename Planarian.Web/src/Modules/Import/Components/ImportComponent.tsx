import { useState } from "react";
import "./ImportComponent.scss";
import { StepWrapper } from "./StepWrapper";
import { ImportCaveComponent } from "./ImportCaves";
import { ImportEntrancesComponent } from "./ImportEntrances";
import { ImportInformationCardComponent } from "./ImportInformationCardComponent";
import { Button } from "antd";

const ImportComponent = () => {
  const [step, setStep] = useState<number>(1);
  const totalSteps = 3; // Change this to the total number of steps

  return (
    <>
      <StepWrapper step={step} setStep={setStep}>
        {step === 1 && <ImportInformationCardComponent />}
        {step === 2 && (
          <ImportCaveComponent
            onUploaded={() => {
              setStep(3);
            }}
          />
        )}
        {step === 3 && (
          <ImportEntrancesComponent
            onUploaded={() => {
              setStep(4);
            }}
          />
        )}
      </StepWrapper>
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          padding: "20px",
        }}
      >
        <Button
          onClick={() => setStep((prev) => Math.max(1, prev - 1))}
          disabled={step === 1}
        >
          Previous
        </Button>
        <Button
          onClick={() => setStep((prev) => Math.min(totalSteps, prev + 1))}
          disabled={step === totalSteps}
        >
          Next
        </Button>
      </div>
    </>
  );
};

export { ImportComponent };
