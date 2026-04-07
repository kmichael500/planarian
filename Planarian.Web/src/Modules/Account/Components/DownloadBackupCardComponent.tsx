import { DownloadOutlined } from "@ant-design/icons";
import { Alert, Card, Progress, Space, Typography, message } from "antd";
import { useCallback, useState } from "react";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import { ShouldDisplay } from "../../../Shared/Permissioning/Components/ShouldDisplay";
import { NotificationComponent } from "../../Import/Components/NotificationComponent";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { AccountService } from "../Services/AccountService";
import { BackupProgressVm } from "../Models/BackupProgressVm";

const initialBackupProgress: BackupProgressVm = {
    statusMessage: "Gathering cave data...",
    processedCaves: 0,
    totalCaves: 0,
};

const ArchiveCardComponent: React.FC = () => {
    const [isPreparingBackup, setIsPreparingBackup] = useState<boolean>(false);
    const [backupProgressGroupName, setBackupProgressGroupName] = useState<string | null>(null);
    const [hasStartedBackupRequest, setHasStartedBackupRequest] = useState<boolean>(false);
    const [hasClickedDownloadBackup, setHasClickedDownloadBackup] = useState<boolean>(false);
    const [backupDownloadSucceeded, setBackupDownloadSucceeded] = useState<boolean>(false);
    const [backupFileName, setBackupFileName] = useState<string | null>(null);
    const [backupProgress, setBackupProgress] = useState<BackupProgressVm>(initialBackupProgress);

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
        const link = document.createElement("a");
        link.href = downloadUrl;
        link.target = "_blank";
        link.rel = "noopener noreferrer";
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
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
            setBackupFileName(fileName);
            setBackupDownloadSucceeded(true);
            startBrowserDownload(downloadUrl);
        } catch (err) {
            const error = err as ApiErrorResponse;
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
        startBrowserDownload,
    ]);

    const onDownloadBackup = () => {
        if (isPreparingBackup) {
            return;
        }

        setHasClickedDownloadBackup(true);
        setIsPreparingBackup(true);
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

                    {!hasClickedDownloadBackup && (
                        <PlanarianButton
                            icon={<DownloadOutlined />}
                            onClick={onDownloadBackup}
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