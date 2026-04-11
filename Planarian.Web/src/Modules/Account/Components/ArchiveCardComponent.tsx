import { DownloadOutlined } from "@ant-design/icons";
import { Alert, Card, Progress, Space, Typography, message } from "antd";
import { useCallback, useState } from "react";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import { ShouldDisplay } from "../../../Shared/Permissioning/Components/ShouldDisplay";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { SignalRProgressComponent } from "../../../Shared/Components/SignalRProgress/SignalRProgressComponent";
import { ProgressVm } from "../../../Shared/Models/ProgressVm";
import { AccountService } from "../Services/AccountService";

const initialArchiveProgress: ProgressVm = {
    statusMessage: "Preparing archive...",
    processedCount: 0,
    totalCount: 0,
};

const startBrowserDownload = (downloadUrl: string) => {
    const link = document.createElement("a");
    link.href = downloadUrl;
    link.rel = "noopener noreferrer";
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};

const ArchiveCardComponent: React.FC = () => {
    const [isPreparingArchive, setIsPreparingArchive] = useState<boolean>(false);
    const [archiveDownloadSucceeded, setArchiveDownloadSucceeded] = useState<boolean>(false);
    const [archiveFileName, setArchiveFileName] = useState<string | null>(null);
    const [archiveProgress, setArchiveProgress] = useState<ProgressVm>(initialArchiveProgress);
    const [archiveProgressGroupName, setArchiveProgressGroupName] = useState<string | null>(null);
    const [hasStartedArchiveRequest, setHasStartedArchiveRequest] = useState<boolean>(false);

    const onArchiveProgress = useCallback((notification: unknown) => {
        if (!notification || typeof notification !== "object") {
            return;
        }

        const nextProgress = notification as ProgressVm;
        setArchiveProgress((currentProgress) => ({
            statusMessage: nextProgress.statusMessage || currentProgress.statusMessage,
            processedCount: nextProgress.processedCount ?? currentProgress.processedCount,
            totalCount: nextProgress.totalCount ?? currentProgress.totalCount,
        }));
    }, []);

    const startArchiveDownload = useCallback(async () => {
        if (isPreparingArchive) {
            return;
        }

        const uuid = crypto.randomUUID();
        setIsPreparingArchive(true);
        setArchiveDownloadSucceeded(false);
        setArchiveFileName(null);
        setArchiveProgress(initialArchiveProgress);
        setArchiveProgressGroupName(uuid);
        setHasStartedArchiveRequest(false);
    }, [isPreparingArchive]);

    const onConnected = useCallback(async () => {
        if (!archiveProgressGroupName || hasStartedArchiveRequest) {
            return;
        }

        setHasStartedArchiveRequest(true);
        try {
            const response = await AccountService.CreateArchive(archiveProgressGroupName);
            setArchiveFileName(response.fileName);
            setArchiveDownloadSucceeded(true);
            startBrowserDownload(response.downloadUrl);
        } catch (err) {
            const error = err as ApiErrorResponse;
            message.error(error.message);
        } finally {
            setIsPreparingArchive(false);
            setHasStartedArchiveRequest(false);
            setArchiveProgressGroupName(null);
        }
    }, [archiveProgressGroupName, hasStartedArchiveRequest]);

    const archiveProgressTotal = archiveProgress.totalCount ?? 0;
    const archiveProgressProcessed = archiveProgress.processedCount ?? 0;

    const archiveProgressPercent = archiveProgressTotal > 0
        ? Math.min(
            100,
            Math.round(
                (archiveProgressProcessed /
                    archiveProgressTotal) *
                100
            )
        )
        : 0;

    return (
        <ShouldDisplay permissionKey={PermissionKey.Admin}>
            <Card title="Archive">
                <Space direction="vertical" size="middle" style={{ width: "100%" }}>
                    <Typography.Paragraph>
                        Create and download a ZIP archive of your account data,
                        including caves, entrances, files, and line plots.
                    </Typography.Paragraph>

                    {archiveDownloadSucceeded && !isPreparingArchive && (
                        <Alert
                            message="Archive download started successfully"
                            description={archiveFileName
                                ? `${archiveFileName} should begin downloading in your browser shortly.`
                                : "Your browser download should begin shortly."}
                            type="success"
                            showIcon
                        />
                    )}

                    {!archiveProgressGroupName && (
                        <PlanarianButton
                            icon={<DownloadOutlined />}
                            onClick={startArchiveDownload}
                            loading={isPreparingArchive}
                        >
                            Create Archive
                        </PlanarianButton>
                    )}

                    {archiveProgressGroupName && (
                        <Space direction="vertical" size="middle" style={{ width: "100%" }}>
                            <Space direction="vertical" size="small" style={{ width: "100%" }}>
                                <Typography.Text strong>
                                    Creating Archive
                                </Typography.Text>
                                <Progress
                                    percent={archiveProgressPercent}
                                    status={isPreparingArchive ? "active" : "normal"}
                                    showInfo={false}
                                />
                                <Typography.Text type="secondary">
                                    {archiveProgress.statusMessage}
                                </Typography.Text>
                            </Space>
                            <SignalRProgressComponent
                                groupName={archiveProgressGroupName}
                                isLoading={isPreparingArchive}
                                onConnected={onConnected}
                                onNotification={onArchiveProgress}
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
