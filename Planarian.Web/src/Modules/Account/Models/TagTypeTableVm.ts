export interface TagTypeTableVm {
  tagTypeId: string;
  name: string;
  isUserModifiable: boolean;
  canRename: boolean;
  occurrences: number;
}
