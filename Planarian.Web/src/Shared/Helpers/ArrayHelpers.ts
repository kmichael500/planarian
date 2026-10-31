import { SelectListItem } from "../Models/SelectListItem";

export const sortSelectListItems = <T>(
  SelectListItems: SelectListItem<T>[]
) => {
  return (
    SelectListItems?.sort((a, b) => a.display.localeCompare(b.display)) || []
  );
};

export const searchFilterSelectListItems = (
  input: string,
  option: any
) => {
  return option?.props.children.toLowerCase().includes(input.toLowerCase());
};

export function customSort(inputArray: string[], stringArray: string[]) {
  return stringArray.sort((a, b) => {
    const indexA = inputArray.indexOf(a);
    const indexB = inputArray.indexOf(b);

    if (indexA !== -1 && indexB !== -1) {
      return indexA - indexB;
    }

    if (indexA !== -1) {
      return -1;
    }
    if (indexB !== -1) {
      return 1;
    }

    return a.localeCompare(b);
  });
}

export function groupBy<T, K extends string>(
  items: T[],
  keyExtractor: (item: T) => K
): { [key: string]: T[] } {
  const result: { [key: string]: T[] } = {};

  items.forEach((item) => {
    const key = keyExtractor(item);
    if (!result[key]) {
      result[key] = [];
    }
    result[key].push(item);
  });

  return result;
}
