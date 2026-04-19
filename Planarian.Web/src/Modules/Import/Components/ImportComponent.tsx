import { useMemo, useState } from "react";
import "./ImportComponent.scss";
import { ImportCaveComponent } from "./ImportCaves";
import { ImportEntrancesComponent } from "./ImportEntrances";
import { ImportInformationCardComponent } from "./ImportInformationCardComponent";
import { Button } from "antd";
import {
  ApartmentOutlined,
  FileImageOutlined,
  ReadOutlined,
  EnvironmentOutlined,
} from "@ant-design/icons";
import { ImportFilesComponent } from "./ImportFilesComponent";

const ImportComponent = () => {
  const [step, setStep] = useState<number>(1);
  const totalSteps = 4;

  const stepItems = useMemo(
    () => [
      {
        key: 1,
        title: "Templates",
        shortTitle: "Refs",
        icon: <ReadOutlined />,
      },
      {
        key: 2,
        title: "Upload Caves",
        shortTitle: "Caves",
        icon: <EnvironmentOutlined />,
      },
      {
        key: 3,
        title: "Upload Cave Entrances",
        shortTitle: "Entrances",
        icon: <ApartmentOutlined />,
      },
      {
        key: 4,
        title: "Upload Files",
        shortTitle: "Files",
        icon: <FileImageOutlined />,
      },
    ],
    []
  );

  const renderActiveStep = () => {
    switch (step) {
      case 1:
        return <ImportInformationCardComponent />;
      case 2:
        return (
          <ImportCaveComponent
            onUploaded={() => {
              setStep(3);
            }}
          />
        );
      case 3:
        return (
          <ImportEntrancesComponent
            onUploaded={() => {
              setStep(4);
            }}
          />
        );
      case 4:
      default:
        return (
          <ImportFilesComponent
            onUploaded={() => {
              setStep(4);
            }}
          />
        );
    }
  };

  return (
    <div className="import-workflow">
      <div className="import-workflow__header">
        <div className="import-workflow__nav" role="tablist" aria-label="Import steps">
          {stepItems.map((item) => (
            <button
              key={item.key}
              onClick={() => setStep(item.key)}
              className={`import-workflow__nav-item ${
                step === item.key ? "import-workflow__nav-item--active" : ""
              }`}
              role="tab"
              aria-selected={step === item.key}
              type="button"
              title={item.title}
            >
              <span className="import-workflow__nav-icon">{item.icon}</span>
              <span className="import-workflow__nav-label">
                {item.title}
              </span>
              <span className="import-workflow__nav-label-short">
                {item.shortTitle}
              </span>
            </button>
          ))}
        </div>
      </div>

      <div className="import-workflow__content">
        <div className="import-workflow__step-scroll">
          <div className="import-workflow__step-panel">{renderActiveStep()}</div>
        </div>
      </div>

      <div className="import-workflow__footer">
        <div className="import-workflow__footer-actions">
          <Button
            onClick={() => setStep((prev) => Math.max(1, prev - 1))}
            disabled={step === 1}
          >
            Previous
          </Button>
          <Button
            type="primary"
            onClick={() => setStep((prev) => Math.min(totalSteps, prev + 1))}
            disabled={step === totalSteps}
          >
            Next
          </Button>
        </div>
      </div>
    </div>
  );
};

export { ImportComponent };
