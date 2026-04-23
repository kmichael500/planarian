export interface CreateAccountVm {
  name: string;
  countyIdDelimiter?: string;
  stateIds: string[];
  defaultViewAccessAllCaves: boolean;
  exportEnabled: boolean;
}
