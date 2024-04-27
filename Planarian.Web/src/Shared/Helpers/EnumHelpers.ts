export function toEnum<E>(enumObj: E, key: string | null): E[keyof E] | null {
  for (const enumKey in enumObj) {
    if (enumObj[enumKey] === key) {
      return enumObj[enumKey];
    }
  }
  return null;
}
