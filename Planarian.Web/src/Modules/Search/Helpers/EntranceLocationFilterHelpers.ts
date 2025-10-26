import {
  formatCoordinateNumber,
  NestedKeyOf,
} from "../../../Shared/Helpers/StringHelpers";
import {
  QueryBuilder,
  QueryOperator,
} from "../Services/QueryBuilder";

export type EntranceLocationFilter = {
  latitude?: number;
  longitude?: number;
  radius?: number;
};

export const isValidEntranceLocationFilter = (
  filter: EntranceLocationFilter
): filter is Required<EntranceLocationFilter> => {
  const { latitude, longitude, radius } = filter;
  return (
    latitude !== undefined &&
    longitude !== undefined &&
    radius !== undefined &&
    Number.isFinite(latitude) &&
    Number.isFinite(longitude) &&
    Number.isFinite(radius) &&
    radius > 0
  );
};

export const serializeEntranceLocationFilter = (
  filter: EntranceLocationFilter
): string | null => {
  if (!isValidEntranceLocationFilter(filter)) {
    return null;
  }

  const latitude = formatCoordinateNumber(filter.latitude);
  const longitude = formatCoordinateNumber(filter.longitude);

  return `${latitude},${longitude},${filter.radius}`;
};

export const parseEntranceLocationFilter = (
  value: string | undefined | null
): EntranceLocationFilter => {
  if (!value) {
    return {};
  }

  const parts = value.split(",").map((part) => part.trim());
  if (parts.length !== 3) {
    return {};
  }

  const latitude = Number(parts[0]);
  const longitude = Number(parts[1]);
  const radius = Number(parts[2]);

  if (
    [latitude, longitude, radius].some(
      (entry) => Number.isNaN(entry) || !Number.isFinite(entry)
    )
  ) {
    return {};
  }

  return { latitude, longitude, radius };
};

export const applyEntranceLocationFilterToQuery = <T extends object>(
  queryBuilder: QueryBuilder<T>,
  field: NestedKeyOf<T>,
  filter: EntranceLocationFilter
) => {
  const serialized = serializeEntranceLocationFilter(filter);

  if (serialized) {
    queryBuilder.filterBy(
      field,
      QueryOperator.Equal,
      serialized as T[keyof T]
    );
  } else {
    queryBuilder.removeFromDictionary(field as string);
  }
};
