import { DeleteOutlined, DownloadOutlined, EyeOutlined, InboxOutlined, ReloadOutlined } from "@ant-design/icons";
import { Col, Form, Input, message, Progress, Row, Typography, Upload } from "antd";
import { FormInstance } from "antd/lib/form";
import { RcFile } from "antd/lib/upload";
import { useMemo, useState } from "react";
import { AddCaveVm } from "../Models/AddCaveVm";
import { CaveService } from "../Service/CaveService";
import { EditFileMetadataVm } from "../../Files/Models/EditFileMetadataVm";
import { TagSelectComponent } from "../../Tag/Components/TagSelectComponent";
import { TagType } from "../../Tag/Models/TagType";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { FileViewer } from "../../Files/Components/FileViewerComponent";
import { getFileType } from "../../Files/Services/FileHelpers";
import { FileService } from "../../Files/Services/FileService";
import { CaveAuthoringAssetCard } from "./CaveAuthoringAssetCard";

type UploadAttempt = {
  key: string;
  file: RcFile;
  percent: number;
  status: "uploading" | "error";
  error?: string;
};

export const CaveFileAuthoringEditor = ({
  form,
  onAuthoringChange,
  stagedFileRequestId,
  existingRequestStagedFileIds = [],
}: {
  form: FormInstance<AddCaveVm>;
  onAuthoringChange?: () => void;
  stagedFileRequestId?: string;
  existingRequestStagedFileIds?: readonly string[];
}) => {
  const files = Form.useWatch<EditFileMetadataVm[]>("files", form) ?? [];
  const [attempts, setAttempts] = useState<Record<string, UploadAttempt>>({});
  const [locallyStagedFileIds, setLocallyStagedFileIds] = useState<Set<string>>(() => new Set());
  const [activeFileId, setActiveFileId] = useState<string>();
  const requestStagedIds = useMemo(() => new Set(existingRequestStagedFileIds), [existingRequestStagedFileIds]);
  const activeFile = files.find(file => file?.id === activeFileId);

  const removeAttempt = (key: string) => setAttempts(current => {
    const next = { ...current };
    delete next[key];
    return next;
  });

  const accessFor = (file?: EditFileMetadataVm) => {
    if (!file) return {};
    if (locallyStagedFileIds.has(file.id)) return {
      viewUrl: CaveService.GetAuthoringStagedFileUrl(file.id, "view"),
      download: () => CaveService.StartAuthoringStagedFileDownload(file.id),
    };
    if (stagedFileRequestId && requestStagedIds.has(file.id)) return {
      viewUrl: CaveService.GetStagedChangeRequestFileUrl(stagedFileRequestId, file.id, "view"),
      download: () => CaveService.StartStagedChangeRequestFileDownload(stagedFileRequestId, file.id),
    };
    return {};
  };

  const activeAccess = accessFor(activeFile);

  return <>
    <Form.List name="files">
      {(fields, { add, remove }) => {
        const stageUpload = async (upload: RcFile, key: string, callbacks?: {
          onProgress?: (event: { percent?: number }) => void;
          onSuccess?: (body?: unknown) => void;
          onError?: (error: Error) => void;
        }) => {
          setAttempts(current => ({ ...current, [key]: { key, file: upload, percent: 0, status: "uploading" } }));
          try {
            const staged = await CaveService.StageAuthoringFile(upload, key, (event) => {
              const percent = event.total ? Math.max(0, Math.min(100, Math.round(event.loaded * 100 / event.total))) : 0;
              setAttempts(current => ({ ...current, [key]: { key, file: upload, percent, status: "uploading" } }));
              callbacks?.onProgress?.({ percent });
            });
            add({
              id: staged.id,
              name: staged.name,
              extension: staged.extension,
              fileTypeTagId: staged.fileTypeTagId,
              fileTypeKey: staged.fileTypeKey,
            });
            setLocallyStagedFileIds(current => new Set(current).add(staged.id));
            removeAttempt(key);
            onAuthoringChange?.();
            callbacks?.onSuccess?.({});
          } catch (error) {
            const resolved = error instanceof Error ? error : new Error("The file could not be staged.");
            const text = (error as ApiErrorResponse)?.message ?? resolved.message ?? "The file could not be staged.";
            setAttempts(current => ({ ...current, [key]: { key, file: upload, percent: current[key]?.percent ?? 0,
              status: "error", error: text } }));
            message.error(text);
            callbacks?.onError?.(resolved);
          }
        };

        return <>
          <Upload.Dragger
            multiple
            showUploadList={false}
            customRequest={({ file, onProgress, onSuccess, onError }) => {
              const upload = file as RcFile;
              void stageUpload(upload, upload.uid, { onProgress, onSuccess, onError });
            }}
          >
            <p className="ant-upload-drag-icon"><InboxOutlined /></p>
            <p className="ant-upload-text">Add files to this Cave change</p>
            <p className="ant-upload-hint">Files are staged now and are not published until this form is saved or approved.</p>
          </Upload.Dragger>

          {Object.values(attempts).length > 0 && <Row gutter={[16, 16]} style={{ marginTop: 16 }}>
            {Object.values(attempts).map(attempt => <Col xs={24} lg={12} key={attempt.key}>
              <CaveAuthoringAssetCard
                title={attempt.file.name}
                ariaLabel={`${attempt.file.name} upload`}
                metadata="File upload"
                actions={attempt.status === "error" ? <>
                  <PlanarianButton size="small" icon={<ReloadOutlined />} alwaysShowChildren
                    onClick={() => void stageUpload(attempt.file, attempt.key)}>Retry</PlanarianButton>
                  <PlanarianButton size="small" alwaysShowChildren onClick={() => removeAttempt(attempt.key)}>Dismiss</PlanarianButton>
                </> : null}
                status={<>
                  <Progress percent={attempt.percent} size="small" status={attempt.status === "error" ? "exception" : "active"} />
                  {attempt.status === "error" && <Typography.Text type="danger">Upload failed: {attempt.error}</Typography.Text>}
                </>}
              />
            </Col>)}
          </Row>}

          <Row gutter={[16, 16]} style={{ marginTop: 16 }}>
            {fields.map((field) => {
              const file = files[field.name];
              const access = accessFor(file);
              return <Col xs={24} lg={12} key={field.key}>
                <CaveAuthoringAssetCard
                  title={file?.name ?? "File"}
                  ariaLabel={`${file?.name ?? "File"} file editor`}
                  metadata={file?.extension || file?.fileTypeKey}
                  actions={<>
                    {file?.id && <PlanarianButton size="small" icon={<EyeOutlined />} alwaysShowChildren
                      onClick={() => setActiveFileId(file.id)}>View</PlanarianButton>}
                    {file?.id && <PlanarianButton size="small" icon={<DownloadOutlined />} alwaysShowChildren
                      onClick={() => access.download ? access.download() : FileService.startFileDownload(file.id)}>Download</PlanarianButton>}
                    <PlanarianButton size="small" danger icon={<DeleteOutlined />} alwaysShowChildren
                      aria-label={`Remove ${file?.name ?? "file"}`}
                      onClick={() => {
                        remove(field.name);
                        onAuthoringChange?.();
                        if (file?.id && locallyStagedFileIds.has(file.id)) {
                          setLocallyStagedFileIds(current => {
                            const next = new Set(current); next.delete(file.id); return next;
                          });
                          void CaveService.DeleteAuthoringStagedFile(file.id).catch(() => undefined);
                        }
                      }}>Remove</PlanarianButton>
                  </>}
                >
                  <Form.Item name={[field.name, "id"]} hidden><Input /></Form.Item>
                  <Form.Item name={[field.name, "fileTypeKey"]} hidden><Input /></Form.Item>
                  <Form.Item name={[field.name, "extension"]} hidden><Input /></Form.Item>
                  <Form.Item label="Name" name={[field.name, "name"]}
                    rules={[{ required: true, whitespace: true, message: "Please enter a name" }]}>
                    <Input addonAfter={file?.extension || undefined} />
                  </Form.Item>
                  <Form.Item label="File Type" name={[field.name, "fileTypeTagId"]}
                    rules={[{ required: true, message: "Please select a file type" }]}>
                    <TagSelectComponent tagType={TagType.File} />
                  </Form.Item>
                </CaveAuthoringAssetCard>
              </Col>;
            })}
          </Row>
        </>;
      }}
    </Form.List>
    <FileViewer
      open={!!activeFile}
      onCancel={() => setActiveFileId(undefined)}
      fileId={activeFile?.id}
      name={activeFile ? `${activeFile.name}${activeFile.extension ?? ""}` : undefined}
      fileType={getFileType(activeFile?.extension)}
      viewUrl={activeAccess.viewUrl}
      onDownload={activeAccess.download}
    />
  </>;
};
