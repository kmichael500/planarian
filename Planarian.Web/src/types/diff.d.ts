declare module "diff" {
  export interface Change {
    value: string;
    count?: number;
    added?: boolean;
    removed?: boolean;
  }

  export interface DiffOptions {
    timeout?: number;
    maxEditLength?: number;
  }

  export function diffWordsWithSpace(
    oldText: string,
    newText: string,
    options?: DiffOptions
  ): Change[] | undefined;
}
