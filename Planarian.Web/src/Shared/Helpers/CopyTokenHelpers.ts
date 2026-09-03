const copyTokenAttribute = "data-planarian-copy-token" as const;

type CopyTokenAttributes = {
  [copyTokenAttribute]: true;
};

export const copyTokenAttributes: CopyTokenAttributes = {
  [copyTokenAttribute]: true,
};

const isCopyTokenElement = (element: Element): element is HTMLElement =>
  element instanceof HTMLElement && element.hasAttribute(copyTokenAttribute);

export const getCopyTokenElements = (root: HTMLElement) =>
  Array.from(root.getElementsByTagName("*")).filter(isCopyTokenElement);

export const isWithinCopyToken = (element: Element | null) => {
  for (let current = element; current; current = current.parentElement) {
    if (isCopyTokenElement(current)) return true;
  }

  return false;
};
