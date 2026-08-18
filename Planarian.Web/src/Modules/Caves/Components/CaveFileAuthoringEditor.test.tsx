import { Form } from "antd";
import { render, screen } from "@testing-library/react";
import { AddCaveVm } from "../Models/AddCaveVm";
import { CaveFileAuthoringEditor } from "./CaveFileAuthoringEditor";

jest.mock("../../Tag/Components/TagSelectComponent", () => ({
  TagSelectComponent: () => <div>File type selector</div>,
}));

beforeAll(() => {
  Object.defineProperty(window, "matchMedia", {
    writable: true,
    value: () => ({ matches: false, addListener: () => undefined, removeListener: () => undefined }),
  });
});

const Editor = () => {
  const [form] = Form.useForm<AddCaveVm>();
  return <Form form={form} initialValues={{ files: [{
    id: "file-1", name: "Entrance Survey", extension: ".pdf",
    fileTypeTagId: "map", fileTypeKey: "Map",
  }] }}>
    <CaveFileAuthoringEditor form={form} />
  </Form>;
};

it("renders the extension as a read-only suffix beside the editable name", () => {
  render(<Editor />);

  expect(screen.getByLabelText("Name")).toHaveValue("Entrance Survey");
  expect(screen.getByText(".pdf")).toBeInTheDocument();
  expect(screen.queryByLabelText("Extension")).not.toBeInTheDocument();
});
