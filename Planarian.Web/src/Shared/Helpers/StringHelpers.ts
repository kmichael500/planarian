export const StringHelpers = {
  NameToInitials(name: string | undefined): string {
    if (name == undefined) return "";
    return name
      .split(" ")
      .map((namePart) => {
        if (typeof namePart[0] !== undefined) {
          return namePart[0];
        }
      })
      .join("");
  },
};

export function isNullOrWhiteSpace(input: string | null | undefined): boolean {
  if (input == null || input == undefined) return true;
  return input.replace(/\s/g, "").length < 1;
}

export const nameof = <T>(name: Extract<keyof T, string>): string => name;

export function formatPhoneNumber(phoneNumber?: string): string {
  const formattedPhoneNumber = phoneNumber?.replace(
    /^(\+1)(\d{3})(\d{3})(\d{4})$/,
    "$1 ($2) $3-$4"
  );
  return formattedPhoneNumber ?? "";
}
