import { render, screen } from "@testing-library/react";
import React from "react";
import { MessageDeliveryEventType } from "../../Models/MessageDeliveryEventType";
import { MessageDeliveryStatus } from "../../Models/MessageDeliveryStatus";
import { EmailHistoryAttempt, EmailHistoryModal } from "./EmailHistoryModal";

jest.mock("../Buttons/PlanarianModal", () => ({
  PlanarianModal: ({
    open,
    header,
    children,
  }: {
    open: boolean;
    header?: React.ReactNode;
    children?: React.ReactNode;
  }) =>
    open ? (
      <div role="dialog">
        <div>{header}</div>
        {children}
      </div>
    ) : null,
}));

const attempts: EmailHistoryAttempt[] = [
  {
    messageLogId: "message002",
    createdOn: "2026-08-14T13:00:00Z",
    deliveryStatus: MessageDeliveryStatus.PermanentFailed,
    deliveryStatusOn: "2026-08-14T13:03:00Z",
    events: [
      {
        eventType: MessageDeliveryEventType.PermanentFailed,
        occurredOn: "2026-08-14T13:03:00Z",
        isDelayedBounce: true,
        severity: "permanent",
        reason: "bounce",
        attemptNumber: 3,
      },
    ],
  },
  {
    messageLogId: "message001",
    createdOn: "2026-08-14T12:00:00Z",
    deliveryStatus: MessageDeliveryStatus.Delivered,
    deliveryStatusOn: "2026-08-14T12:01:00Z",
    events: [
      {
        eventType: MessageDeliveryEventType.Opened,
        occurredOn: "2026-08-14T12:02:00Z",
        bot: "apple",
      },
    ],
  },
];

describe("EmailHistoryModal", () => {
  it("renders delivery attempts and provider event details", () => {
    render(
      <EmailHistoryModal
        open
        attempts={attempts}
        title="Email history"
        onClose={() => undefined}
      />
    );

    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(screen.getByText("Email history")).toBeInTheDocument();
    expect(screen.getByText(/Attempt 2/)).toBeInTheDocument();
    expect(screen.getAllByText(/Permanent failure/)).toHaveLength(2);
    expect(screen.queryByText(/PermanentFailed/)).not.toBeInTheDocument();
    expect(screen.getByText(/automated \(apple\)/)).toBeInTheDocument();
    expect(screen.getByText(/delivery attempt 3/)).toHaveTextContent("bounce");
    expect(screen.getByText(/delivery attempt 3/)).toHaveTextContent(
      "delayed bounce"
    );
  });

  it("renders the caller-provided empty state", () => {
    render(
      <EmailHistoryModal
        open
        attempts={[]}
        emptyText="No invitation emails yet."
        onClose={() => undefined}
      />
    );

    expect(screen.getByText("No invitation emails yet.")).toBeInTheDocument();
  });
});
