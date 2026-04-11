import { CloseOutlined, DownloadOutlined } from "@ant-design/icons";
import { Alert, Card, Progress, Space, Spin, Typography, message } from "antd";
import { useCallback, useEffect, useState } from "react";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import { AuthenticationService } from "../../Authentication/Services/AuthenticationService";
import { ShouldDisplay } from "../../../Shared/Permissioning/Components/ShouldDisplay";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { DeleteButtonComponent } from "../../../Shared/Components/Buttons/DeleteButtonComponent";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { SignalRProgressComponent } from "../../../Shared/Components/SignalRProgress/SignalRProgressComponent";
import { ProgressVm } from "../../../Shared/Models/ProgressVm";
import { AccountService } from "../Services/AccountService";
import { ArchiveProgressVm } from "../Models/Archive/ArchiveProgressVm";
import { ArchiveListItemVm } from "../Models/Archive/ArchiveListItemVm";

const initialArchiveProgress: ProgressVm = {
    statusMessage: "Preparing archive...",
    processedCount: 0,
    totalCount: 0,
};

const ArchiveCardComponent: React.FC = () => {
    const accountId = AuthenticationService.GetAccountId();
    const archiveProgressGroupName = accountId ? `archive-${accountId}` : null;

    const [isPreparingArchive, setIsPreparingArchive] = useState<boolean>(false);
    const [isCancelingArchive, setIsCancelingArchive] = useState<boolean>(false);
    const [deletingArchiveBlobKey, setDeletingArchiveBlobKey] = useState<string | null>(null);
    const [isLoadingArchiveState, setIsLoadingArchiveState] = useState<boolean>(true);
    const [isArchiveActive, setIsArchiveActive] = useState<boolean>(false);
    const [latestArchiveFileName, setLatestArchiveFileName] = useState<string | null>(null);
    const [archiveProgress, setArchiveProgress] = useState<ProgressVm>(initialArchiveProgress);
    const [recentArchives, setRecentArchives] = useState<ArchiveListItemVm[]>([]);

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

            if (archiveStatus?.isActive) {
                setArchiveProgress({
                    statusMessage: archiveStatus.statusMessage,
                    processedCount: archiveStatus.processedCount,
                    totalCount: archiveStatus.totalCount,
                    message: archiveStatus.message,
                    isError: archiveStatus.isError,
                    isCanceled: archiveStatus.isCanceled,
                });
                setIsArchiveActive(true);
                setIsPreparingArchive(true);
                return;
            }

            setIsArchiveActive(false);
            setIsPreparingArchive(false);
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
            message: nextProgress.message ?? currentProgress.message,
            isError: nextProgress.isError ?? currentProgress.isError,
            isCanceled: nextProgress.isCanceled ?? currentProgress.isCanceled,
        }));

        if (nextProgress.isComplete) {
            if (nextProgress.isError) {
                message.error(nextProgress.message || nextProgress.statusMessage);
            } else if (nextProgress.isCanceled) {
                message.info(nextProgress.statusMessage || "Archive canceled.");
            } else {
                setLatestArchiveFileName(nextProgress.fileName ?? null);
                message.success(nextProgress.fileName
                    ? `${nextProgress.fileName} is ready to download below.`
                    : "Archive is ready to download below.");
            }

            setIsArchiveActive(false);
            setIsPreparingArchive(false);
            setArchiveProgress(initialArchiveProgress);
            void AccountService.GetRecentArchives().then(setRecentArchives);
            return;
        }

        setIsArchiveActive(true);
        setIsPreparingArchive(true);
    }, []);

    const startArchiveDownload = useCallback(async () => {
        if (isPreparingArchive || isCancelingArchive) {
            return;
        }

        try {
            setLatestArchiveFileName(null);
            setIsPreparingArchive(true);
            setIsArchiveActive(true);
            setArchiveProgress(initialArchiveProgress);
            await AccountService.CreateArchive();
            await loadArchiveState();
        } catch (err) {
            const error = err as ApiErrorResponse;
            message.error(error.message);
            await loadArchiveState();
        }
    }, [isPreparingArchive, isCancelingArchive, loadArchiveState]);

    const cancelArchive = useCallback(async () => {
        if (!isArchiveActive || isCancelingArchive) {
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
    }, [isArchiveActive, isCancelingArchive]);

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
            if (latestArchiveFileName === archive.fileName) {
                setLatestArchiveFileName(null);
            }
            message.success(`${archive.fileName} deleted.`);
        } catch (err) {
            const error = err as ApiErrorResponse;
            message.error(error.message);
        } finally {
            setDeletingArchiveBlobKey(null);
        }
    }, [deletingArchiveBlobKey, latestArchiveFileName]);

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
            <Card title="Archive">
                <Space direction="vertical" size="middle" style={{ width: "100%" }}>
                    <Typography.Paragraph>
                        Create and download a ZIP archive of your account data,
                        including caves, entrances, files, and line plots.
                    </Typography.Paragraph>
                    <Typography.Text type="secondary">
                        Only the 5 most recent archives are kept. Older archives are automatically deleted after a new archive is created.
                    </Typography.Text>

                    {latestArchiveFileName && !isPreparingArchive && (
                        <Alert
                            message="Archive ready"
                            description={`${latestArchiveFileName} is available in the recent archives list below.`}
                            type="success"
                            showIcon
                        />
                    )}

                    {!isArchiveActive && !isLoadingArchiveState && (
                        <PlanarianButton
                            icon={<DownloadOutlined />}
                            onClick={startArchiveDownload}
                            loading={isPreparingArchive}
                        >
                            Create Archive
                        </PlanarianButton>
                    )}

                    {isArchiveActive && (
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
                            <PlanarianButton icon={<CloseOutlined />} onClick={cancelArchive} loading={isCancelingArchive}>
                                Cancel Export
                            </PlanarianButton>
                        </Space>
                    )}

                    {isLoadingArchiveState && !isArchiveActive && (
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
                                    <Typography.Link href={archive.downloadUrl}>
                                        <DownloadOutlined />{" "}
                                        Download
                                    </Typography.Link>
                                </Space>
                            </Space>
                        ))}
                    </Space>

                    {archiveProgressGroupName && (
                        <SignalRProgressComponent
                            groupName={archiveProgressGroupName}
                            isLoading={isPreparingArchive}
                            onNotification={onArchiveProgress}
                            hideNotifications={true}
                        />
                    )}
                </Space>
            </Card>
        </ShouldDisplay>
    );
};

export { ArchiveCardComponent };
