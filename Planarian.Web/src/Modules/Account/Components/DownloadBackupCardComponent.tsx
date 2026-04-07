import { DownloadOutlined } from "@ant-design/icons";
import { Alert, Card, Progress, Space, Typography, message } from "antd";
import { AxiosProgressEvent } from "axios";
import { useCallback, useState } from "react";
import { saveAs } from "file-saver";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import { ShouldDisplay } from "../../../Shared/Permissioning/Components/ShouldDisplay";
import { NotificationComponent } from "../../Import/Components/NotificationComponent";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { AccountService } from "../Services/AccountService";

const DownloadBackupCardComponent: React.FC = () => {
    const [isDownloadingBackup, setIsDownloadingBackup] = useState<boolean>(false);
    const [backupProgressGroupName, setBackupProgressGroupName] = useState<string | null>(null);
    const [hasStartedBackupDownload, setHasStartedBackupDownload] = useState<boolean>(false);
    const [backupDownloadBytesLoaded, setBackupDownloadBytesLoaded] = useState<number>(0);
    const [backupDownloadBytesTotal, setBackupDownloadBytesTotal] = useState<number | null>(null);
    const [backupDownloadSucceeded, setBackupDownloadSucceeded] = useState<boolean>(false);

    const formatBytesToMb = (bytes: number) => {
        return (bytes / (1024 * 1024)).toFixed(1);
    };

    const onBackupDownloadProgress = useCallback(
        (progressEvent: AxiosProgressEvent) => {
            setBackupDownloadBytesLoaded(progressEvent.loaded);
            setBackupDownloadBytesTotal(progressEvent.total ?? null);
        },
        []
    );

    const startBackupDownload = useCallback(async () => {
        if (
            !backupProgressGroupName ||
            hasStartedBackupDownload ||
            !isDownloadingBackup
        ) {
            return;
        }

        try {
            setHasStartedBackupDownload(true);
            const { blob, fileName } = await AccountService.DownloadBackup(
                backupProgressGroupName,
                onBackupDownloadProgress
            );
            saveAs(blob, fileName);
            setBackupDownloadSucceeded(true);
        } catch (err) {
            const error = err as ApiErrorResponse;
            message.error(error.message);
        } finally {
            setIsDownloadingBackup(false);
            setHasStartedBackupDownload(false);
            setBackupProgressGroupName(null);
            setBackupDownloadBytesLoaded(0);
            setBackupDownloadBytesTotal(null);
        }
    }, [
        backupProgressGroupName,
        hasStartedBackupDownload,
        isDownloadingBackup,
        onBackupDownloadProgress,
    ]);

    const onDownloadBackup = () => {
        if (isDownloadingBackup) {
            return;
        }

        setIsDownloadingBackup(true);
        setHasStartedBackupDownload(false);
        setBackupDownloadSucceeded(false);
        setBackupProgressGroupName(crypto.randomUUID());
        setBackupDownloadBytesLoaded(0);
        setBackupDownloadBytesTotal(null);
    };

    const isBackupDownloadStreaming = backupDownloadBytesLoaded > 0;
    const backupDownloadPercent =
        backupDownloadBytesTotal && backupDownloadBytesTotal > 0
            ? Math.round((backupDownloadBytesLoaded / backupDownloadBytesTotal) * 100)
            : undefined;
    const backupDownloadMessage = isBackupDownloadStreaming
        ? backupDownloadBytesTotal && backupDownloadBytesTotal > 0
            ? `Downloading ${formatBytesToMb(backupDownloadBytesLoaded)} MB of ${formatBytesToMb(
                backupDownloadBytesTotal
            )} MB`
            : `Downloading ${formatBytesToMb(backupDownloadBytesLoaded)} MB`
        : null;
    const isPreparingBackup = isDownloadingBackup && !isBackupDownloadStreaming;

    return (
        <ShouldDisplay permissionKey={PermissionKey.Admin}>
            <Card title="Download Backup">
                <Space direction="vertical" size="middle" style={{ width: "100%" }}>
                    <Typography.Paragraph>
                        Create and download a ZIP archive of your account data,
                        including caves, entrances, files, and line plots.
                    </Typography.Paragraph>

                    {backupDownloadSucceeded && !isDownloadingBackup && (
                        <Alert
                            message="Backup download started successfully"
                            type="success"
                            showIcon
                        />
                    )}

                    {!isDownloadingBackup && (
                        <PlanarianButton
                            icon={<DownloadOutlined />}
                            onClick={onDownloadBackup}
                        >
                            Download Backup
                        </PlanarianButton>
                    )}

                    {backupProgressGroupName && (
                        <Space direction="vertical" size="middle" style={{ width: "100%" }}>
                            {backupDownloadMessage && (
                                <Alert
                                    message={backupDownloadMessage}
                                    description={backupDownloadBytesTotal && backupDownloadBytesTotal > 0
                                        ? `${backupDownloadPercent ?? 0}% complete`
                                        : undefined}
                                    type="info"
                                    showIcon
                                />
                            )}
                            {backupDownloadMessage && backupDownloadPercent !== undefined && (
                                <Progress
                                    percent={backupDownloadPercent}
                                    status="active"
                                    showInfo
                                />
                            )}
                            {!isBackupDownloadStreaming && (
                                <NotificationComponent
                                    groupName={backupProgressGroupName}
                                    isLoading={isPreparingBackup}
                                    onConnected={startBackupDownload}
                                />
                            )}
                        </Space>
                    )}
                </Space>
            </Card>
        </ShouldDisplay>
    );
};

export { DownloadBackupCardComponent };