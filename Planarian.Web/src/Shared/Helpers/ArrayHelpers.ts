export function distinct(array: string[]): string[] {
  return [...new Set(array)];
}

export function customSort(inputArray: string[], stringArray: string[]) {
  return stringArray.sort((a, b) => {
    const indexA = inputArray.indexOf(a);
    const indexB = inputArray.indexOf(b);

    // If both strings are in the input array, sort them by their positions.
    if (indexA !== -1 && indexB !== -1) {
      return indexA - indexB;
    }

    // If only one of the strings is in the input array, prioritize it.
    if (indexA !== -1) {
      return -1;
    }
    if (indexB !== -1) {
      return 1;
    }

    // If neither string is in the input array, sort them alphabetically.
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
