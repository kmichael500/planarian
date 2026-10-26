import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Form } from 'antd';
import { CaveFileAuthoringEditor } from './CaveFileAuthoringEditor';
import { CaveLinePlotAuthoringEditor } from './CaveLinePlotAuthoringEditor';

jest.mock("./CaveLinePlotViewer", () => ({
  CaveLinePlotViewer: ({ open, linePlot }: { open: boolean; linePlot?: { name: string } }) =>
    open ? <div>Viewing line plot {linePlot?.name}</div> : null,
}));
import { CaveService } from '../Service/CaveService';
import { FileService } from '../../Files/Services/FileService';
import { parseCaveLinePlotFile } from '../../Map/Helpers/LinePlotFileParser';
import { AddCaveVm } from '../Models/AddCaveVm';

jest.mock('../../Map/Helpers/LinePlotFileParser', () => ({
  parseCaveLinePlotFile: jest.fn(),
}));

jest.mock('../../Tag/Components/TagSelectComponent', () => ({
  TagSelectComponent: () => <div>File type</div>,
}));

jest.mock('../../Files/Components/FileViewerComponent', () => ({
  FileViewer: ({ open, viewUrl, onDownload }: { open: boolean; viewUrl?: string; onDownload?: () => void }) =>
    open ? <div role="dialog" aria-label="File preview"><span>{viewUrl}</span>
      <button onClick={onDownload}>Viewer download</button></div> : null,
}));

beforeAll(() => {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: () => ({ matches: false, addListener: () => undefined, removeListener: () => undefined,
      addEventListener: () => undefined, removeEventListener: () => undefined, dispatchEvent: () => false }),
  });
});

afterEach(() => jest.restoreAllMocks());

const FileHarness = ({ onFinish, onAuthoringChange, onValuesChange, stagedFileRequestId,
  existingRequestStagedFileIds }: {
  onFinish: jest.Mock;
  onAuthoringChange?: jest.Mock;
  onValuesChange?: jest.Mock;
  stagedFileRequestId?: string;
  existingRequestStagedFileIds?: readonly string[];
}) => {
  const [form] = Form.useForm<AddCaveVm>();
  return <Form form={form} onValuesChange={onValuesChange} initialValues={{ files: [{ id: 'file-existing', name: 'IRT',
    extension: '.DAT', fileTypeTagId: 'type-file', fileTypeKey: 'Data' }] }}>
    <CaveFileAuthoringEditor form={form} onAuthoringChange={onAuthoringChange}
      stagedFileRequestId={stagedFileRequestId} existingRequestStagedFileIds={existingRequestStagedFileIds} />
    <button type="button" onClick={() => onFinish(form.getFieldsValue())}>Read files</button>
  </Form>;
};

const LinePlotHarness = ({ onFinish, onAuthoringChange, onValuesChange }: {
  onFinish: jest.Mock;
  onAuthoringChange?: jest.Mock;
  onValuesChange?: jest.Mock;
}) => {
  const [form] = Form.useForm<AddCaveVm>();
  return <Form form={form} onValuesChange={onValuesChange} initialValues={{ linePlots: [{ id: 'line-existing',
    name: 'Main line', geoJson: '{"type":"FeatureCollection","features":[]}' }] }}>
    <CaveLinePlotAuthoringEditor form={form} onAuthoringChange={onAuthoringChange} />
    <button type="button" onClick={() => onFinish(form.getFieldsValue())}>Read line plots</button>
  </Form>;
};

it('omits an existing published file from submitted form values after removal', async () => {
  const onFinish = jest.fn();
  render(<FileHarness onFinish={onFinish} />);

  await userEvent.click(screen.getByRole('button', { name: 'Remove IRT' }));
  await userEvent.click(screen.getByRole('button', { name: 'Read files' }));

  expect(onFinish).toHaveBeenCalledWith(expect.objectContaining({ files: [] }));
});

it('omits an existing line plot from submitted form values after removal', async () => {
  const onFinish = jest.fn();
  render(<LinePlotHarness onFinish={onFinish} />);

  await userEvent.click(screen.getByRole('button', { name: 'Remove Main line' }));
  await userEvent.click(screen.getByRole('button', { name: 'Read line plots' }));

  expect(onFinish).toHaveBeenCalledWith(expect.objectContaining({ linePlots: [] }));
});
it('reports file removal through the shared authoring-change callback', async () => {
  const onAuthoringChange = jest.fn();
  render(<FileHarness onFinish={jest.fn()} onAuthoringChange={onAuthoringChange} />);

  await userEvent.click(screen.getByRole('button', { name: 'Remove IRT' }));

  expect(onAuthoringChange).toHaveBeenCalledTimes(1);
});

it('reports line-plot removal through the shared authoring-change callback', async () => {
  const onAuthoringChange = jest.fn();
  render(<LinePlotHarness onFinish={jest.fn()} onAuthoringChange={onAuthoringChange} />);

  await userEvent.click(screen.getByRole('button', { name: 'Remove Main line' }));

  expect(onAuthoringChange).toHaveBeenCalledTimes(1);
});

it('surfaces file metadata edits through the parent form change handler', async () => {
  const onValuesChange = jest.fn();
  render(<FileHarness onFinish={jest.fn()} onValuesChange={onValuesChange} />);

  const name = screen.getByRole('textbox', { name: 'Name' });
  await userEvent.clear(name);
  await userEvent.type(name, 'Updated file name');

  expect(onValuesChange).toHaveBeenCalled();
});

it('surfaces line-plot metadata edits through the parent form change handler', async () => {
  const onValuesChange = jest.fn();
  render(<LinePlotHarness onFinish={jest.fn()} onValuesChange={onValuesChange} />);

  const name = screen.getByRole('textbox', { name: 'Name' });
  await userEvent.clear(name);
  await userEvent.type(name, 'Updated line plot name');

  expect(onValuesChange).toHaveBeenCalled();
});

it('keeps existing request-staged files on request-scoped view and download access', async () => {
  const viewUrl = jest.spyOn(CaveService, 'GetStagedChangeRequestFileUrl')
    .mockReturnValue('/api/cave-change-requests/request001/files/file-existing/view');
  const stagedDownload = jest.spyOn(CaveService, 'StartStagedChangeRequestFileDownload')
    .mockImplementation(() => undefined);
  const publishedDownload = jest.spyOn(FileService, 'startFileDownload').mockImplementation(() => undefined);

  render(<FileHarness onFinish={jest.fn()} stagedFileRequestId="request001"
    existingRequestStagedFileIds={['file-existing']} />);

  const fileEditor = screen.getByRole('group', { name: 'IRT file editor' });
  await userEvent.click(within(fileEditor).getByRole('button', { name: /Download/i }));
  expect(stagedDownload).toHaveBeenCalledWith('request001', 'file-existing');
  expect(publishedDownload).not.toHaveBeenCalled();

  await userEvent.click(within(fileEditor).getByRole('button', { name: /View/i }));
  expect(viewUrl).toHaveBeenCalledWith('request001', 'file-existing', 'view');
  expect(screen.getByRole('dialog', { name: 'File preview' })).toHaveTextContent(
    '/api/cave-change-requests/request001/files/file-existing/view');

  await userEvent.click(screen.getByRole('button', { name: 'Viewer download' }));
  expect(stagedDownload).toHaveBeenCalledTimes(2);
  expect(publishedDownload).not.toHaveBeenCalled();
});


it('preserves the stable File ID when metadata is edited', async () => {
  const onFinish = jest.fn();
  render(<FileHarness onFinish={onFinish} />);

  const name = screen.getByRole('textbox', { name: 'Name' });
  await userEvent.clear(name);
  await userEvent.type(name, 'Renamed IRT');
  await userEvent.click(screen.getByRole('button', { name: 'Read files' }));

  expect(onFinish).toHaveBeenCalledWith(expect.objectContaining({
    files: [expect.objectContaining({ id: 'file-existing', name: 'Renamed IRT' })],
  }));
});

it('preserves the stable line-plot ID when metadata is edited', async () => {
  const onFinish = jest.fn();
  render(<LinePlotHarness onFinish={onFinish} />);

  const name = screen.getByRole('textbox', { name: 'Name' });
  await userEvent.clear(name);
  await userEvent.type(name, 'Renamed main line');
  await userEvent.click(screen.getByRole('button', { name: 'Read line plots' }));

  expect(onFinish).toHaveBeenCalledWith(expect.objectContaining({
    linePlots: [expect.objectContaining({ id: 'line-existing', name: 'Renamed main line' })],
  }));
});

it('removes a newly staged File from desired state and cleans up the unpublished staging object', async () => {
  const onFinish = jest.fn();
  const stage = jest.spyOn(CaveService, 'StageAuthoringFile').mockResolvedValue({
    id: 'file-new', name: 'New map', extension: '.pdf', fileTypeTagId: 'type-file', fileTypeKey: 'Map',
  } as any);
  const removeStaged = jest.spyOn(CaveService, 'DeleteAuthoringStagedFile').mockResolvedValue();
  const { container } = render(<FileHarness onFinish={onFinish} />);
  const input = container.querySelector('input[type="file"]') as HTMLInputElement;

  await userEvent.upload(input, new File(['bytes'], 'new-map.pdf', { type: 'application/pdf' }));

  await waitFor(() => expect(stage).toHaveBeenCalledTimes(1));
  await screen.findByRole('group', { name: 'New map file editor' });
  await userEvent.click(screen.getByRole('button', { name: 'Remove New map' }));
  await userEvent.click(screen.getByRole('button', { name: 'Read files' }));

  await waitFor(() => expect(removeStaged).toHaveBeenCalledWith('file-new'));
  expect(onFinish).toHaveBeenCalledWith(expect.objectContaining({
    files: [expect.objectContaining({ id: 'file-existing' })],
  }));
  expect(onFinish.mock.calls[0][0].files.some((file: any) => file.id === 'file-new')).toBe(false);
});

it('does not submit a phantom line plot when one is added and removed before save', async () => {
  const onFinish = jest.fn();
  (parseCaveLinePlotFile as jest.MockedFunction<typeof parseCaveLinePlotFile>).mockResolvedValue(
    [{ type: 'FeatureCollection', features: [] }] as any
  );
  const { container } = render(<LinePlotHarness onFinish={onFinish} />);
  const input = container.querySelectorAll('input[type="file"]')[0] as HTMLInputElement;

  await userEvent.upload(input, new File(
    ['{"type":"FeatureCollection","features":[]}'],
    'new-line.geojson',
    { type: 'application/geo+json' }
  ));

  await screen.findByRole('group', { name: 'new-line line plot editor' });
  await userEvent.click(screen.getByRole('button', { name: 'Remove new-line' }));
  await userEvent.click(screen.getByRole('button', { name: 'Read line plots' }));

  expect(onFinish).toHaveBeenCalledWith(expect.objectContaining({
    linePlots: [expect.objectContaining({ id: 'line-existing' })],
  }));
  expect(onFinish.mock.calls[0][0].linePlots).toHaveLength(1);
});
