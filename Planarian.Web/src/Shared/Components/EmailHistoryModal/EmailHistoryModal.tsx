import React from "react";
import { Spin, Typography } from "antd";
import {
  MessageDeliveryEventType,
  MessageDeliveryEventTypeDisplay,
} from "../../Models/MessageDeliveryEventType";
import {
  MessageDeliveryStatus,
  MessageDeliveryStatusDisplay,
} from "../../Models/MessageDeliveryStatus";
import { formatDateTime } from "../../Helpers/StringHelpers";
import { PlanarianModal } from "../Buttons/PlanarianModal";
import "./EmailHistoryModal.scss";

const { Text } = Typography;

export interface EmailHistoryEvent {
  eventType: MessageDeliveryEventType;
  occurredOn: string;
  bot?: string | null;
  severity?: string | null;
  reason?: string | null;
  deliveryCode?: string | null;
  enhancedDeliveryCode?: string | null;
  attemptNumber?: number | null;
  isDelayedBounce?: boolean | null;
}

export interface EmailHistoryAttempt {
  messageLogId: string;
  createdOn: string;
  deliveryStatus?: MessageDeliveryStatus | null;
  deliveryStatusOn?: string | null;
  events: EmailHistoryEvent[];
}

interface EmailHistoryModalProps {
  open: boolean;
  loading?: boolean;
  title?: React.ReactNode;
  attempts: EmailHistoryAttempt[];
  emptyText?: React.ReactNode;
  onClose: () => void;
}

const EmailHistoryModal: React.FC<EmailHistoryModalProps> = ({
  open,
  loading = false,
  title = "Email History",
  attempts,
  emptyText = "No tracked emails.",
  onClose,
}) => (
  <PlanarianModal
    header={title}
    open={open}
    onClose={onClose}
    width={720}
    height="70vh"
  >
    <Spin spinning={loading}>
      <div className="email-history-modal">
        {!loading && attempts.length === 0 ? (
          <Text type="secondary">{emptyText}</Text>
        ) : (
          attempts.map((attempt, index) => (
            <div
              className="email-history-modal__attempt"
              key={attempt.messageLogId}
            >
              <Text strong>
                Attempt {attempts.length - index} ·{" "}
                {formatDateTime(attempt.createdOn)}
              </Text>
              <Text>
                Delivery: {attempt.deliveryStatus
                  ? MessageDeliveryStatusDisplay[attempt.deliveryStatus]
                  : "Not tracked"}
                {attempt.deliveryStatusOn
                  ? ` · ${formatDateTime(attempt.deliveryStatusOn)}`
                  : ""}
              </Text>
              {attempt.events.length === 0 ? (
                <Text type="secondary">No provider events recorded yet.</Text>
              ) : (
                <div className="email-history-modal__events">
                  {attempt.events.map((event, eventIndex) => (
                    <Text
                      type="secondary"
                      key={`${attempt.messageLogId}-${event.occurredOn}-${eventIndex}`}
                    >
                      {MessageDeliveryEventTypeDisplay[event.eventType]} · {formatDateTime(event.occurredOn)}
                      {event.bot ? ` · automated (${event.bot})` : ""}
                      {event.attemptNumber != null
                        ? ` · delivery attempt ${event.attemptNumber}`
                        : ""}
                      {event.severity ? ` · ${event.severity}` : ""}
                      {event.reason ? ` · ${event.reason}` : ""}
                      {event.enhancedDeliveryCode
                        ? ` · ${event.enhancedDeliveryCode}`
                        : event.deliveryCode
                        ? ` · ${event.deliveryCode}`
                        : ""}
                      {event.isDelayedBounce ? " · delayed bounce" : ""}
                    </Text>
                  ))}
                </div>
              )}
            </div>
          ))
        )}
      </div>
    </Spin>
  </PlanarianModal>
);

export { EmailHistoryModal };
