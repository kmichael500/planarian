import React, { useContext, useEffect, useRef, useState } from "react";
import {
  Checkbox,
  Input,
  message,
  Modal,
  Select,
  Typography,
} from "antd";
import {
  CheckSquareOutlined,
  CloseOutlined,
  DeleteOutlined,
  EditOutlined,
  MailOutlined,
  RedoOutlined,
  UserAddOutlined,
} from "@ant-design/icons";
import { useNavigate } from "react-router-dom";

import { AccountUserManagerService } from "../Services/UserManagerService";
import { InvitationEmailAttemptVm } from "../Models/InvitationEmailHistoryVm";
import { MessageDeliveryStatus } from "../../../Shared/Models/MessageDeliveryStatus";
import { UserManagerGridVm } from "../Models/UserManagerGridVm";
import { PlanarianError } from "../../../Shared/Exceptions/PlanarianErrors";
import { formatDateTime } from "../../../Shared/Helpers/StringHelpers";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import { CardGridComponent } from "../../../Shared/Components/CardGrid/CardGridComponent";
import {
  GridCard,
  GridCardAction,
} from "../../../Shared/Components/CardGrid/GridCard";
import { SpinnerCardComponent } from "../../../Shared/Components/SpinnerCard/SpinnerCard";
import { EmailHistoryModal } from "../../../Shared/Components/EmailHistoryModal/EmailHistoryModal";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";
import { SplitSortControl } from "../../Search/Components/SplitSortControl";
import { ScrollCollapseSection } from "../../../Shared/Components/ScrollCollapseSection/ScrollCollapseSection";
import { useScrollRevealVisibility } from "../../../Shared/Scroll/useScrollRevealVisibility";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { RequestRuntimeState } from "../../../Shared/Http/RequestRuntimeState";
import { InvitePeopleModal } from "./InvitePeopleModal";
import { BulkDisableModal } from "./BulkDisableModal";
import "./UserManagerComponent.scss";

const { Text } = Typography;
const USER_MANAGER_COLLAPSE_BREAKPOINT_PX = 760;

type UserStatusFilter = "all" | "accepted" | "pending" | "disabled";
type UserSortBy =
  | "fullName"
  | "emailAddress"
  | "invitationSentOn"
  | "invitationAcceptedOn"
  | "lastActiveOn";

const UserManagerComponent: React.FC = () => {
  const [users, setUsers] = useState<UserManagerGridVm[]>([]);
  const [loading, setLoading] = useState<boolean>(false);
  const [inviteModalVisible, setInviteModalVisible] = useState<boolean>(false);
  const [selecting, setSelecting] = useState(false);
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [disableTargets, setDisableTargets] = useState<UserManagerGridVm[] | null>(null);
  const [membershipRevocationTarget, setMembershipRevocationTarget] = useState<{
    user: UserManagerGridVm;
    accountId: string;
  } | null>(null);
  const [revocationConfirmation, setRevocationConfirmation] = useState("");
  const [sendRevocationEmail, setSendRevocationEmail] = useState(true);
  const [revocationMessage, setRevocationMessage] = useState("");
  const [revokingMembership, setRevokingMembership] = useState(false);
  const [searchText, setSearchText] = useState<string>("");
  const [statusFilter, setStatusFilter] = useState<UserStatusFilter>("all");
  const [sortBy, setSortBy] = useState<UserSortBy>("invitationSentOn");
  const [sortDescending, setSortDescending] = useState(true);
  const [invitationHistoryVisible, setInvitationHistoryVisible] =
    useState(false);
  const [invitationHistoryLoading, setInvitationHistoryLoading] =
    useState(false);
  const [invitationHistoryUser, setInvitationHistoryUser] =
    useState<UserManagerGridVm | null>(null);
  const [invitationEmailHistory, setInvitationEmailHistory] = useState<
    InvitationEmailAttemptVm[]
  >([]);
  const navigate = useNavigate();
  const { currentUser, currentAccountName } = useContext(AppContext);
  const listRequest = useRef(0);
  useEffect(() => {
    setSelecting(false);
    setSelectedIds([]);
    setDisableTargets(null);
    setMembershipRevocationTarget(null);
    setRevocationConfirmation("");
    setSendRevocationEmail(true);
    setRevocationMessage("");
    setInviteModalVisible(false);
    setInvitationHistoryVisible(false);
    setInvitationHistoryUser(null);
    setInvitationEmailHistory([]);
  }, [currentUser?.currentAccountId]);

  const toolbarVisibility = useScrollRevealVisibility({
    breakpointPx: USER_MANAGER_COLLAPSE_BREAKPOINT_PX,
    mode: "direct",
  });

  const fetchUsers = async () => {
    const accountId = RequestRuntimeState.getCurrentAccountId();
    const request = ++listRequest.current;
    if (!accountId) {
      setUsers([]);
      setLoading(false);
      return;
    }
    setLoading(true);
    try {
      const response = await AccountUserManagerService.GetAccountUsers(accountId);
      if (request === listRequest.current &&
          RequestRuntimeState.getCurrentAccountId() === accountId) {
        setUsers(response);
      }
    } catch (error) {
      if (request === listRequest.current &&
          RequestRuntimeState.getCurrentAccountId() === accountId) {
        message.error("Failed to load users.");
        console.error(error);
      }
    } finally {
      if (request === listRequest.current) setLoading(false);
    }
  };

  useEffect(() => {
    setUsers([]);
    void fetchUsers();
    return () => { listRequest.current++; };
  }, [currentUser?.currentAccountId]);

  const handleShowInvitationHistory = async (user: UserManagerGridVm) => {
    setInvitationHistoryUser(user);
    setInvitationHistoryVisible(true);
    setInvitationHistoryLoading(true);
    try {
      setInvitationEmailHistory(
        await AccountUserManagerService.GetInvitationEmailHistory(user.userId)
      );
    } catch (err) {
      const error = err as PlanarianError;
      message.error(error.message);
      setInvitationEmailHistory([]);
    } finally {
      setInvitationHistoryLoading(false);
    }
  };

  const [isRestoring, setIsRestoring] = useState<boolean>(false);
  const handleRestoreAccess = async (userId: string) => {
    try {
      setIsRestoring(true);
      await AccountUserManagerService.RestoreAccess(userId);
      message.success("Access restored.");
      fetchUsers();
    } catch (err) {
      const error = err as PlanarianError;
      message.error(error.message);
    }
    setIsRestoring(false);
  };

  const closeMembershipRevocation = () => {
    if (revokingMembership) return;
    setMembershipRevocationTarget(null);
    setRevocationConfirmation("");
    setSendRevocationEmail(true);
    setRevocationMessage("");
  };

  const handleMembershipRevocation = async () => {
    if (!membershipRevocationTarget || revocationConfirmation !== "REVOKE" || revokingMembership ||
        (sendRevocationEmail && !revocationMessage.trim())) return;
    const { user, accountId } = membershipRevocationTarget;
    if (RequestRuntimeState.getCurrentAccountId() !== accountId) {
      message.error("The selected account changed. Reopen the confirmation before revoking any membership.");
      setMembershipRevocationTarget(null);
      setRevocationConfirmation("");
      return;
    }

    setRevokingMembership(true);
    try {
      const result = await AccountUserManagerService.RevokeMembership(
        user.userId, accountId,
        sendRevocationEmail ? revocationMessage.trim() : undefined, sendRevocationEmail
      );
      if (RequestRuntimeState.getCurrentAccountId() === accountId) {
        if (result.notificationEmailDeliveryStatus === MessageDeliveryStatus.SendFailed) {
          message.warning("Membership revoked, but notification email failed.");
        } else if (result.notificationEmailDeliveryStatus === MessageDeliveryStatus.Submitted) {
          message.success("Membership revoked. Notification email submitted.");
        } else {
          message.success("Membership revoked from this account.");
        }
        setMembershipRevocationTarget(null);
        setRevocationConfirmation("");
        await fetchUsers();
      }
    } catch (err) {
      if (RequestRuntimeState.getCurrentAccountId() === accountId) {
        message.error((err as PlanarianError).message);
      }
    } finally {
      setRevokingMembership(false);
    }
  };

  const [isResending, setIsResending] = useState<boolean>(false);
  const handleResendInvitation = async (userId: string) => {
    try {
      setIsResending(true);
      await AccountUserManagerService.ResendInvitation(userId);
      message.success("Invitation resent.");
      fetchUsers();
    } catch (err) {
      const error = err as PlanarianError;
      message.error(error.message);
    }
    setIsResending(false);
  };

  const getUserStatus = (user: UserManagerGridVm): UserStatusFilter => {
    if (user.accessDisabledOn) return "disabled";
    return user.hasActiveInvitation ? "pending" : "accepted";
  };

  const sortOptions: SelectListItem<string>[] = [
    { display: "Invitation sent", value: "invitationSentOn" },
    { display: "Name", value: "fullName" },
    { display: "Email", value: "emailAddress" },
    { display: "Invitation accepted", value: "invitationAcceptedOn" },
    { display: "Last active", value: "lastActiveOn" },
  ];

  const compareNullableDates = (a?: string | null, b?: string | null): number =>
    new Date(a ?? 0).getTime() - new Date(b ?? 0).getTime();

  const filteredUsers = users
    .filter(
      (user) =>
        user.fullName.toLowerCase().includes(searchText.toLowerCase()) ||
        user.emailAddress.toLowerCase().includes(searchText.toLowerCase())
    )
    .filter(
      (user) => statusFilter === "all" || getUserStatus(user) === statusFilter
    )
    .sort((a, b) => {
      let comparison = 0;

      switch (sortBy) {
        case "fullName":
          comparison = a.fullName.localeCompare(b.fullName);
          break;
        case "emailAddress":
          comparison = a.emailAddress.localeCompare(b.emailAddress);
          break;
        case "invitationAcceptedOn":
          comparison = compareNullableDates(
            a.invitationAcceptedOn,
            b.invitationAcceptedOn
          );
          break;
        case "lastActiveOn":
          comparison = compareNullableDates(a.lastActiveOn, b.lastActiveOn);
          break;
        case "invitationSentOn":
        default:
          comparison = compareNullableDates(
            a.invitationSentOn,
            b.invitationSentOn
          );
          break;
      }

      return sortDescending ? -comparison : comparison;
    });

  const eligibleIds = filteredUsers
    .filter((user) => !user.accessDisabledOn && user.userId !== currentUser?.id)
    .map((user) => user.userId);
  const selectedUsers = filteredUsers.filter((user) => selectedIds.includes(user.userId));
  const selectedAll = eligibleIds.length > 0 &&
    eligibleIds.every((id) => selectedIds.includes(id));
  const selectedSome = eligibleIds.some((id) => selectedIds.includes(id));

  const toggleSelected = (userId: string, checked: boolean) =>
    setSelectedIds((previous) => checked
      ? [...previous.filter((id) => id !== userId), userId]
      : previous.filter((id) => id !== userId));

  const renderDate = (value?: string | null) =>
    value ? formatDateTime(value) : "Not recorded";

  const renderUserCard = (user: UserManagerGridVm) => {
    const isPending = user.hasActiveInvitation;
    const isDisabled = !!user.accessDisabledOn;
    const actions: GridCardAction[] = [
      {
        key: "edit",
        label: "Edit",
        icon: <EditOutlined />,
        to: user.userId,
        type: "primary",
      },
    ];

    if (isPending && !isDisabled) {
      actions.push({
        key: "resend",
        label: "Resend",
        icon: <RedoOutlined />,
        loading: isResending,
        onClick: () => handleResendInvitation(user.userId),
      });
    }

    if (isDisabled) {
      actions.push({
        key: "restore",
        label: "Restore access",
        render: (
          <PlanarianButton
            alwaysShowChildren
            icon={<RedoOutlined />}
            permissionKey={PermissionKey.Admin}
            loading={isRestoring}
            type="default"
            onClick={() => handleRestoreAccess(user.userId)}
          >
            Restore access
          </PlanarianButton>
        ),
      });
    } else if (user.userId !== currentUser?.id) {
      actions.push({
        key: "remove",
        label: isPending ? "Remove invitation" : "Disable access",
        render: (
          <PlanarianButton
            alwaysShowChildren
            permissionKey={PermissionKey.Admin}
            danger
            type="default"
            icon={<DeleteOutlined />}
            aria-label={isPending ? "Remove invitation" : "Disable access"}
            onClick={() => setDisableTargets([user])}
          >
            {isPending ? "Remove invitation" : "Disable access"}
          </PlanarianButton>
        ),
      });
    }

    return (
      <GridCard
        actions={selecting ? undefined : actions}
        className="user-manager-grid-card"
        stickyFooter
        stickyHeader
        header={
          <span>
            <span className="user-manager-grid-card__name">
              {user.fullName}
            </span>
            <span className="user-manager-grid-card__email">
              {user.emailAddress}
            </span>
          </span>
        }
        headerExtra={
          <div className="user-manager-grid-card__header-extra">
            {isDisabled ? (
              <span className="user-manager-grid-card__status">Access disabled</span>
            ) : isPending ? (
              <span className="user-manager-grid-card__status">Pending</span>
            ) : null}
            {selecting && !isDisabled && user.userId !== currentUser?.id && (
              <Checkbox
                aria-label={`Select ${user.fullName}`}
                checked={selectedIds.includes(user.userId)}
                onChange={(event) => toggleSelected(user.userId, event.target.checked)}
              />
            )}
          </div>
        }
      >
        <div className="user-manager-grid-card__details">
          <div className="user-manager-grid-card__detail">
            <Text type="secondary">Invitation Sent</Text>
            <span>{renderDate(user.invitationSentOn)}</span>
          </div>
          <div className="user-manager-grid-card__detail">
            <Text type="secondary">Invitation Accepted</Text>
            <span>{renderDate(user.invitationAcceptedOn)}</span>
          </div>
          <div className="user-manager-grid-card__detail">
            <Text type="secondary">Last Active</Text>
            <span>{renderDate(user.lastActiveOn)}</span>
          </div>
          {user.accessDisabledOn ? (
            <div className="user-manager-grid-card__detail">
              <Text type="secondary">Access Disabled</Text>
              <span>{renderDate(user.accessDisabledOn)}</span>
            </div>
          ) : null}
          {user.invitationEmailAttemptCount > 0 ? (
            <div className="user-manager-grid-card__history">
              <PlanarianButton
                alwaysShowChildren
                icon={<MailOutlined />}
                size="small"
                type="link"
                onClick={() => handleShowInvitationHistory(user)}
              >
                Email History
              </PlanarianButton>
            </div>
          ) : null}
          {!selecting && !isPending && user.userId !== currentUser?.id && (
            <PlanarianButton
              className="user-manager-grid-card__membership-revoke"
              alwaysShowChildren
              permissionKey={PermissionKey.Admin}
              danger
              type="link"
              icon={<DeleteOutlined />}
              aria-label="Revoke membership"
              onClick={() => {
                const accountId = RequestRuntimeState.getCurrentAccountId();
                if (!accountId || accountId !== currentUser?.currentAccountId) return;
                setMembershipRevocationTarget({ user, accountId });
                setRevocationConfirmation("");
                setSendRevocationEmail(true);
                setRevocationMessage(
                  `An administrator has revoked your membership in ${currentAccountName || "this account"} on Planarian. You'll need a new invitation to rejoin.`
                );
              }}
            >
              Revoke membership
            </PlanarianButton>
          )}
        </div>
      </GridCard>
    );
  };

  return (
    <>
      <div className="user-manager-container">
        <div className="user-manager-toolbar">
          <div className="user-manager-toolbar__search-row">
            <Input.Search
              className="user-manager-search"
              placeholder="Search users"
              onSearch={(value) => {
                setSearchText(value);
                setSelectedIds([]);
              }}
              onChange={(e) => {
                setSearchText(e.target.value);
                setSelectedIds([]);
              }}
              allowClear
            />
          </div>
          <ScrollCollapseSection
            className="user-manager-toolbar__collapsible"
            visible={toolbarVisibility.isVisible}
          >
            <div className="user-manager-toolbar__secondary-rows">
              <div className="user-manager-toolbar__secondary-controls">
                <Select<UserStatusFilter>
                  className="user-manager-filter"
                  value={statusFilter}
                  onChange={(value) => {
                    setStatusFilter(value);
                    setSelectedIds([]);
                  }}
                  options={[
                    { label: "All statuses", value: "all" },
                    { label: "Accepted", value: "accepted" },
                    { label: "Pending", value: "pending" },
                    { label: "Access disabled", value: "disabled" },
                  ]}
                />
                <SplitSortControl
                  isDescending={sortDescending}
                  onSelect={(value) => setSortBy(value as UserSortBy)}
                  onToggleDirection={() =>
                    setSortDescending((previous) => !previous)
                  }
                  selectedValue={sortBy}
                  sortOptions={sortOptions}
                />
              </div>
              <div className="user-manager-invite-column">
                <PlanarianButton
                  permissionKey={PermissionKey.Admin}
                  icon={<CheckSquareOutlined />}
                  aria-label={selecting ? "Cancel selection" : "Select people"}
                  alwaysShowChildren
                  debounceTime={0}
                  onClick={() => {
                    setSelecting((previous) => !previous);
                    setSelectedIds([]);
                  }}
                >
                  {selecting ? "Cancel selection" : "Select people"}
                </PlanarianButton>
                {!selecting && (
                  <PlanarianButton
                    className="user-manager-invite-button"
                    permissionKey={PermissionKey.Admin}
                    icon={<UserAddOutlined />}
                    aria-label="Invite people"
                    type="primary"
                    alwaysShowChildren
                    onClick={() => setInviteModalVisible(true)}
                  >
                    Invite people
                  </PlanarianButton>
                )}
              </div>
            </div>
          </ScrollCollapseSection>
        </div>
        {selecting && (
          <div className="user-manager-selection-bar">
            <Checkbox
              indeterminate={selectedSome && !selectedAll}
              checked={selectedAll}
              disabled={eligibleIds.length === 0}
              onChange={(event) => setSelectedIds(event.target.checked ? eligibleIds : [])}
            >
              Select all shown ({eligibleIds.length})
            </Checkbox>
            <span role="status">{selectedIds.length} selected</span>
            <PlanarianButton
              alwaysShowChildren icon={<CheckSquareOutlined />}
              aria-label="Review access changes"
              permissionKey={PermissionKey.Admin}
              type="primary"
              danger
              disabled={selectedIds.length === 0}
              onClick={() => setDisableTargets([...selectedUsers])}
            >
              Review access changes
            </PlanarianButton>
          </div>
        )}
        <div className="user-manager-grid">
          <SpinnerCardComponent spinning={loading}>
            <CardGridComponent
              fillHeight
              items={filteredUsers}
              itemKey={(user) => user.userId}
              noDataDescription="No users found"
              onScrollStateChange={toolbarVisibility.handleScrollStateChange}
              renderItem={renderUserCard}
            />
          </SpinnerCardComponent>
        </div>
      </div>

      <EmailHistoryModal
        open={invitationHistoryVisible}
        loading={invitationHistoryLoading}
        title={
          invitationHistoryUser
            ? `Invitation email history — ${invitationHistoryUser.fullName}`
            : "Invitation email history"
        }
        attempts={invitationEmailHistory}
        emptyText="No tracked invitation emails."
        onClose={() => setInvitationHistoryVisible(false)}
      />

      <InvitePeopleModal
        open={inviteModalVisible}
        existingUsers={users}
        onClose={() => setInviteModalVisible(false)}
        onChanged={fetchUsers}
        onManagePermissions={(userId) => {
          setInviteModalVisible(false);
          navigate(`${userId}/permissions/${PermissionKey.View}`);
        }}
      />
      <Modal
        open={membershipRevocationTarget !== null}
        title="Revoke membership?"
        onCancel={closeMembershipRevocation}
        closable={!revokingMembership}
        maskClosable={!revokingMembership}
        footer={[
          <PlanarianButton
            key="cancel"
            icon={<CloseOutlined />}
            onClick={closeMembershipRevocation}
            disabled={revokingMembership}
          >
            Cancel
          </PlanarianButton>,
          <PlanarianButton
            key="revoke-membership"
            danger
            icon={<DeleteOutlined />}
            aria-label="Revoke membership"
            loading={revokingMembership}
            disabled={revocationConfirmation !== "REVOKE" || (sendRevocationEmail && !revocationMessage.trim())}
            onClick={handleMembershipRevocation}
          >
            Revoke membership
          </PlanarianButton>,
        ]}
      >
        <Typography.Paragraph>
          Revoking <strong>{membershipRevocationTarget?.user.fullName}</strong>'s membership will delete their saved favorites and account-specific permissions.
        </Typography.Paragraph>
        <Typography.Paragraph>
          This cannot be undone. Rejoining will require a new invitation and permissions.
        </Typography.Paragraph>
        <div className="user-manager-notification-message">
          <Checkbox
            checked={sendRevocationEmail}
            disabled={revokingMembership}
            onChange={(event) => setSendRevocationEmail(event.target.checked)}
          >
            Send notification email
          </Checkbox>
          {sendRevocationEmail && (
            <>
              <label htmlFor="membership-revocation-message">Message to user</label>
              <Input.TextArea
                id="membership-revocation-message"
                value={revocationMessage}
                onChange={(event) => setRevocationMessage(event.target.value)}
                disabled={revokingMembership}
                required
                rows={3}
                maxLength={1000}
                showCount
              />
              <Typography.Text type="secondary">
                Edit or replace the message sent to this member.
              </Typography.Text>
            </>
          )}
        </div>
        <label htmlFor="confirm-membership-revocation">
          Type <strong>REVOKE</strong> to confirm.
        </label>
        <Input
          id="confirm-membership-revocation"
          value={revocationConfirmation}
          onChange={(event) => setRevocationConfirmation(event.target.value)}
          disabled={revokingMembership}
          autoComplete="off"
        />
      </Modal>
      <BulkDisableModal
        open={disableTargets !== null}
        selected={disableTargets ?? []}
        onClose={() => setDisableTargets(null)}
        onFinished={async () => {
          await fetchUsers();
          setSelectedIds([]);
          setSelecting(false);
        }}
      />
    </>
  );
};

export { UserManagerComponent };
