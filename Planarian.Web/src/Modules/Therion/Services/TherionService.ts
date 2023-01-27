import { ContinuationPoint } from "../Models/ContinuationPoint";
import { Coordinates } from "../Models/Coordinates";
import { Station } from "../Models/Station";

export const TherionService = {
  GetContinuationPoints(text: string): ContinuationPoint[] {
    const continuationPoints = readContinuationPoints(text);
    const stations = readStationPoints(text);

    // Find the closest station for each continuation point
    continuationPoints.forEach((continuationPoint) => {
      continuationPoint.closestStation = findClosestStation(
        continuationPoint,
        stations
      );
    });

    return continuationPoints;
  },
};

const readContinuationPoints = (text: string): ContinuationPoint[] => {
  const lines = text.split(/\r?\n/);
  const continuationPoints: ContinuationPoint[] = [];

  const regex = /point\s+(-?\d+.?\d*)\s+(-?\d+.?\d*)\s+(\w+)(.*)/;

  lines.forEach((line) => {
    const match = line.match(regex);
    if (match) {
      const x = Number(match[1]);
      const y = Number(match[2]);
      const pointType = match[3];

      if (pointType !== "continuation") {
        return;
      }

      const continuationPoint: ContinuationPoint = new ContinuationPoint(
        new Coordinates(x, y)
      );

      const args = match[4];

      // Extract argument name and value
      // Extract argument name and value
      const argRegex = /(-[a-zA-Z]+)\s+(?:"([^"]+)"|(\S+))/g;
      for (const argMatch of args.matchAll(argRegex)) {
        const argName = argMatch[1].replace(/^-+/, "");

        const argValue = argMatch[2].replace(/"+$/, "");

        if (argName === "text") {
          continuationPoint.description = argValue;
        }
      }

      continuationPoints.push(continuationPoint);
    }
  });

  return continuationPoints;
};

const readStationPoints = (text: string): Station[] => {
  const lines = text.split(/\r?\n/);
  const stations: Station[] = [];

  const regex = /point\s+(-?\d+.?\d*)\s+(-?\d+.?\d*)\s+(\w+)(.*)/;

  lines.forEach((line) => {
    const match = line.match(regex);
    if (match) {
      const x = Number(match[1]);
      const y = Number(match[2]);
      const pointType = match[3];

      if (pointType !== "station") {
        return;
      }

      const station: Station = new Station(new Coordinates(x, y));

      const args = match[4];

      // Extract argument name and value

      // Extract argument name and value
      const argRegex = /(-[a-zA-Z]+)\s+(?:""([^""]+)""|(\S+))/g;
      for (const argMatch of Array.from(args.matchAll(argRegex))) {
        const argName = argMatch[1].replace(/-/g, "");
        let argValue = argMatch[2]
          ? argMatch[2].replace(/^"|"$/g, "")
          : argMatch[3];

        argValue = argValue.replace(/^"|"$/g, ""); // remove leading and trailing quotes

        if (argName === "name") {
          station.name = argValue;
        }
      }

      stations.push(station);
    }
  });

  return stations;
};

const findClosestStation = (
  continuationPoint: ContinuationPoint,
  stations: Station[]
): Station | undefined => {
  let closestStation: Station | undefined = undefined;
  let minDistance = Number.MAX_VALUE;

  stations.forEach((station) => {
    const distance = Math.sqrt(
      Math.pow(station.coordinates.x - continuationPoint.coordinates.x, 2) +
        Math.pow(station.coordinates.y - continuationPoint.coordinates.y, 2)
    );
    if (distance < minDistance) {
      closestStation = station;
      minDistance = distance;
    }
  });

  return closestStation;
};
