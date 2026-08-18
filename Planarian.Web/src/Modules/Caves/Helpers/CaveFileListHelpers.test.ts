import { fileAtFormListIndex } from "./CaveFileListHelpers";

test("uses the current Form.List name after an earlier file is removed", () => {
  const remainingFiles = [
    {
      id: "file-b",
      fileTypeTagId: "type-b",
      fileTypeKey: "Survey",
      name: "File B",
    },
  ];

  // Ant Design may retain B's stable field key as 1, but its current name/index is 0.
  expect(fileAtFormListIndex(remainingFiles, 0)).toEqual(remainingFiles[0]);
  expect(fileAtFormListIndex(remainingFiles, 1)).toBeUndefined();
});
