import { CaveVm } from "../../Modules/Caves/Models/CaveVm";
import dayjs from "dayjs";

export const StringHelpers = {
  GenerateAbbreviation(name: string | undefined): string {
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

export enum DistanceFormat {
  feet = "feet",
  miles = "miles",
  meters = "meters",
  kilometers = "kilometers",
}

export function formatDistance(
  distanceInFeet: number | undefined | null,
  distanceFormat?: DistanceFormat
): string | null {
  if (distanceInFeet === undefined || distanceInFeet === null) return null;

  const milesThreshold = 2640;
  const formatOptions = {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  } as Intl.NumberFormatOptions;

  if (distanceFormat) {
    let convertedValue: number;
    let unitLabel: string;
    switch (distanceFormat) {
      case DistanceFormat.feet:
        convertedValue = distanceInFeet;
        unitLabel = "ft";
        formatOptions.minimumFractionDigits = 0;
        break;
      case DistanceFormat.miles:
        convertedValue = distanceInFeet / 5280;
        unitLabel = "mi";
        break;
      case DistanceFormat.kilometers:
        convertedValue = distanceInFeet * 0.0003048;
        unitLabel = "km";
        break;
      case DistanceFormat.meters:
        convertedValue = distanceInFeet * 0.3048;
        unitLabel = "m";
        formatOptions.minimumFractionDigits = 0;
        break;
      default:
        convertedValue = distanceInFeet;
        unitLabel = "ft";
        formatOptions.minimumFractionDigits = 0;
    }
    return `${convertedValue.toLocaleString(
      undefined,
      formatOptions
    )} ${unitLabel}`;
  }

  // Otherwise do the current default logic
  if (distanceInFeet >= milesThreshold) {
    const miles = distanceInFeet / 5280;
    const formattedMiles = miles.toLocaleString(undefined, formatOptions);
    return `${formattedMiles} mi`;
  }

  formatOptions.minimumFractionDigits = 0;
  return `${distanceInFeet.toLocaleString(undefined, formatOptions)} ft`;
}

export function isNullOrWhiteSpace(
  input: string | null | undefined
): input is null | undefined {
  if (input == null || input == undefined) return true;
  return input.replace(/\s/g, "").length < 1;
}

export function getDirectionsUrl(
  latitude: string | number,
  longitude: string | number
): string {
  return `https://www.google.com/maps/dir/?api=1&destination=${latitude},${longitude}&travelmode=car`;
}

//#region nameof

export const nameof = <T extends object>(name: NestedKeyOf<T>): string => name;

// https://dev.to/pffigueiredo/typescript-utility-keyof-nested-object-2pa3
export type NestedKeyOf<ObjectType extends object> = {
  [Key in keyof ObjectType & (string | number)]: ObjectType[Key] extends object
    ? `${Key}` | `${Key}.${NestedKeyOf<ObjectType[Key]>}`
    : `${Key}`;
}[keyof ObjectType & (string | number)];

//#endregion
export function formatPhoneNumber(phoneNumber?: string): string {
  const formattedPhoneNumber = phoneNumber?.replace(
    /^(\+1)(\d{3})(\d{3})(\d{4})$/,
    "$1 ($2) $3-$4"
  );
  return formattedPhoneNumber ?? "";
}

export function formatDate(
  date: Date | string | null | undefined,
  formatString: string = "YYYY MMM-DD"
): string | null {
  // Check for null, undefined, or whitespace-only strings.
  if (typeof date === "string" && date.trim() === "") return null;
  if (date === null || date === undefined) return null;

  // Check if the date follows the pattern "YYYY-01-01 00:00:00+00:00"
  const yearPattern = /(\d{4})-01-01 00:00:00\+00:00/;
  const match = yearPattern.exec(date.toString());
  if (match) {
    return dayjs.utc(match[1], "YYYY").format(formatString);
  }

  return dayjs.utc(date).format(formatString);
}

export function formatDateTime(
  date: Date | string | null | undefined,
  formatString: string = "YYYY MMM-D h:mm A"
): string | null {
  if (typeof date === "string" && isNullOrWhiteSpace(date)) return null;
  if (date === null || date === undefined) return null;

  return dayjs(date).format(formatString);
}

export function formatBoolean(
  value: boolean | null | undefined
): string | null {
  if (value === null || value === undefined) {
    return null;
  }
  return value ? "Yes" : "No";
}

export function formatCoordinate(
  coordinate: number | null | undefined | string
): string | null {
  if (coordinate === null || coordinate === undefined) {
    return null;
  }

  const coord =
    typeof coordinate === "string" ? parseFloat(coordinate) : coordinate;

  if (isNaN(coord)) {
    return null;
  }

  return `${coord}`;
}

export function formatCoordinates(
  latitude: number | null | undefined | string,
  longitude: number | null | undefined | string
): string | null {
  const lat = formatCoordinate(latitude);
  const lon = formatCoordinate(longitude);

  if (lat === null || lon === null) {
    return null;
  }

  return `${lat}, ${lon}`;
}

export function formatNumber(value: number | null | undefined): string | null {
  if (value !== null && value !== undefined) {
    return value.toLocaleString();
  }
  return null;
}

export function splitCamelCase(input: string): string {
  const result = input.replace(/([a-z])([A-Z])/g, "$1 $2");
  return result;
}

export function defaultIfEmpty(value: string | null | undefined) {
  if (isNullOrWhiteSpace(value)) {
    return "-";
  } else return value;
}

export function capitalizeFirstLetter(
  input: string | null | undefined
): string {
  if (isNullOrWhiteSpace(input)) return "";
  return input.charAt(0).toUpperCase() + input.slice(1);
}

export function toCommaString(input: string[] | null | undefined): string {
  if (input == null || input.length < 1) return "";
  if (input.length === 1) return input[0];
  if (input.length === 2) return `${input[0]} and ${input[1]}`;
  const items = [...input]; // Create a copy of the array to avoid mutation
  const lastItem = items.pop();
  return `${items.join(", ")}, and ${lastItem}`;
}
