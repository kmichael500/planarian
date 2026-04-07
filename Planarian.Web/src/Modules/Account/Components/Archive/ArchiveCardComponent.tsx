import { DownloadOutlined } from "@ant-design/icons";
import { Alert, Card, Progress, Space, Typography, message } from "antd";
import { useCallback, useEffect, useRef, useState } from "react";
import { PermissionKey } from "../../../Authentication/Models/PermissionKey";
import { ShouldDisplay } from "../../../../Shared/Permissioning/Components/ShouldDisplay";
import { NotificationComponent } from "../../../Import/Components/NotificationComponent";
import { ApiErrorResponse } from "../../../../Shared/Models/ApiErrorResponse";
import { PlanarianButton } from "../../../../Shared/Components/Buttons/PlanarianButtton";
import { AuthenticationService } from "../../../Authentication/Services/AuthenticationService";
import { AccountService } from "../../Services/AccountService";
import { BackupProgressVm } from "../../Models/Archive/BackupProgressVm";

const initialBackupProgress: BackupProgressVm = {
    statusMessage: "Gathering cave data...",
    processedCaves: 0,
    totalCaves: 0,
};

const MAX_ARCHIVES_PER_DAY = 2;
const ARCHIVE_USAGE_STORAGE_KEY_PREFIX = "planarian.account.archive.daily-usage";

interface ArchiveUsageRecord {
    dayKey: string;
    count: number;
}

const getTodayKey = () => new Date().toISOString().slice(0, 10);

const getArchiveUsageStorageKey = (userId: string, accountId: string) => {
    return `${ARCHIVE_USAGE_STORAGE_KEY_PREFIX}.${userId}.${accountId}`;
};

const readArchiveUsageCount = (storageKey: string): number => {
    try {
        const storedValue = window.localStorage.getItem(storageKey);
        if (!storedValue) {
            return 0;
        }

        const parsedValue = JSON.parse(storedValue) as ArchiveUsageRecord;
        if (parsedValue.dayKey !== getTodayKey()) {
            return 0;
        }

        return Number.isFinite(parsedValue.count) ? parsedValue.count : 0;
    } catch {
        return 0;
    }
};

const incrementArchiveUsageCount = (storageKey: string): number => {
    const nextCount = readArchiveUsageCount(storageKey) + 1;
    const nextUsageRecord: ArchiveUsageRecord = {
        dayKey: getTodayKey(),
        count: nextCount,
    };

    try {
        window.localStorage.setItem(storageKey, JSON.stringify(nextUsageRecord));
    } catch {
    }

    return nextCount;
};

const ArchiveCardComponent: React.FC = () => {
    const userId = AuthenticationService.GetUserId();
    const accountId = AuthenticationService.GetAccountId();

    const archiveUsageStorageKey = getArchiveUsageStorageKey(
        userId!,
        accountId!
    );
    const [isPreparingBackup, setIsPreparingBackup] = useState<boolean>(false);
    const [backupProgressGroupName, setBackupProgressGroupName] = useState<string | null>(null);
    const [hasStartedBackupRequest, setHasStartedBackupRequest] = useState<boolean>(false);
    const [hasCompletedBackupProcessing, setHasCompletedBackupProcessing] = useState<boolean>(false);
    const [backupDownloadSucceeded, setBackupDownloadSucceeded] = useState<boolean>(false);
    const [backupFileName, setBackupFileName] = useState<string | null>(null);
    const [backupProgress, setBackupProgress] = useState<BackupProgressVm>(initialBackupProgress);
    const [dailyArchiveCount, setDailyArchiveCount] = useState<number>(() => readArchiveUsageCount(archiveUsageStorageKey));
    const downloadFrameRef = useRef<HTMLIFrameElement | null>(null);

    const hasReachedDailyArchiveLimit = dailyArchiveCount >= MAX_ARCHIVES_PER_DAY;
    const remainingArchivesToday = Math.max(0, MAX_ARCHIVES_PER_DAY - dailyArchiveCount);

    useEffect(() => {
        return () => {
            if (downloadFrameRef.current) {
                downloadFrameRef.current.remove();
                downloadFrameRef.current = null;
            }
        };
    }, []);

    const onBackupProgress = useCallback((notification: unknown) => {
        if (!notification || typeof notification !== "object") {
            return;
        }

        const nextProgress = notification as BackupProgressVm;
        setBackupProgress((currentProgress) => ({
            statusMessage: nextProgress.statusMessage || currentProgress.statusMessage,
            processedCaves: nextProgress.processedCaves ?? currentProgress.processedCaves,
            totalCaves: nextProgress.totalCaves ?? currentProgress.totalCaves,
        }));
    }, []);

    const startBrowserDownload = useCallback((downloadUrl: string) => {
        try {
            if (downloadFrameRef.current) {
                downloadFrameRef.current.remove();
                downloadFrameRef.current = null;
            }

            const frame = document.createElement("iframe");
            frame.style.display = "none";
            frame.setAttribute("aria-hidden", "true");
            frame.src = downloadUrl;
            document.body.appendChild(frame);
            downloadFrameRef.current = frame;

            window.setTimeout(() => {
                if (downloadFrameRef.current === frame) {
                    frame.remove();
                    downloadFrameRef.current = null;
                }
            }, 60_000);
        } catch {
            window.location.assign(downloadUrl);
        }
    }, []);

    const startBackupDownload = useCallback(async () => {
        if (
            !backupProgressGroupName ||
            hasStartedBackupRequest ||
            !isPreparingBackup
        ) {
            return;
        }

        try {
            setHasStartedBackupRequest(true);
            const { downloadUrl, fileName } = await AccountService.DownloadBackup(
                backupProgressGroupName
            );
            setDailyArchiveCount(incrementArchiveUsageCount(archiveUsageStorageKey));
            setHasCompletedBackupProcessing(true);
            setBackupFileName(fileName);
            setBackupDownloadSucceeded(true);
            startBrowserDownload(downloadUrl);
        } catch (err) {
            const error = err as ApiErrorResponse;
            setHasCompletedBackupProcessing(false);
            message.error(error.message);
        } finally {
            setIsPreparingBackup(false);
            setHasStartedBackupRequest(false);
            setBackupProgressGroupName(null);
        }
    }, [
        backupProgressGroupName,
        hasStartedBackupRequest,
        isPreparingBackup,
        archiveUsageStorageKey,
        startBrowserDownload,
    ]);

    const onDownloadBackup = () => {
        if (isPreparingBackup) {
            return;
        }

        if (hasReachedDailyArchiveLimit) {
            message.warning(`You can create up to ${MAX_ARCHIVES_PER_DAY} archives per day.`);
            return;
        }

        setIsPreparingBackup(true);
        setHasCompletedBackupProcessing(false);
        setHasStartedBackupRequest(false);
        setBackupDownloadSucceeded(false);
        setBackupFileName(null);
        setBackupProgress(initialBackupProgress);
        setBackupProgressGroupName(crypto.randomUUID());
    };

    const backupProgressPercent = (backupProgress.totalCaves ?? 0) > 0
        ? Math.min(100, Math.round(((backupProgress.processedCaves ?? 0) / (backupProgress.totalCaves ?? 0)) * 100))
        : 0;

    return (
        <ShouldDisplay permissionKey={PermissionKey.Admin}>
            <Card title="Archive">
                <Space direction="vertical" size="middle" style={{ width: "100%" }}>
                    <Typography.Paragraph>
                        Create and download a ZIP archive of your account data,
                        including caves, entrances, files, and line plots.
                    </Typography.Paragraph>

                    <Typography.Text type="secondary">
                        {dailyArchiveCount} of {MAX_ARCHIVES_PER_DAY} archives used today.
                        {remainingArchivesToday > 0
                            ? ` ${remainingArchivesToday} remaining.`
                            : " Daily limit reached."}
                    </Typography.Text>

                    {backupDownloadSucceeded && !isPreparingBackup && (
                        <Alert
                            message="Archive download started successfully"
                            description={backupFileName
                                ? `${backupFileName} should begin downloading in your browser shortly.`
                                : "Your browser download should begin shortly."}
                            type="success"
                            showIcon
                        />
                    )}

                    {!isPreparingBackup && !hasCompletedBackupProcessing && (
                        <PlanarianButton
                            icon={<DownloadOutlined />}
                            onClick={onDownloadBackup}
                            disabled={hasReachedDailyArchiveLimit}
                        >
                            Create Archive
                        </PlanarianButton>
                    )}

                    {backupProgressGroupName && (
                        <Space direction="vertical" size="middle" style={{ width: "100%" }}>
                            <Space direction="vertical" size="small" style={{ width: "100%" }}>
                                <Typography.Text strong>
                                    Creating Archive
                                </Typography.Text>
                                <Progress
                                    percent={backupProgressPercent}
                                    status={isPreparingBackup ? "active" : "normal"}
                                    showInfo={false}
                                />
                                <Typography.Text type="secondary">
                                    {backupProgress.statusMessage}
                                </Typography.Text>
                            </Space>
                            <NotificationComponent
                                groupName={backupProgressGroupName}
                                isLoading={isPreparingBackup}
                                onConnected={startBackupDownload}
                                onNotification={onBackupProgress}
                                hideNotifications={true}
                            />
                        </Space>
                    )}
                </Space>
            </Card>
        </ShouldDisplay>
    );
};

export { ArchiveCardComponent };