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

const FileHarness = ({ onFinish, onAuthoringChange, onValuesChange }: {
  onFinish: jest.Mock;
  onAuthoringChange?: jest.Mock;
  onValuesChange?: jest.Mock;
}) => {
  const [form] = Form.useForm<any>();
  return <Form form={form} onValuesChange={onValuesChange} initialValues={{ files: [{ id: 'file-existing', name: 'IRT',
    extension: '.DAT', fileTypeTagId: 'type-file', fileTypeKey: 'Data' }] }}>
    <CaveFileAuthoringEditor form={form} onAuthoringChange={onAuthoringChange} />
    <button type="button" onClick={() => onFinish(form.getFieldsValue())}>Read files</button>
  </Form>;
};

const LinePlotHarness = ({ onFinish, onAuthoringChange, onValuesChange }: {
  onFinish: jest.Mock;
  onAuthoringChange?: jest.Mock;
  onValuesChange?: jest.Mock;
}) => {
  const [form] = Form.useForm<any>();
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
