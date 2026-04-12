import { CloseOutlined, DownloadOutlined } from "@ant-design/icons";
import { Alert, Card, Progress, Space, Spin, Typography, message } from "antd";
import { useCallback, useEffect, useState } from "react";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import { AuthenticationService } from "../../Authentication/Services/AuthenticationService";
import { ShouldDisplay } from "../../../Shared/Permissioning/Components/ShouldDisplay";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { DeleteButtonComponent } from "../../../Shared/Components/Buttons/DeleteButtonComponent";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { useSignalRGroup } from "../../../Shared/Components/SignalRProgress/useSignalRGroup";
import { ProgressState } from "../../../Shared/Models/ProgressState";
import { AccountService } from "../Services/AccountService";
import { ArchiveProgressVm } from "../Models/Archive/ArchiveProgressVm";
import { ArchiveListItemVm } from "../Models/Archive/ArchiveListItemVm";

const initialArchiveProgress: ArchiveProgressVm = {
    statusMessage: "Preparing archive...",
    processedCount: 0,
    totalCount: 0,
};

const ArchiveCardComponent: React.FC = () => {
    const accountId = AuthenticationService.GetAccountId();
    const archiveProgressGroupName = accountId ? `archive-${accountId}` : null;

    const [isArchiveRunning, setIsArchiveRunning] = useState<boolean>(false);
    const [isCancelingArchive, setIsCancelingArchive] = useState<boolean>(false);
    const [deletingArchiveBlobKey, setDeletingArchiveBlobKey] = useState<string | null>(null);
    const [downloadingArchiveBlobKey, setDownloadingArchiveBlobKey] = useState<string | null>(null);
    const [isLoadingArchiveState, setIsLoadingArchiveState] = useState<boolean>(true);
    const [archiveProgress, setArchiveProgress] = useState<ArchiveProgressVm>(initialArchiveProgress);
    const [recentArchives, setRecentArchives] = useState<ArchiveListItemVm[]>([]);
    const [completedArchiveFileName, setCompletedArchiveFileName] = useState<string | null>(null);

    const loadArchiveState = useCallback(async (showLoadingState: boolean = false) => {
        if (showLoadingState) {
            setIsLoadingArchiveState(true);
        }

        try {
            const [archiveStatus, archives] = await Promise.all([
                AccountService.GetArchiveStatus(),
                AccountService.GetRecentArchives(),
            ]);

            setRecentArchives(archives);

            if (archiveStatus) {
                setArchiveProgress(archiveStatus);
                setIsArchiveRunning(archiveStatus.state === ProgressState.Running);
                return;
            }

            setIsArchiveRunning(false);
            setArchiveProgress(initialArchiveProgress);
        } catch (err) {
            const error = err as ApiErrorResponse;
            message.error(error.message);
        } finally {
            if (showLoadingState) {
                setIsLoadingArchiveState(false);
            }
        }
    }, []);

    const onArchiveProgress = useCallback((notification: unknown) => {
        if (!notification || typeof notification !== "object") {
            return;
        }

        const nextProgress = notification as ArchiveProgressVm;
        setArchiveProgress((currentProgress) => ({
            statusMessage: nextProgress.statusMessage || currentProgress.statusMessage,
            processedCount: nextProgress.processedCount ?? currentProgress.processedCount,
            totalCount: nextProgress.totalCount ?? currentProgress.totalCount,
            state: nextProgress.state ?? currentProgress.state,
        }));

        if (nextProgress.state === ProgressState.Completed) {
            setCompletedArchiveFileName(nextProgress.fileName ?? "Archive Ready");
            message.success(nextProgress.fileName
                ? `${nextProgress.fileName} is ready to download below.`
                : "Archive is ready to download below.");
            setIsArchiveRunning(false);
            setArchiveProgress(initialArchiveProgress);
            void loadArchiveState();
            return;
        }

        if (nextProgress.state === ProgressState.Failed) {
            message.error(nextProgress.statusMessage);
            setIsArchiveRunning(false);
            setArchiveProgress(initialArchiveProgress);
            void loadArchiveState();
            return;
        }

        if (nextProgress.state === ProgressState.Canceled) {
            message.info(nextProgress.statusMessage || "Archive canceled.");
            setIsArchiveRunning(false);
            setArchiveProgress(initialArchiveProgress);
            void loadArchiveState();
            return;
        }

        setIsArchiveRunning(true);
    }, [loadArchiveState]);

    useSignalRGroup({
        groupName: archiveProgressGroupName,
        onConnected: loadArchiveState,
        onNotification: onArchiveProgress,
    });

    const startArchiveDownload = useCallback(async () => {
        if (isArchiveRunning || isCancelingArchive) {
            return;
        }

        try {
            setIsArchiveRunning(true);
            setArchiveProgress(initialArchiveProgress);
            await AccountService.CreateArchive();
            await loadArchiveState();
        } catch (err) {
            const error = err as ApiErrorResponse;
            message.error(error.message);
            setIsArchiveRunning(false);
            await loadArchiveState();
        }
    }, [isArchiveRunning, isCancelingArchive, loadArchiveState]);

    const cancelArchive = useCallback(async () => {
        if (!isArchiveRunning || isCancelingArchive) {
            return;
        }

        try {
            setIsCancelingArchive(true);
            await AccountService.CancelArchive();
        } catch (err) {
            const error = err as ApiErrorResponse;
            message.error(error.message);
        } finally {
            setIsCancelingArchive(false);
        }
    }, [isArchiveRunning, isCancelingArchive]);

    const deleteArchive = useCallback(async (archive: ArchiveListItemVm) => {
        if (deletingArchiveBlobKey) {
            return;
        }

        try {
            setDeletingArchiveBlobKey(archive.blobKey);
            await AccountService.DeleteArchive(archive.blobKey);
            setRecentArchives((currentArchives) =>
                currentArchives.filter((currentArchive) => currentArchive.blobKey !== archive.blobKey)
            );
            message.success(`${archive.fileName} deleted.`);
        } catch (err) {
            const error = err as ApiErrorResponse;
            message.error(error.message);
        } finally {
            setDeletingArchiveBlobKey(null);
        }
    }, [deletingArchiveBlobKey]);

    const downloadArchive = useCallback(async (archive: ArchiveListItemVm) => {
        if (downloadingArchiveBlobKey || deletingArchiveBlobKey === archive.blobKey) {
            return;
        }

        try {
            setDownloadingArchiveBlobKey(archive.blobKey);
            await AccountService.StartArchiveDownload(archive.blobKey);
        } catch (err) {
            const error = err as ApiErrorResponse;
            message.error(error.message);
        } finally {
            setDownloadingArchiveBlobKey(null);
        }
    }, [deletingArchiveBlobKey, downloadingArchiveBlobKey]);

    useEffect(() => {
        if (!archiveProgressGroupName) {
            return;
        }

        void loadArchiveState(true);
    }, [archiveProgressGroupName, loadArchiveState]);

    const archiveProgressTotal = archiveProgress.totalCount ?? 0;
    const archiveProgressProcessed = archiveProgress.processedCount ?? 0;
    const archiveProgressPercent = archiveProgressTotal > 0
        ? Math.min(100, Math.round((archiveProgressProcessed / archiveProgressTotal) * 100))
        : 0;

    return (
        <ShouldDisplay permissionKey={PermissionKey.Admin}>
            <Card
                title="Archive"
                extra={isArchiveRunning ? (
                    <PlanarianButton icon={<CloseOutlined />} onClick={cancelArchive} loading={isCancelingArchive}>
                        Cancel Export
                    </PlanarianButton>
                ) : !completedArchiveFileName && !isLoadingArchiveState ? (
                    <PlanarianButton
                        icon={<DownloadOutlined />}
                        onClick={startArchiveDownload}
                    >
                        Create Archive
                    </PlanarianButton>
                ) : null}
            >
                <Space direction="vertical" size="middle" style={{ width: "100%" }}>
                    {/* Intentional: after a successful archive completes, keep the success state visible
                        and hide the create action until the page is reloaded. */}
                    <Typography.Paragraph>
                        Create and download an archive of your account data,
                        including caves, entrances, files, and line plots.
                    </Typography.Paragraph>

                    {completedArchiveFileName && !isArchiveRunning && (
                        <Alert
                            message="Archive Ready"
                            description={`${completedArchiveFileName} is available in the recent archives list below.`}
                            type="success"
                            showIcon
                        />
                    )}

                    {isArchiveRunning && (
                        <Space direction="vertical" size="middle" style={{ width: "100%" }}>
                            <Space direction="vertical" size="small" style={{ width: "100%" }}>
                                <Typography.Text strong>
                                    Creating Archive
                                </Typography.Text>
                                <Progress
                                    percent={archiveProgressPercent}
                                    status={isArchiveRunning ? "active" : "normal"}
                                    showInfo={false}
                                />
                                <Typography.Text type="secondary">
                                    {archiveProgress.statusMessage}
                                </Typography.Text>
                            </Space>
                        </Space>
                    )}

                    {isLoadingArchiveState && !isArchiveRunning && (
                        <Space direction="vertical" size="small" style={{ width: "100%" }}>
                            <Typography.Text strong>
                                Loading Archive Status
                            </Typography.Text>
                            <Spin />
                        </Space>
                    )}

                    <Space direction="vertical" size="small" style={{ width: "100%" }}>
                        <Typography.Text strong>
                            Recent Archives
                        </Typography.Text>
                        <Typography.Text type="secondary">
                            Only the 5 most recent archives are kept. Older archives are automatically deleted after a new archive is created.
                        </Typography.Text>
                        {isLoadingArchiveState && (
                            <Typography.Text type="secondary">
                                Loading recent archives...
                            </Typography.Text>
                        )}
                        {!isLoadingArchiveState && recentArchives.length === 0 && (
                            <Typography.Text type="secondary">
                                No archives available yet.
                            </Typography.Text>
                        )}
                        {recentArchives.map((archive) => (
                            <Space
                                key={archive.blobKey}
                                style={{ width: "100%", justifyContent: "space-between" }}
                            >
                                <Space direction="vertical" size={0}>
                                    <Typography.Text>{archive.fileName}</Typography.Text>
                                    <Typography.Text type="secondary">
                                        {new Date(archive.createdAt).toLocaleString()}
                                    </Typography.Text>
                                </Space>
                                <Space>
                                    <DeleteButtonComponent
                                        type="link"
                                        danger={false}
                                        title="Delete archive"
                                        description={`Delete ${archive.fileName}?`}
                                        onConfirm={() => deleteArchive(archive)}
                                        loading={deletingArchiveBlobKey === archive.blobKey}
                                    >
                                        Delete
                                    </DeleteButtonComponent>
                                    <PlanarianButton
                                        type="link"
                                        icon={<DownloadOutlined />}
                                        onClick={() => downloadArchive(archive)}
                                        loading={downloadingArchiveBlobKey === archive.blobKey}
                                        disabled={deletingArchiveBlobKey === archive.blobKey}
                                    >
                                        Download
                                    </PlanarianButton>
                                </Space>
                            </Space>
                        ))}
                    </Space>
                </Space>
            </Card>
        </ShouldDisplay>
    );
};

export { ArchiveCardComponent };
