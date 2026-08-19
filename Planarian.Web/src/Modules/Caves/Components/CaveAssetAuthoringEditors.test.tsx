import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Form } from 'antd';
import { CaveFileAuthoringEditor } from './CaveFileAuthoringEditor';
import { CaveLinePlotAuthoringEditor } from './CaveLinePlotAuthoringEditor';

jest.mock('../../Tag/Components/TagSelectComponent', () => ({
  TagSelectComponent: () => <div>File type</div>,
}));

beforeAll(() => {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: () => ({ matches: false, addListener: () => undefined, removeListener: () => undefined,
      addEventListener: () => undefined, removeEventListener: () => undefined, dispatchEvent: () => false }),
  });
});

const FileHarness = ({ onFinish }: { onFinish: jest.Mock }) => {
  const [form] = Form.useForm<any>();
  return <Form form={form} initialValues={{ files: [{ id: 'file-existing', name: 'IRT',
    extension: '.DAT', fileTypeTagId: 'type-file', fileTypeKey: 'Data' }] }}>
    <CaveFileAuthoringEditor form={form} />
    <button type="button" onClick={() => onFinish(form.getFieldsValue())}>Read files</button>
  </Form>;
};const LinePlotHarness = ({ onFinish }: { onFinish: jest.Mock }) => {
  const [form] = Form.useForm<any>();
  return <Form form={form} initialValues={{ linePlots: [{ id: 'line-existing',
    name: 'Main line', geoJson: '{"type":"FeatureCollection","features":[]}' }] }}>
    <CaveLinePlotAuthoringEditor form={form} />
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