import React, { useContext, useEffect, useRef, useState } from "react";
import {
  Checkbox,
  Input,
  message,
  Select,
  Typography,
} from "antd";
import {
  CheckSquareOutlined,
  DeleteOutlined,
  EditOutlined,
  MailOutlined,
  RedoOutlined,
  UserAddOutlined,
} from "@ant-design/icons";
import { useNavigate } from "react-router-dom";

import { AccountUserManagerService } from "../Services/UserManagerService";
import { InvitationEmailAttemptVm } from "../Models/InvitationEmailHistoryVm";
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
import { AccountAccessChangeModal } from "./AccountAccessChangeModal";
import { AccountAccessAction } from "../Models/AccountAccessChangeResultVm";
import "./UserManagerComponent.scss";

const { Text } = Typography;
const USER_MANAGER_COLLAPSE_BREAKPOINT_PX = 760;

enum UserStatusFilter {
  All = "All",
  Accepted = "Accepted",
  Pending = "Pending",
  Disabled = "Disabled",
}
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
  const [accessChange, setAccessChange] = useState<{
    selected: UserManagerGridVm[];
    action: AccountAccessAction;
  } | null>(null);
  const [searchText, setSearchText] = useState<string>("");
  const [statusFilter, setStatusFilter] = useState<UserStatusFilter>(UserStatusFilter.All);
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
  const loadedAccountId = useRef<string | null>(null);
  const bulkChangesFinished = useRef(false);
  useEffect(() => {
    setSelecting(false);
    setSelectedIds([]);
    setAccessChange(null);
    bulkChangesFinished.current = false;
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
    // Keep existing cards visible during background refreshes.
    if (loadedAccountId.current !== accountId) setLoading(true);
    try {
      const response = await AccountUserManagerService.GetAccountUsers(accountId);
      if (request === listRequest.current &&
          RequestRuntimeState.getCurrentAccountId() === accountId) {
        loadedAccountId.current = accountId;
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
    // Never retain a different account's user list while its replacement loads.
    loadedAccountId.current = null;
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
    if (user.accessDisabledOn) return UserStatusFilter.Disabled;
    return user.hasActiveInvitation ? UserStatusFilter.Pending : UserStatusFilter.Accepted;
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
      (user) => statusFilter === UserStatusFilter.All || getUserStatus(user) === statusFilter
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
    .filter((user) => user.userId !== currentUser?.id)
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

  const reviewAccessChanges = (
    targets: UserManagerGridVm[],
    action: AccountAccessAction = AccountAccessAction.Disable
  ) => {
    bulkChangesFinished.current = false;
    setAccessChange({ selected: targets, action });
  };

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
        label: "Enable access",
        render: (
          <PlanarianButton
            alwaysShowChildren
            icon={<RedoOutlined />}
            permissionKey={PermissionKey.Admin}
            type="default"
            onClick={() => reviewAccessChanges([user], AccountAccessAction.Enable)}
          >
            Enable access
          </PlanarianButton>
        ),
      });
    } else if (user.userId !== currentUser?.id) {
      actions.push({
        key: isPending ? "remove-invitation" : AccountAccessAction.Disable,
        label: isPending ? "Remove invitation" : "Disable access",
        render: (
          <PlanarianButton
            alwaysShowChildren
            permissionKey={PermissionKey.Admin}
            danger
            type="default"
            icon={<DeleteOutlined />}
            aria-label={isPending ? "Remove invitation" : "Disable access"}
            onClick={() => reviewAccessChanges([user])}
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
            {selecting && user.userId !== currentUser?.id && (
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
              onClick={() => reviewAccessChanges([user], AccountAccessAction.Revoke)}
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
                    { label: "All statuses", value: UserStatusFilter.All },
                    { label: "Accepted", value: UserStatusFilter.Accepted },
                    { label: "Pending", value: UserStatusFilter.Pending },
                    { label: "Access disabled", value: UserStatusFilter.Disabled },
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
              alwaysShowChildren
              icon={<CheckSquareOutlined />}
              aria-label="Disable selected access"
              permissionKey={PermissionKey.Admin}
              disabled={selectedUsers.length === 0 ||
                selectedUsers.some((user) => !!user.accessDisabledOn)}
              onClick={() => reviewAccessChanges([...selectedUsers], AccountAccessAction.Disable)}
            >
              Disable access
            </PlanarianButton>
            <PlanarianButton
              alwaysShowChildren
              icon={<RedoOutlined />}
              aria-label="Enable selected access"
              permissionKey={PermissionKey.Admin}
              disabled={selectedUsers.length === 0 ||
                selectedUsers.some((user) => !user.accessDisabledOn || user.hasActiveInvitation)}
              onClick={() => reviewAccessChanges([...selectedUsers], AccountAccessAction.Enable)}
            >
              Enable access
            </PlanarianButton>
            <PlanarianButton
              alwaysShowChildren
              icon={<DeleteOutlined />}
              aria-label="Revoke selected memberships"
              permissionKey={PermissionKey.Admin}
              danger
              disabled={selectedUsers.length === 0}
              onClick={() => reviewAccessChanges([...selectedUsers], AccountAccessAction.Revoke)}
            >
              Revoke memberships
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
      <AccountAccessChangeModal
        open={accessChange !== null}
        action={accessChange?.action ?? AccountAccessAction.Disable}
        selected={accessChange?.selected ?? []}
        onClose={() => {
          setAccessChange(null);
          if (bulkChangesFinished.current) {
            setSelectedIds([]);
            setSelecting(false);
          }
          bulkChangesFinished.current = false;
        }}
        onFinished={async () => {
          bulkChangesFinished.current = true;
          await fetchUsers();
        }}
      />
    </>
  );
};

export { UserManagerComponent };
